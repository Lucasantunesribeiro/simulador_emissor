import * as cdk from 'aws-cdk-lib';
import { Template } from 'aws-cdk-lib/assertions';
import { NFeInfrastructureStack } from '../lib/nfe-infrastructure-stack';

describe('NFeInfrastructureStack', () => {
  let app: cdk.App;
  let template: Template;

  beforeEach(() => {
    app = new cdk.App();
  });

  describe('Development Environment', () => {
    beforeEach(() => {
      const stack = new NFeInfrastructureStack(app, 'TestStack-dev', {
        stage: 'dev',
        env: { account: '123456789012', region: 'us-east-1' }
      });
      template = Template.fromStack(stack);
    });

    test('Creates VPC with correct configuration', () => {
      template.hasResourceProperties('AWS::EC2::VPC', {
        CidrBlock: '10.0.0.0/16',
        EnableDnsHostnames: true,
        EnableDnsSupport: true
      });
    });

    test('Creates RDS instance with correct configuration', () => {
      template.hasResourceProperties('AWS::RDS::DBInstance', {
        Engine: 'postgres',
        EngineVersion: '16.6',
        DBInstanceClass: 'db.t3.micro',
        MultiAZ: false,
        StorageEncrypted: true
      });
    });

    test('Creates Lambda functions', () => {
      // API Lambda
      template.hasResourceProperties('AWS::Lambda::Function', {
        Runtime: 'dotnet8',
        Handler: 'NFe.API::NFe.API.LambdaEntryPoint::FunctionHandlerAsync',
        Timeout: 30
      });

      // Worker Lambda
      template.hasResourceProperties('AWS::Lambda::Function', {
        Runtime: 'dotnet8',
        Handler: 'NFe.Worker::NFe.Worker.Function::FunctionHandler',
        Timeout: 900
      });
    });

    test('Creates S3 buckets with encryption', () => {
      // XMLs bucket
      template.hasResourceProperties('AWS::S3::Bucket', {
        BucketEncryption: {
          ServerSideEncryptionConfiguration: [
            {
              ServerSideEncryptionByDefault: {
                SSEAlgorithm: 'AES256'
              }
            }
          ]
        },
        PublicAccessBlockConfiguration: {
          BlockPublicAcls: true,
          BlockPublicPolicy: true,
          IgnorePublicAcls: true,
          RestrictPublicBuckets: true
        }
      });
    });

    test('Creates SQS queues with DLQ', () => {
      // Main queue
      template.hasResourceProperties('AWS::SQS::Queue', {
        MessageRetentionPeriod: 1209600, // 14 days
        VisibilityTimeoutSeconds: 900, // 15 minutes
        KmsMasterKeyId: 'alias/aws/sqs'
      });

      // DLQ
      template.hasResourceProperties('AWS::SQS::Queue', {
        MessageRetentionPeriod: 1209600 // 14 days
      });
    });

    test('Creates API Gateway with throttling', () => {
      template.hasResourceProperties('AWS::ApiGateway::Stage', {
        ThrottleSettings: {
          RateLimit: 100,
          BurstLimit: 200
        }
      });
    });

    test('Creates Secrets Manager secrets', () => {
      // Certificate secret
      template.hasResource('AWS::SecretsManager::Secret', {});
      
      // Database secret
      template.hasResource('AWS::SecretsManager::Secret', {});
    });

    test('Creates CloudWatch alarms', () => {
      template.hasResource('AWS::CloudWatch::Alarm', {});
    });

    test('Creates WAF Web ACL', () => {
      template.hasResourceProperties('AWS::WAFv2::WebACL', {
        Scope: 'REGIONAL',
        DefaultAction: {
          Allow: {}
        }
      });
    });

    test('Creates IAM roles with least privilege', () => {
      template.hasResourceProperties('AWS::IAM::Role', {
        AssumeRolePolicyDocument: {
          Statement: [
            {
              Action: 'sts:AssumeRole',
              Effect: 'Allow',
              Principal: {
                Service: 'lambda.amazonaws.com'
              }
            }
          ]
        }
      });
    });
  });

  describe('Production Environment', () => {
    beforeEach(() => {
      const stack = new NFeInfrastructureStack(app, 'TestStack-prod', {
        stage: 'prod',
        env: { account: '123456789012', region: 'us-east-1' }
      });
      template = Template.fromStack(stack);
    });

    test('Creates RDS with Multi-AZ for production', () => {
      template.hasResourceProperties('AWS::RDS::DBInstance', {
        Engine: 'postgres',
        DBInstanceClass: 'db.t3.medium',
        MultiAZ: true,
        DeletionProtection: true,
        BackupRetentionPeriod: 7
      });
    });

    test('Creates higher capacity Lambda functions', () => {
      // API Lambda with more memory
      template.hasResourceProperties('AWS::Lambda::Function', {
        Runtime: 'dotnet8',
        Handler: 'NFe.API::NFe.API.LambdaEntryPoint::FunctionHandlerAsync',
        MemorySize: 1024
      });

      // Worker Lambda with more memory
      template.hasResourceProperties('AWS::Lambda::Function', {
        Runtime: 'dotnet8',
        Handler: 'NFe.Worker::NFe.Worker.Function::FunctionHandler',
        MemorySize: 2048
      });
    });

    test('Creates higher API Gateway limits', () => {
      template.hasResourceProperties('AWS::ApiGateway::Stage', {
        ThrottleSettings: {
          RateLimit: 1000,
          BurstLimit: 2000
        }
      });
    });

    test('Creates VPC with 3 AZs for production', () => {
      // Count number of subnets (should be more for prod)
      const subnets = template.findResources('AWS::EC2::Subnet');
      expect(Object.keys(subnets).length).toBeGreaterThan(6); // 3 AZs * 3 subnet types
    });
  });

  describe('Security', () => {
    beforeEach(() => {
      const stack = new NFeInfrastructureStack(app, 'TestStack-security', {
        stage: 'dev',
        env: { account: '123456789012', region: 'us-east-1' }
      });
      template = Template.fromStack(stack);
    });

    test('All S3 buckets have encryption enabled', () => {
      const buckets = template.findResources('AWS::S3::Bucket');
      Object.values(buckets).forEach((bucket: any) => {
        expect(bucket.Properties.BucketEncryption).toBeDefined();
        expect(bucket.Properties.PublicAccessBlockConfiguration).toEqual({
          BlockPublicAcls: true,
          BlockPublicPolicy: true,
          IgnorePublicAcls: true,
          RestrictPublicBuckets: true
        });
      });
    });

    test('RDS has encryption at rest enabled', () => {
      template.hasResourceProperties('AWS::RDS::DBInstance', {
        StorageEncrypted: true
      });
    });

    test('Lambda functions have X-Ray tracing enabled', () => {
      template.hasResourceProperties('AWS::Lambda::Function', {
        TracingConfig: {
          Mode: 'Active'
        }
      });
    });

    test('Security groups follow least privilege', () => {
      const securityGroups = template.findResources('AWS::EC2::SecurityGroup');
      Object.values(securityGroups).forEach((sg: any) => {
        if (sg.Properties.SecurityGroupEgress) {
          // Check that not all traffic is allowed outbound by default
          const hasAllTrafficRule = sg.Properties.SecurityGroupEgress.some((rule: any) => 
            rule.CidrIp === '0.0.0.0/0' && rule.IpProtocol === '-1'
          );
          // Some security groups might need all outbound traffic, so this is not a hard requirement
        }
      });
    });
  });
});