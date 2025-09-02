#!/bin/bash

set -e

# Infrastructure Validation Script
# Usage: ./validate-infrastructure.sh <environment>

ENVIRONMENT=$1

if [ -z "$ENVIRONMENT" ]; then
    echo "❌ Usage: $0 <environment>"
    echo "   environment: development | staging | production"
    exit 1
fi

echo "🏗️ Validating infrastructure for $ENVIRONMENT environment..."

# AWS Configuration
AWS_REGION=${AWS_REGION:-us-east-1}
AWS_ACCOUNT_ID=${AWS_ACCOUNT_ID:-$(aws sts get-caller-identity --query Account --output text)}

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

CHECKS_PASSED=0
CHECKS_FAILED=0
CHECKS_TOTAL=0

# Helper function
check_resource() {
    local check_name="$1"
    local check_command="$2"
    
    CHECKS_TOTAL=$((CHECKS_TOTAL + 1))
    echo -n "🔍 $check_name... "
    
    if eval "$check_command" >/dev/null 2>&1; then
        echo -e "${GREEN}✅ OK${NC}"
        CHECKS_PASSED=$((CHECKS_PASSED + 1))
    else
        echo -e "${RED}❌ FAIL${NC}"
        CHECKS_FAILED=$((CHECKS_FAILED + 1))
    fi
}

echo ""
echo "📋 Infrastructure Validation Report - $(date)"
echo "=============================================="

# Lambda Functions
check_resource "API Lambda Function" \
    "aws lambda get-function --function-name nfe-api-$ENVIRONMENT"

check_resource "Worker Lambda Function" \
    "aws lambda get-function --function-name nfe-worker-$ENVIRONMENT"

# API Gateway
check_resource "API Gateway" \
    "aws apigateway get-rest-apis --query \"items[?name=='nfe-api-$ENVIRONMENT'].id\" --output text | grep -v None"

# ECR Repositories
check_resource "ECR Repository (API)" \
    "aws ecr describe-repositories --repository-names nfe-api"

check_resource "ECR Repository (Worker)" \
    "aws ecr describe-repositories --repository-names nfe-worker"

# IAM Roles
check_resource "API Lambda Role" \
    "aws iam get-role --role-name NFeLambdaAPIRole-$ENVIRONMENT"

check_resource "Worker Lambda Role" \
    "aws iam get-role --role-name NFeLambdaWorkerRole-$ENVIRONMENT"

# SSM Parameters
check_resource "Database Connection String" \
    "aws ssm get-parameter --name /nfe/$ENVIRONMENT/database/connection-string"

# S3 Bucket (if using for frontend)
if [ "$ENVIRONMENT" != "production" ]; then
    check_resource "Frontend S3 Bucket" \
        "aws s3api head-bucket --bucket nfe-frontend-$ENVIRONMENT"
fi

# CloudWatch Log Groups
check_resource "API Log Group" \
    "aws logs describe-log-groups --log-group-name-prefix /aws/lambda/nfe-api-$ENVIRONMENT"

check_resource "Worker Log Group" \
    "aws logs describe-log-groups --log-group-name-prefix /aws/lambda/nfe-worker-$ENVIRONMENT"

echo ""
echo "📊 Validation Summary"
echo "===================="
echo "Total Checks: $CHECKS_TOTAL"
echo -e "Passed: ${GREEN}$CHECKS_PASSED${NC}"
echo -e "Failed: ${RED}$CHECKS_FAILED${NC}"

if [ "$CHECKS_FAILED" -eq 0 ]; then
    echo -e "\n🎉 ${GREEN}All infrastructure components validated successfully!${NC}"
    exit 0
else
    echo -e "\n💥 ${RED}Infrastructure validation failed!${NC}"
    exit 1
fi