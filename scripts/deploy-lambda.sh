#!/bin/bash

set -e

# Deploy Lambda Function Script
# Usage: ./deploy-lambda.sh <service> <environment> <image_tag>
# Example: ./deploy-lambda.sh api development abc123

SERVICE=$1
ENVIRONMENT=$2
IMAGE_TAG=$3

if [ -z "$SERVICE" ] || [ -z "$ENVIRONMENT" ] || [ -z "$IMAGE_TAG" ]; then
    echo "❌ Usage: $0 <service> <environment> <image_tag>"
    echo "   service: api | worker"
    echo "   environment: development | staging | production"
    echo "   image_tag: Docker image tag"
    exit 1
fi

# Validate service
if [ "$SERVICE" != "api" ] && [ "$SERVICE" != "worker" ]; then
    echo "❌ Invalid service: $SERVICE. Must be 'api' or 'worker'"
    exit 1
fi

# Validate environment
if [ "$ENVIRONMENT" != "development" ] && [ "$ENVIRONMENT" != "staging" ] && [ "$ENVIRONMENT" != "production" ]; then
    echo "❌ Invalid environment: $ENVIRONMENT"
    exit 1
fi

echo "🚀 Deploying NFe $SERVICE to $ENVIRONMENT..."

# AWS Configuration
AWS_ACCOUNT_ID=${AWS_ACCOUNT_ID:-$(aws sts get-caller-identity --query Account --output text)}
AWS_REGION=${AWS_REGION:-us-east-1}
ECR_REGISTRY="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"
ECR_REPOSITORY="nfe-$SERVICE"
FUNCTION_NAME="nfe-$SERVICE-$ENVIRONMENT"

# Full image URI
IMAGE_URI="$ECR_REGISTRY/$ECR_REPOSITORY:$IMAGE_TAG"

echo "📋 Deployment Details:"
echo "   Service: $SERVICE"
echo "   Environment: $ENVIRONMENT"
echo "   Function: $FUNCTION_NAME"
echo "   Image: $IMAGE_URI"
echo ""

# Check if Lambda function exists
if aws lambda get-function --function-name "$FUNCTION_NAME" >/dev/null 2>&1; then
    echo "🔄 Updating existing Lambda function..."
    
    # Update function code
    aws lambda update-function-code \
        --function-name "$FUNCTION_NAME" \
        --image-uri "$IMAGE_URI" \
        --no-cli-pager
    
    # Wait for update to complete
    echo "⏳ Waiting for function update to complete..."
    aws lambda wait function-updated \
        --function-name "$FUNCTION_NAME"
    
    echo "✅ Function code updated successfully"
    
else
    echo "🆕 Creating new Lambda function..."
    
    # Set role based on service
    if [ "$SERVICE" = "api" ]; then
        ROLE_NAME="NFeLambdaAPIRole-$ENVIRONMENT"
    else
        ROLE_NAME="NFeLambdaWorkerRole-$ENVIRONMENT"
    fi
    
    ROLE_ARN="arn:aws:iam::$AWS_ACCOUNT_ID:role/$ROLE_NAME"
    
    # Create function
    aws lambda create-function \
        --function-name "$FUNCTION_NAME" \
        --role "$ROLE_ARN" \
        --code ImageUri="$IMAGE_URI" \
        --package-type Image \
        --timeout 30 \
        --memory-size 512 \
        --environment Variables="{
            ASPNETCORE_ENVIRONMENT=$ENVIRONMENT,
            AWS_REGION=$AWS_REGION,
            ConnectionStrings__DefaultConnection=$(aws ssm get-parameter --name "/nfe/$ENVIRONMENT/database/connection-string" --with-decryption --query 'Parameter.Value' --output text 2>/dev/null || echo '')
        }" \
        --no-cli-pager
    
    echo "✅ Lambda function created successfully"
fi

# Update function configuration if needed
echo "🔧 Updating function configuration..."

# Set timeout and memory based on service
if [ "$SERVICE" = "api" ]; then
    TIMEOUT=30
    MEMORY=512
else
    TIMEOUT=300  # 5 minutes for worker
    MEMORY=1024
fi

aws lambda update-function-configuration \
    --function-name "$FUNCTION_NAME" \
    --timeout "$TIMEOUT" \
    --memory-size "$MEMORY" \
    --environment Variables="{
        ASPNETCORE_ENVIRONMENT=$ENVIRONMENT,
        AWS_REGION=$AWS_REGION,
        ConnectionStrings__DefaultConnection=$(aws ssm get-parameter --name "/nfe/$ENVIRONMENT/database/connection-string" --with-decryption --query 'Parameter.Value' --output text 2>/dev/null || echo '')
    }" \
    --no-cli-pager >/dev/null

echo "✅ Function configuration updated"

# Create/update API Gateway integration for API service
if [ "$SERVICE" = "api" ]; then
    echo "🔗 Setting up API Gateway integration..."
    
    API_NAME="nfe-api-$ENVIRONMENT"
    
    # Get API Gateway ID
    API_ID=$(aws apigateway get-rest-apis --query "items[?name=='$API_NAME'].id" --output text)
    
    if [ "$API_ID" != "None" ] && [ -n "$API_ID" ]; then
        echo "🔄 API Gateway exists: $API_ID"
        
        # Update deployment
        aws apigateway create-deployment \
            --rest-api-id "$API_ID" \
            --stage-name "$ENVIRONMENT" \
            --description "Deployment for $IMAGE_TAG" \
            --no-cli-pager >/dev/null
        
        echo "✅ API Gateway deployment updated"
        
        # Get API URL
        API_URL="https://$API_ID.execute-api.$AWS_REGION.amazonaws.com/$ENVIRONMENT"
        echo "🌐 API URL: $API_URL"
        
    else
        echo "⚠️  API Gateway not found. Run infrastructure deployment first."
    fi
fi

# Tag the function
aws lambda tag-resource \
    --resource "$FUNCTION_NAME" \
    --tags Environment="$ENVIRONMENT",Service="$SERVICE",ImageTag="$IMAGE_TAG",DeployedAt="$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    --no-cli-pager >/dev/null

echo ""
echo "🎉 Deployment completed successfully!"
echo "   Function: $FUNCTION_NAME"
echo "   Status: $(aws lambda get-function --function-name "$FUNCTION_NAME" --query 'Configuration.State' --output text)"
echo "   Last Modified: $(aws lambda get-function --function-name "$FUNCTION_NAME" --query 'Configuration.LastModified' --output text)"
echo ""