#!/bin/bash

set -e

# Health Check Script
# Usage: ./health-check.sh <environment>
# Example: ./health-check.sh development

ENVIRONMENT=$1

if [ -z "$ENVIRONMENT" ]; then
    echo "❌ Usage: $0 <environment>"
    echo "   environment: development | staging | production"
    exit 1
fi

# Validate environment
if [ "$ENVIRONMENT" != "development" ] && [ "$ENVIRONMENT" != "staging" ] && [ "$ENVIRONMENT" != "production" ]; then
    echo "❌ Invalid environment: $ENVIRONMENT"
    exit 1
fi

echo "🏥 Running health checks for $ENVIRONMENT environment..."

# AWS Configuration
AWS_REGION=${AWS_REGION:-us-east-1}
API_FUNCTION_NAME="nfe-api-$ENVIRONMENT"
WORKER_FUNCTION_NAME="nfe-worker-$ENVIRONMENT"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Health check results
API_STATUS="UNKNOWN"
WORKER_STATUS="UNKNOWN"
DB_STATUS="UNKNOWN"
API_URL=""

echo ""
echo "📋 Health Check Report - $(date)"
echo "================================"

# 1. Check API Lambda Function
echo -n "🔍 API Lambda Function... "
if aws lambda get-function --function-name "$API_FUNCTION_NAME" >/dev/null 2>&1; then
    FUNCTION_STATE=$(aws lambda get-function --function-name "$API_FUNCTION_NAME" --query 'Configuration.State' --output text)
    LAST_UPDATE_STATUS=$(aws lambda get-function --function-name "$API_FUNCTION_NAME" --query 'Configuration.LastUpdateStatus' --output text)
    
    if [ "$FUNCTION_STATE" = "Active" ] && [ "$LAST_UPDATE_STATUS" = "Successful" ]; then
        echo -e "${GREEN}✅ Active${NC}"
        API_STATUS="HEALTHY"
    else
        echo -e "${RED}❌ Issues detected (State: $FUNCTION_STATE, Update: $LAST_UPDATE_STATUS)${NC}"
        API_STATUS="UNHEALTHY"
    fi
else
    echo -e "${RED}❌ Not found${NC}"
    API_STATUS="NOT_FOUND"
fi

# 2. Check Worker Lambda Function
echo -n "🔍 Worker Lambda Function... "
if aws lambda get-function --function-name "$WORKER_FUNCTION_NAME" >/dev/null 2>&1; then
    FUNCTION_STATE=$(aws lambda get-function --function-name "$WORKER_FUNCTION_NAME" --query 'Configuration.State' --output text)
    LAST_UPDATE_STATUS=$(aws lambda get-function --function-name "$WORKER_FUNCTION_NAME" --query 'Configuration.LastUpdateStatus' --output text)
    
    if [ "$FUNCTION_STATE" = "Active" ] && [ "$LAST_UPDATE_STATUS" = "Successful" ]; then
        echo -e "${GREEN}✅ Active${NC}"
        WORKER_STATUS="HEALTHY"
    else
        echo -e "${RED}❌ Issues detected (State: $FUNCTION_STATE, Update: $LAST_UPDATE_STATUS)${NC}"
        WORKER_STATUS="UNHEALTHY"
    fi
else
    echo -e "${RED}❌ Not found${NC}"
    WORKER_STATUS="NOT_FOUND"
fi

# 3. Get API Gateway URL
echo -n "🔍 API Gateway... "
API_NAME="nfe-api-$ENVIRONMENT"
API_ID=$(aws apigateway get-rest-apis --query "items[?name=='$API_NAME'].id" --output text)

if [ "$API_ID" != "None" ] && [ -n "$API_ID" ]; then
    API_URL="https://$API_ID.execute-api.$AWS_REGION.amazonaws.com/$ENVIRONMENT"
    echo -e "${GREEN}✅ Found${NC}"
    echo "   URL: $API_URL"
else
    echo -e "${RED}❌ Not found${NC}"
    API_URL=""
fi

# 4. Test API Health Endpoint
if [ -n "$API_URL" ] && [ "$API_STATUS" = "HEALTHY" ]; then
    echo -n "🔍 API Health Endpoint... "
    
    # Test health endpoint with timeout
    if HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$API_URL/health" 2>/dev/null); then
        if [ "$HTTP_CODE" = "200" ]; then
            echo -e "${GREEN}✅ Healthy (HTTP $HTTP_CODE)${NC}"
            
            # Get detailed response
            RESPONSE=$(curl -s --max-time 5 "$API_URL/health" 2>/dev/null || echo "{}")
            echo "   Response: $RESPONSE"
        else
            echo -e "${YELLOW}⚠️  Response received (HTTP $HTTP_CODE)${NC}"
        fi
    else
        echo -e "${RED}❌ No response (timeout or error)${NC}"
    fi
fi

# 5. Test Database Connection
echo -n "🔍 Database Connection... "
if [ "$API_STATUS" = "HEALTHY" ] && [ -n "$API_URL" ]; then
    if HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 15 "$API_URL/health/database" 2>/dev/null); then
        if [ "$HTTP_CODE" = "200" ]; then
            echo -e "${GREEN}✅ Connected${NC}"
            DB_STATUS="HEALTHY"
        elif [ "$HTTP_CODE" = "503" ]; then
            echo -e "${RED}❌ Connection failed${NC}"
            DB_STATUS="UNHEALTHY"
        else
            echo -e "${YELLOW}⚠️  Unknown status (HTTP $HTTP_CODE)${NC}"
            DB_STATUS="UNKNOWN"
        fi
    else
        echo -e "${RED}❌ No response${NC}"
        DB_STATUS="UNREACHABLE"
    fi
else
    echo -e "${YELLOW}⚠️  Skipped (API not available)${NC}"
    DB_STATUS="SKIPPED"
fi

# 6. Check CloudWatch Logs for errors
echo -n "🔍 Recent Errors... "
LOG_GROUP_API="/aws/lambda/$API_FUNCTION_NAME"
LOG_GROUP_WORKER="/aws/lambda/$WORKER_FUNCTION_NAME"

ERROR_COUNT=0

# Check API errors in last 5 minutes
if aws logs describe-log-groups --log-group-name-prefix "$LOG_GROUP_API" >/dev/null 2>&1; then
    API_ERRORS=$(aws logs filter-log-events \
        --log-group-name "$LOG_GROUP_API" \
        --start-time $(($(date +%s) * 1000 - 300000)) \
        --filter-pattern "ERROR" \
        --query 'events' \
        --output text 2>/dev/null | wc -l || echo "0")
    ERROR_COUNT=$((ERROR_COUNT + API_ERRORS))
fi

# Check Worker errors in last 5 minutes
if aws logs describe-log-groups --log-group-name-prefix "$LOG_GROUP_WORKER" >/dev/null 2>&1; then
    WORKER_ERRORS=$(aws logs filter-log-events \
        --log-group-name "$LOG_GROUP_WORKER" \
        --start-time $(($(date +%s) * 1000 - 300000)) \
        --filter-pattern "ERROR" \
        --query 'events' \
        --output text 2>/dev/null | wc -l || echo "0")
    ERROR_COUNT=$((ERROR_COUNT + WORKER_ERRORS))
fi

if [ "$ERROR_COUNT" -eq 0 ]; then
    echo -e "${GREEN}✅ No errors (last 5 min)${NC}"
else
    echo -e "${YELLOW}⚠️  $ERROR_COUNT errors found (last 5 min)${NC}"
fi

# Summary
echo ""
echo "📊 Summary"
echo "=========="
echo -n "API Service: "
case $API_STATUS in
    "HEALTHY") echo -e "${GREEN}✅ Healthy${NC}" ;;
    "UNHEALTHY") echo -e "${RED}❌ Unhealthy${NC}" ;;
    *) echo -e "${YELLOW}⚠️  $API_STATUS${NC}" ;;
esac

echo -n "Worker Service: "
case $WORKER_STATUS in
    "HEALTHY") echo -e "${GREEN}✅ Healthy${NC}" ;;
    "UNHEALTHY") echo -e "${RED}❌ Unhealthy${NC}" ;;
    *) echo -e "${YELLOW}⚠️  $WORKER_STATUS${NC}" ;;
esac

echo -n "Database: "
case $DB_STATUS in
    "HEALTHY") echo -e "${GREEN}✅ Healthy${NC}" ;;
    "UNHEALTHY") echo -e "${RED}❌ Unhealthy${NC}" ;;
    *) echo -e "${YELLOW}⚠️  $DB_STATUS${NC}" ;;
esac

# Overall status
if [ "$API_STATUS" = "HEALTHY" ] && [ "$WORKER_STATUS" = "HEALTHY" ] && [ "$DB_STATUS" = "HEALTHY" ]; then
    echo -e "\n🎉 ${GREEN}Overall Status: HEALTHY${NC}"
    exit 0
else
    echo -e "\n💥 ${RED}Overall Status: ISSUES DETECTED${NC}"
    exit 1
fi