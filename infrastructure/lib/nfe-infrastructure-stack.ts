import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as ec2 from 'aws-cdk-lib/aws-ec2';
import * as rds from 'aws-cdk-lib/aws-rds';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as apigateway from 'aws-cdk-lib/aws-apigateway';
import * as sqs from 'aws-cdk-lib/aws-sqs';
import * as secretsmanager from 'aws-cdk-lib/aws-secretsmanager';
import * as cloudwatch from 'aws-cdk-lib/aws-cloudwatch';
import * as sns from 'aws-cdk-lib/aws-sns';
import * as snsSubscriptions from 'aws-cdk-lib/aws-sns-subscriptions';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as wafv2 from 'aws-cdk-lib/aws-wafv2';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as events from 'aws-cdk-lib/aws-events';
import * as targets from 'aws-cdk-lib/aws-events-targets';
import * as lambdaEventSources from 'aws-cdk-lib/aws-lambda-event-sources';

export interface NFeInfrastructureStackProps extends cdk.StackProps {
  stage: 'dev' | 'prod';
}

export class NFeInfrastructureStack extends cdk.Stack {
  public readonly vpc: ec2.Vpc;
  public readonly database: rds.DatabaseInstance;
  public readonly apiFunction: lambda.Function;
  public readonly workerFunction: lambda.Function;
  public readonly api: apigateway.RestApi;

  constructor(scope: Construct, id: string, props: NFeInfrastructureStackProps) {
    super(scope, id, props);

    const { stage } = props;
    const isProd = stage === 'prod';

    // =================
    // VPC Configuration
    // =================
    this.vpc = new ec2.Vpc(this, 'NFeVpc', {
      maxAzs: isProd ? 3 : 2,
      natGateways: isProd ? 2 : 1,
      subnetConfiguration: [
        {
          name: 'Public',
          subnetType: ec2.SubnetType.PUBLIC,
          cidrMask: 24,
        },
        {
          name: 'Private',
          subnetType: ec2.SubnetType.PRIVATE_WITH_EGRESS,
          cidrMask: 24,
        },
        {
          name: 'Database',
          subnetType: ec2.SubnetType.PRIVATE_ISOLATED,
          cidrMask: 28,
        },
      ],
      enableDnsHostnames: true,
      enableDnsSupport: true,
    });

    // VPC Endpoints for cost optimization
    this.vpc.addGatewayEndpoint('S3Endpoint', {
      service: ec2.GatewayVpcEndpointAwsService.S3,
      subnets: [{ subnetType: ec2.SubnetType.PRIVATE_WITH_EGRESS }],
    });

    this.vpc.addInterfaceEndpoint('SecretsManagerEndpoint', {
      service: ec2.InterfaceVpcEndpointAwsService.SECRETS_MANAGER,
      subnets: { subnetType: ec2.SubnetType.PRIVATE_WITH_EGRESS },
      securityGroups: [this.createVpcEndpointSecurityGroup()],
    });

    // ===================
    // Security Groups
    // ===================
    const dbSecurityGroup = new ec2.SecurityGroup(this, 'DatabaseSecurityGroup', {
      vpc: this.vpc,
      description: 'Security group for RDS PostgreSQL database',
      allowAllOutbound: false,
    });

    const lambdaSecurityGroup = new ec2.SecurityGroup(this, 'LambdaSecurityGroup', {
      vpc: this.vpc,
      description: 'Security group for Lambda functions',
      allowAllOutbound: true,
    });

    // Allow Lambda to connect to RDS
    dbSecurityGroup.addIngressRule(
      lambdaSecurityGroup,
      ec2.Port.tcp(5432),
      'Allow Lambda functions to connect to PostgreSQL'
    );

    // =================
    // S3 Buckets
    // =================
    const nfeXmlsBucket = new s3.Bucket(this, 'NFeXmlsBucket', {
      bucketName: `nfe-xmls-${stage}-${this.account}`,
      encryption: s3.BucketEncryption.S3_MANAGED,
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      enforceSSL: true,
      versioned: isProd,
      lifecycleRules: [
        {
          id: 'DeleteOldVersions',
          enabled: true,
          noncurrentVersionExpiration: cdk.Duration.days(90),
        },
        {
          id: 'TransitionToIA',
          enabled: true,
          transitions: [
            {
              storageClass: s3.StorageClass.INFREQUENT_ACCESS,
              transitionAfter: cdk.Duration.days(30),
            },
          ],
        },
      ],
    });

    const danfesBucket = new s3.Bucket(this, 'DanfesBucket', {
      bucketName: `nfe-danfes-${stage}-${this.account}`,
      encryption: s3.BucketEncryption.S3_MANAGED,
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      enforceSSL: true,
      versioned: isProd,
      lifecycleRules: [
        {
          id: 'TransitionToIA',
          enabled: true,
          transitions: [
            {
              storageClass: s3.StorageClass.INFREQUENT_ACCESS,
              transitionAfter: cdk.Duration.days(60),
            },
          ],
        },
      ],
    });

    const logsBucket = new s3.Bucket(this, 'LogsBucket', {
      bucketName: `nfe-logs-${stage}-${this.account}`,
      encryption: s3.BucketEncryption.S3_MANAGED,
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      enforceSSL: true,
      lifecycleRules: [
        {
          id: 'DeleteOldLogs',
          enabled: true,
          expiration: cdk.Duration.days(isProd ? 365 : 90),
        },
      ],
    });

    // =================
    // SQS Queues
    // =================
    const deadLetterQueue = new sqs.Queue(this, 'NFeProcessingDLQ', {
      queueName: `nfe-processing-dlq-${stage}`,
      encryption: sqs.QueueEncryption.SQS_MANAGED,
      retentionPeriod: cdk.Duration.days(14),
    });

    const processingQueue = new sqs.Queue(this, 'NFeProcessingQueue', {
      queueName: `nfe-processing-${stage}`,
      encryption: sqs.QueueEncryption.SQS_MANAGED,
      visibilityTimeout: cdk.Duration.minutes(15),
      retentionPeriod: cdk.Duration.days(14),
      deadLetterQueue: {
        queue: deadLetterQueue,
        maxReceiveCount: 3,
      },
    });

    // =================
    // Secrets Manager
    // =================
    const certificateSecret = new secretsmanager.Secret(this, 'CertificateA1Secret', {
      secretName: `nfe-certificate-a1-${stage}`,
      description: 'Certificado A1 para assinatura digital de NFe',
      generateSecretString: {
        secretStringTemplate: JSON.stringify({
          certificatePem: '',
          privateKeyPem: '',
          password: ''
        }),
        generateStringKey: 'password',
        passwordLength: 32,
        excludeCharacters: '"@/\\\'',
      },
    });

    const dbSecret = new secretsmanager.Secret(this, 'DatabaseSecret', {
      secretName: `nfe-database-credentials-${stage}`,
      description: 'Database credentials for NFe system',
      generateSecretString: {
        secretStringTemplate: JSON.stringify({ username: 'nfeadmin' }),
        generateStringKey: 'password',
        passwordLength: 32,
        excludeCharacters: '"@/\\\'',
      },
    });

    // =================
    // RDS PostgreSQL
    // =================
    const dbSubnetGroup = new rds.SubnetGroup(this, 'DatabaseSubnetGroup', {
      vpc: this.vpc,
      description: 'Subnet group for NFe database',
      vpcSubnets: { subnetType: ec2.SubnetType.PRIVATE_ISOLATED },
    });

    this.database = new rds.DatabaseInstance(this, 'NFeDatabase', {
      engine: rds.DatabaseInstanceEngine.postgres({
        version: rds.PostgresEngineVersion.VER_16_6,
      }),
      instanceType: isProd 
        ? ec2.InstanceType.of(ec2.InstanceClass.T3, ec2.InstanceSize.MEDIUM)
        : ec2.InstanceType.of(ec2.InstanceClass.T3, ec2.InstanceSize.MICRO),
      credentials: rds.Credentials.fromSecret(dbSecret),
      vpc: this.vpc,
      subnetGroup: dbSubnetGroup,
      securityGroups: [dbSecurityGroup],
      multiAz: isProd,
      allocatedStorage: isProd ? 100 : 20,
      storageEncrypted: true,
      backupRetention: isProd ? cdk.Duration.days(7) : cdk.Duration.days(1),
      deletionProtection: isProd,
      databaseName: 'nfedb',
      enablePerformanceInsights: isProd,
      performanceInsightRetention: isProd 
        ? rds.PerformanceInsightRetention.LONG_TERM 
        : rds.PerformanceInsightRetention.DEFAULT,
    });

    // =================
    // Lambda Functions
    // =================
    const lambdaRole = new iam.Role(this, 'LambdaExecutionRole', {
      assumedBy: new iam.ServicePrincipal('lambda.amazonaws.com'),
      managedPolicies: [
        iam.ManagedPolicy.fromAwsManagedPolicyName('service-role/AWSLambdaVPCAccessExecutionRole'),
      ],
      inlinePolicies: {
        NFePolicy: new iam.PolicyDocument({
          statements: [
            new iam.PolicyStatement({
              effect: iam.Effect.ALLOW,
              actions: [
                's3:GetObject',
                's3:PutObject',
                's3:DeleteObject',
              ],
              resources: [
                nfeXmlsBucket.arnForObjects('*'),
                danfesBucket.arnForObjects('*'),
                logsBucket.arnForObjects('*'),
              ],
            }),
            new iam.PolicyStatement({
              effect: iam.Effect.ALLOW,
              actions: [
                'sqs:SendMessage',
                'sqs:ReceiveMessage',
                'sqs:DeleteMessage',
                'sqs:GetQueueAttributes',
              ],
              resources: [processingQueue.queueArn, deadLetterQueue.queueArn],
            }),
            new iam.PolicyStatement({
              effect: iam.Effect.ALLOW,
              actions: [
                'secretsmanager:GetSecretValue',
              ],
              resources: [certificateSecret.secretArn, dbSecret.secretArn],
            }),
          ],
        }),
      },
    });

    // API Lambda Function
    this.apiFunction = new lambda.Function(this, 'NFeApiFunction', {
      functionName: `nfe-api-${stage}`,
      runtime: lambda.Runtime.DOTNET_8,
      handler: 'NFe.API::NFe.API.LambdaEntryPoint::FunctionHandlerAsync',
      code: lambda.Code.fromAsset('../NFe.API/bin/Release/net8.0/publish'),
      timeout: cdk.Duration.seconds(30),
      memorySize: isProd ? 1024 : 512,
      role: lambdaRole,
      vpc: this.vpc,
      vpcSubnets: { subnetType: ec2.SubnetType.PRIVATE_WITH_EGRESS },
      securityGroups: [lambdaSecurityGroup],
      environment: {
        ASPNETCORE_ENVIRONMENT: stage === 'prod' ? 'Production' : 'Development',
        ConnectionStrings__DefaultConnection: `Host=${this.database.instanceEndpoint.hostname};Port=5432;Database=nfedb;Username={username};Password={password}`,
        AWS_REGION: this.region,
        S3_XMLS_BUCKET: nfeXmlsBucket.bucketName,
        S3_DANFES_BUCKET: danfesBucket.bucketName,
        S3_LOGS_BUCKET: logsBucket.bucketName,
        SQS_PROCESSING_QUEUE: processingQueue.queueUrl,
        SECRETS_CERTIFICATE: certificateSecret.secretName,
        SECRETS_DATABASE: dbSecret.secretName,
      },
      tracing: lambda.Tracing.ACTIVE,
      logRetention: isProd ? logs.RetentionDays.ONE_MONTH : logs.RetentionDays.ONE_WEEK,
    });

    // Worker Lambda Function
    this.workerFunction = new lambda.Function(this, 'NFeWorkerFunction', {
      functionName: `nfe-worker-${stage}`,
      runtime: lambda.Runtime.DOTNET_8,
      handler: 'NFe.Worker::NFe.Worker.Function::FunctionHandler',
      code: lambda.Code.fromAsset('../NFe.Worker/bin/Release/net8.0/publish'),
      timeout: cdk.Duration.minutes(15),
      memorySize: isProd ? 2048 : 1024,
      role: lambdaRole,
      vpc: this.vpc,
      vpcSubnets: { subnetType: ec2.SubnetType.PRIVATE_WITH_EGRESS },
      securityGroups: [lambdaSecurityGroup],
      environment: {
        ASPNETCORE_ENVIRONMENT: stage === 'prod' ? 'Production' : 'Development',
        ConnectionStrings__DefaultConnection: `Host=${this.database.instanceEndpoint.hostname};Port=5432;Database=nfedb;Username={username};Password={password}`,
        AWS_REGION: this.region,
        S3_XMLS_BUCKET: nfeXmlsBucket.bucketName,
        S3_DANFES_BUCKET: danfesBucket.bucketName,
        S3_LOGS_BUCKET: logsBucket.bucketName,
        SECRETS_CERTIFICATE: certificateSecret.secretName,
        SECRETS_DATABASE: dbSecret.secretName,
      },
      tracing: lambda.Tracing.ACTIVE,
      logRetention: isProd ? logs.RetentionDays.ONE_MONTH : logs.RetentionDays.ONE_WEEK,
    });

    // Connect Worker to SQS
    this.workerFunction.addEventSource(new lambdaEventSources.SqsEventSource(processingQueue, {
      batchSize: 10,
      maxBatchingWindow: cdk.Duration.seconds(10),
    }));

    // =================
    // API Gateway
    // =================
    this.api = new apigateway.RestApi(this, 'NFeApi', {
      restApiName: `nfe-api-${stage}`,
      description: `NFe API - ${stage.toUpperCase()} environment`,
      deployOptions: {
        stageName: stage,
        throttle: {
          rateLimit: isProd ? 1000 : 100,
          burstLimit: isProd ? 2000 : 200,
        },
        loggingLevel: apigateway.MethodLoggingLevel.INFO,
        dataTraceEnabled: !isProd,
        metricsEnabled: true,
      },
      endpointConfiguration: {
        types: [apigateway.EndpointType.REGIONAL],
      },
      cloudWatchRole: true,
    });

    const integration = new apigateway.LambdaIntegration(this.apiFunction, {
      requestTemplates: { 'application/json': '{ "statusCode": "200" }' },
    });

    this.api.root.addMethod('ANY', integration);
    this.api.root.addProxy({
      defaultIntegration: integration,
      anyMethod: true,
    });

    // =================
    // WAF Protection
    // =================
    const webAcl = new wafv2.CfnWebACL(this, 'NFeApiWAF', {
      name: `nfe-api-waf-${stage}`,
      scope: 'REGIONAL',
      defaultAction: { allow: {} },
      rules: [
        {
          name: 'AWSManagedRulesCommonRuleSet',
          priority: 1,
          overrideAction: { none: {} },
          statement: {
            managedRuleGroupStatement: {
              vendorName: 'AWS',
              name: 'AWSManagedRulesCommonRuleSet',
            },
          },
          visibilityConfig: {
            sampledRequestsEnabled: true,
            cloudWatchMetricsEnabled: true,
            metricName: 'CommonRuleSetMetric',
          },
        },
        {
          name: 'RateLimitRule',
          priority: 2,
          action: { block: {} },
          statement: {
            rateBasedStatement: {
              limit: isProd ? 2000 : 500,
              aggregateKeyType: 'IP',
            },
          },
          visibilityConfig: {
            sampledRequestsEnabled: true,
            cloudWatchMetricsEnabled: true,
            metricName: 'RateLimitMetric',
          },
        },
      ],
      visibilityConfig: {
        sampledRequestsEnabled: true,
        cloudWatchMetricsEnabled: true,
        metricName: `NFeApiWAF${stage}`,
      },
    });

    new wafv2.CfnWebACLAssociation(this, 'NFeApiWAFAssociation', {
      resourceArn: this.api.deploymentStage.stageArn,
      webAclArn: webAcl.attrArn,
    });

    // =================
    // Monitoring & Alarms
    // =================
    const alertsTopic = new sns.Topic(this, 'NFeAlertsTopic', {
      topicName: `nfe-alerts-${stage}`,
      displayName: `NFe System Alerts - ${stage.toUpperCase()}`,
    });

    // Lambda Function Alarms
    this.createLambdaAlarms(this.apiFunction, 'API', alertsTopic, isProd);
    this.createLambdaAlarms(this.workerFunction, 'Worker', alertsTopic, isProd);

    // API Gateway Alarms
    this.createApiGatewayAlarms(alertsTopic, isProd);

    // Database Alarms
    this.createDatabaseAlarms(alertsTopic, isProd);

    // SQS Alarms
    this.createSqsAlarms(processingQueue, deadLetterQueue, alertsTopic, isProd);

    // =================
    // Outputs
    // =================
    new cdk.CfnOutput(this, 'ApiUrl', {
      value: this.api.url,
      description: 'NFe API Gateway URL',
      exportName: `NFeApiUrl-${stage}`,
    });

    new cdk.CfnOutput(this, 'DatabaseEndpoint', {
      value: this.database.instanceEndpoint.hostname,
      description: 'RDS PostgreSQL endpoint',
      exportName: `NFeDbEndpoint-${stage}`,
    });

    new cdk.CfnOutput(this, 'ProcessingQueueUrl', {
      value: processingQueue.queueUrl,
      description: 'SQS Processing Queue URL',
      exportName: `NFeProcessingQueue-${stage}`,
    });
  }

  private createVpcEndpointSecurityGroup(): ec2.SecurityGroup {
    const sg = new ec2.SecurityGroup(this, 'VpcEndpointSecurityGroup', {
      vpc: this.vpc,
      description: 'Security group for VPC endpoints',
      allowAllOutbound: false,
    });

    sg.addIngressRule(
      ec2.Peer.ipv4(this.vpc.vpcCidrBlock),
      ec2.Port.tcp(443),
      'Allow HTTPS from VPC'
    );

    return sg;
  }

  private createLambdaAlarms(fn: lambda.Function, name: string, topic: sns.Topic, isProd: boolean): void {
    // Error Rate Alarm
    new cloudwatch.Alarm(this, `${name}LambdaErrorAlarm`, {
      metric: fn.metricErrors({
        period: cdk.Duration.minutes(5),
      }),
      threshold: isProd ? 5 : 10,
      evaluationPeriods: 2,
      alarmDescription: `${name} Lambda function error rate is too high`,
    }).addAlarmAction(new sns.SnsAction(topic));

    // Duration Alarm
    new cloudwatch.Alarm(this, `${name}LambdaDurationAlarm`, {
      metric: fn.metricDuration({
        period: cdk.Duration.minutes(5),
      }),
      threshold: name === 'API' ? 10000 : 300000, // 10s for API, 5min for Worker
      evaluationPeriods: 3,
      alarmDescription: `${name} Lambda function duration is too high`,
    }).addAlarmAction(new sns.SnsAction(topic));

    // Throttle Alarm
    new cloudwatch.Alarm(this, `${name}LambdaThrottleAlarm`, {
      metric: fn.metricThrottles({
        period: cdk.Duration.minutes(5),
      }),
      threshold: 1,
      evaluationPeriods: 1,
      alarmDescription: `${name} Lambda function is being throttled`,
    }).addAlarmAction(new sns.SnsAction(topic));
  }

  private createApiGatewayAlarms(topic: sns.Topic, isProd: boolean): void {
    // 4XX Error Rate
    new cloudwatch.Alarm(this, 'ApiGateway4XXAlarm', {
      metric: this.api.metricClientError({
        period: cdk.Duration.minutes(5),
      }),
      threshold: isProd ? 50 : 20,
      evaluationPeriods: 3,
      alarmDescription: 'API Gateway 4XX error rate is too high',
    }).addAlarmAction(new sns.SnsAction(topic));

    // 5XX Error Rate
    new cloudwatch.Alarm(this, 'ApiGateway5XXAlarm', {
      metric: this.api.metricServerError({
        period: cdk.Duration.minutes(5),
      }),
      threshold: isProd ? 10 : 5,
      evaluationPeriods: 2,
      alarmDescription: 'API Gateway 5XX error rate is too high',
    }).addAlarmAction(new sns.SnsAction(topic));

    // Latency Alarm
    new cloudwatch.Alarm(this, 'ApiGatewayLatencyAlarm', {
      metric: this.api.metricLatency({
        period: cdk.Duration.minutes(5),
      }),
      threshold: 5000, // 5 seconds
      evaluationPeriods: 3,
      alarmDescription: 'API Gateway latency is too high',
    }).addAlarmAction(new sns.SnsAction(topic));
  }

  private createDatabaseAlarms(topic: sns.Topic, isProd: boolean): void {
    // CPU Utilization
    new cloudwatch.Alarm(this, 'DatabaseCPUAlarm', {
      metric: this.database.metricCPUUtilization({
        period: cdk.Duration.minutes(5),
      }),
      threshold: isProd ? 80 : 90,
      evaluationPeriods: 3,
      alarmDescription: 'Database CPU utilization is too high',
    }).addAlarmAction(new sns.SnsAction(topic));

    // Database Connections
    new cloudwatch.Alarm(this, 'DatabaseConnectionsAlarm', {
      metric: this.database.metricDatabaseConnections({
        period: cdk.Duration.minutes(5),
      }),
      threshold: isProd ? 80 : 40,
      evaluationPeriods: 2,
      alarmDescription: 'Database connection count is too high',
    }).addAlarmAction(new sns.SnsAction(topic));
  }

  private createSqsAlarms(queue: sqs.Queue, dlq: sqs.Queue, topic: sns.Topic, isProd: boolean): void {
    // Queue Depth
    new cloudwatch.Alarm(this, 'SQSQueueDepthAlarm', {
      metric: queue.metricApproximateNumberOfVisibleMessages({
        period: cdk.Duration.minutes(5),
      }),
      threshold: isProd ? 1000 : 100,
      evaluationPeriods: 2,
      alarmDescription: 'SQS queue depth is too high',
    }).addAlarmAction(new sns.SnsAction(topic));

    // DLQ Messages
    new cloudwatch.Alarm(this, 'SQSDLQAlarm', {
      metric: dlq.metricApproximateNumberOfVisibleMessages({
        period: cdk.Duration.minutes(1),
      }),
      threshold: 1,
      evaluationPeriods: 1,
      alarmDescription: 'Messages found in Dead Letter Queue',
    }).addAlarmAction(new sns.SnsAction(topic));
  }
}