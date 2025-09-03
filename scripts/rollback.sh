#!/bin/bash

set -e

# Rollback Script
# Usage: ./rollback.sh <service> <environment> [version]

SERVICE=$1
ENVIRONMENT=$2
VERSION=$3

if [ -z "$SERVICE" ] || [ -z "$ENVIRONMENT" ]; then
    echo "❌ Usage: $0 <service> <environment> [version]"
    echo "   service: api | worker | frontend | all"
    echo "   environment: development | staging | production"
    echo "   version: specific version to rollback to (optional)"
    exit 1
fi

echo "🔄 Initiating rollback for $SERVICE in $ENVIRONMENT..."

# AWS Configuration
AWS_REGION=${AWS_REGION:-us-east-1}
AWS_ACCOUNT_ID=${AWS_ACCOUNT_ID:-$(aws sts get-caller-identity --query Account --output text)}

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

rollback_lambda() {
    local service=$1
    local function_name="nfe-$service-$ENVIRONMENT"
    
    echo "🔄 Rolling back Lambda function: $function_name"
    
    # Get function versions
    echo "📋 Available versions:"
    aws lambda list-versions-by-function --function-name "$function_name" \
        --query 'Versions[?Version!=`$LATEST`].[Version,LastModified,Description]' \
        --output table
    
    if [ -n "$VERSION" ]; then
        TARGET_VERSION=$VERSION
    else
        # Get previous version (second latest)
        TARGET_VERSION=$(aws lambda list-versions-by-function --function-name "$function_name" \
            --query 'Versions[?Version!=`$LATEST`].Version' \
            --output text | tr '\t' '\n' | sort -n | tail -2 | head -1)
    fi
    
    if [ -z "$TARGET_VERSION" ] || [ "$TARGET_VERSION" = "None" ]; then
        echo -e "${RED}❌ No previous version found to rollback to${NC}"
        return 1
    fi
    
    echo "🎯 Rolling back to version: $TARGET_VERSION"
    
    # Get the image URI from the target version
    IMAGE_URI=$(aws lambda get-function --function-name "$function_name" --qualifier "$TARGET_VERSION" \
        --query 'Code.ImageUri' --output text)
    
    if [ -n "$IMAGE_URI" ] && [ "$IMAGE_URI" != "None" ]; then
        # Update function code to previous version
        aws lambda update-function-code \
            --function-name "$function_name" \
            --image-uri "$IMAGE_URI" \
            --no-cli-pager
        
        # Wait for update to complete
        echo "⏳ Waiting for rollback to complete..."
        aws lambda wait function-updated --function-name "$function_name"
        
        echo -e "${GREEN}✅ Rollback completed for $function_name${NC}"
    else
        echo -e "${RED}❌ Could not retrieve image URI for version $TARGET_VERSION${NC}"
        return 1
    fi
}

rollback_frontend() {
    echo "🔄 Rolling back frontend deployment..."
    
    local bucket_name="nfe-frontend-$ENVIRONMENT"
    local backup_prefix="backups/$(date +%Y/%m/%d)"
    
    # List available backups
    echo "📋 Available frontend backups:"
    aws s3 ls "s3://$bucket_name/$backup_prefix/" --recursive || {
        echo -e "${RED}❌ No backups found for today${NC}"
        return 1
    }
    
    if [ -n "$VERSION" ]; then
        BACKUP_PATH="$backup_prefix/$VERSION"
    else
        # Get latest backup
        BACKUP_PATH=$(aws s3 ls "s3://$bucket_name/$backup_prefix/" | tail -1 | awk '{print $4}')
    fi
    
    if [ -z "$BACKUP_PATH" ]; then
        echo -e "${RED}❌ No backup found to rollback to${NC}"
        return 1
    fi
    
    echo "🎯 Rolling back to: $BACKUP_PATH"
    
    # Create temp directory for backup
    TEMP_DIR=$(mktemp -d)
    
    # Download backup
    aws s3 sync "s3://$bucket_name/$BACKUP_PATH" "$TEMP_DIR/"
    
    # Deploy backup to live site
    aws s3 sync "$TEMP_DIR/" "s3://$bucket_name/" \
        --delete \
        --cache-control max-age=86400
    
    # Invalidate CloudFront
    DISTRIBUTION_ID_VAR="CLOUDFRONT_DISTRIBUTION_$(echo $ENVIRONMENT | tr '[:lower:]' '[:upper:]')"
    DISTRIBUTION_ID=${!DISTRIBUTION_ID_VAR}
    
    if [ -n "$DISTRIBUTION_ID" ]; then
        aws cloudfront create-invalidation \
            --distribution-id "$DISTRIBUTION_ID" \
            --paths "/*"
    fi
    
    # Cleanup
    rm -rf "$TEMP_DIR"
    
    echo -e "${GREEN}✅ Frontend rollback completed${NC}"
}

rollback_all() {
    echo "🔄 Rolling back all services..."
    
    # Rollback in reverse order (frontend first, then worker, then api)
    rollback_frontend || echo -e "${YELLOW}⚠️ Frontend rollback failed or skipped${NC}"
    rollback_lambda "worker" || echo -e "${YELLOW}⚠️ Worker rollback failed${NC}"
    rollback_lambda "api" || echo -e "${YELLOW}⚠️ API rollback failed${NC}"
    
    echo -e "${GREEN}✅ All services rollback attempted${NC}"
}

# Main rollback logic
case $SERVICE in
    "api")
        rollback_lambda "api"
        ;;
    "worker")
        rollback_lambda "worker"
        ;;
    "frontend")
        rollback_frontend
        ;;
    "all")
        rollback_all
        ;;
    *)
        echo -e "${RED}❌ Invalid service: $SERVICE${NC}"
        echo "Valid services: api, worker, frontend, all"
        exit 1
        ;;
esac

# Run health check after rollback
if command -v ./scripts/health-check.sh >/dev/null 2>&1; then
    echo ""
    echo "🏥 Running post-rollback health check..."
    ./scripts/health-check.sh "$ENVIRONMENT" || {
        echo -e "${RED}❌ Health check failed after rollback${NC}"
        echo -e "${YELLOW}⚠️ Manual intervention may be required${NC}"
        exit 1
    }
else
    echo -e "${YELLOW}⚠️ Health check script not found, skipping validation${NC}"
fi

echo ""
echo -e "🎉 ${GREEN}Rollback completed successfully!${NC}"
echo "📋 Summary:"
echo "   Service: $SERVICE"
echo "   Environment: $ENVIRONMENT"
echo "   Version: ${VERSION:-'Previous'}"
echo "   Time: $(date)"