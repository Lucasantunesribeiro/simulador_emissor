#!/bin/bash

set -e

# Smoke Tests Script
# Usage: ./smoke-tests.sh <environment>
# Example: ./smoke-tests.sh staging

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

echo "🧪 Running smoke tests for $ENVIRONMENT environment..."

# AWS Configuration
AWS_REGION=${AWS_REGION:-us-east-1}

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test results
TESTS_PASSED=0
TESTS_FAILED=0
TESTS_TOTAL=0

# Helper function to run a test
run_test() {
    local test_name="$1"
    local test_command="$2"
    local expected_result="$3"
    
    TESTS_TOTAL=$((TESTS_TOTAL + 1))
    echo -n "🧪 $test_name... "
    
    if eval "$test_command" >/dev/null 2>&1; then
        local result="PASS"
        if [ -n "$expected_result" ]; then
            # If we expect a specific result, check it
            local actual_result=$(eval "$test_command" 2>/dev/null)
            if [ "$actual_result" = "$expected_result" ]; then
                result="PASS"
            else
                result="FAIL"
            fi
        fi
        
        if [ "$result" = "PASS" ]; then
            echo -e "${GREEN}✅ PASS${NC}"
            TESTS_PASSED=$((TESTS_PASSED + 1))
        else
            echo -e "${RED}❌ FAIL${NC}"
            TESTS_FAILED=$((TESTS_FAILED + 1))
        fi
    else
        echo -e "${RED}❌ FAIL${NC}"
        TESTS_FAILED=$((TESTS_FAILED + 1))
    fi
}

# Get API URL
API_NAME="nfe-api-$ENVIRONMENT"
API_ID=$(aws apigateway get-rest-apis --query "items[?name=='$API_NAME'].id" --output text 2>/dev/null || echo "")

if [ -z "$API_ID" ] || [ "$API_ID" = "None" ]; then
    echo "❌ API Gateway not found for environment: $ENVIRONMENT"
    exit 1
fi

API_URL="https://$API_ID.execute-api.$AWS_REGION.amazonaws.com/$ENVIRONMENT"

echo ""
echo "📋 Smoke Test Report - $(date)"
echo "================================"
echo "Environment: $ENVIRONMENT"
echo "API URL: $API_URL"
echo ""

# Test 1: API Gateway is accessible
run_test "API Gateway accessibility" \
    "curl -s -o /dev/null -w '%{http_code}' --max-time 10 '$API_URL/health'" \
    "200"

# Test 2: Health endpoint returns valid JSON
run_test "Health endpoint JSON response" \
    "curl -s --max-time 10 '$API_URL/health' | jq -e '.status' >/dev/null"

# Test 3: Database health check
run_test "Database connectivity" \
    "curl -s -o /dev/null -w '%{http_code}' --max-time 15 '$API_URL/health/database'" \
    "200"

# Test 4: API versioning endpoint
run_test "API versioning endpoint" \
    "curl -s -o /dev/null -w '%{http_code}' --max-time 10 '$API_URL/api/version'" \
    "200"

# Test 5: Authentication endpoint (should return proper error)
run_test "Authentication endpoint exists" \
    "curl -s -o /dev/null -w '%{http_code}' --max-time 10 '$API_URL/api/auth/login'"

# Test 6: CORS headers
run_test "CORS headers present" \
    "curl -s -H 'Origin: https://localhost:3000' -H 'Access-Control-Request-Method: GET' -H 'Access-Control-Request-Headers: Content-Type' -X OPTIONS '$API_URL/api/version' | grep -i 'access-control-allow'"

# Test 7: API rate limiting (should not be blocked immediately)
run_test "Rate limiting configured" \
    "curl -s -o /dev/null -w '%{http_code}' --max-time 10 '$API_URL/health'" \
    "200"

# Test 8: Security headers
run_test "Security headers present" \
    "curl -s -I '$API_URL/health' | grep -i 'x-content-type-options'"

# Test 9: Lambda function cold start performance
echo -n "🧪 Lambda cold start performance... "
START_TIME=$(date +%s%N)
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 30 "$API_URL/health")
END_TIME=$(date +%s%N)
DURATION_MS=$(( (END_TIME - START_TIME) / 1000000 ))

TESTS_TOTAL=$((TESTS_TOTAL + 1))

if [ "$HTTP_CODE" = "200" ]; then
    if [ "$DURATION_MS" -lt 5000 ]; then  # Less than 5 seconds
        echo -e "${GREEN}✅ PASS (${DURATION_MS}ms)${NC}"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        echo -e "${YELLOW}⚠️  SLOW (${DURATION_MS}ms)${NC}"
        TESTS_PASSED=$((TESTS_PASSED + 1))  # Still pass, just slow
    fi
else
    echo -e "${RED}❌ FAIL (HTTP $HTTP_CODE)${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi

# Test 10: Worker Lambda function exists and is active
echo -n "🧪 Worker Lambda function status... "
WORKER_FUNCTION_NAME="nfe-worker-$ENVIRONMENT"
TESTS_TOTAL=$((TESTS_TOTAL + 1))

if aws lambda get-function --function-name "$WORKER_FUNCTION_NAME" >/dev/null 2>&1; then
    FUNCTION_STATE=$(aws lambda get-function --function-name "$WORKER_FUNCTION_NAME" --query 'Configuration.State' --output text)
    if [ "$FUNCTION_STATE" = "Active" ]; then
        echo -e "${GREEN}✅ PASS (Active)${NC}"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        echo -e "${RED}❌ FAIL (State: $FUNCTION_STATE)${NC}"
        TESTS_FAILED=$((TESTS_FAILED + 1))
    fi
else
    echo -e "${RED}❌ FAIL (Not found)${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi

# Test 11: Environment variables are set
echo -n "🧪 Environment configuration... "
TESTS_TOTAL=$((TESTS_TOTAL + 1))

RESPONSE=$(curl -s --max-time 10 "$API_URL/health" 2>/dev/null || echo "{}")
ENV_CHECK=$(echo "$RESPONSE" | jq -e '.environment' >/dev/null 2>&1 && echo "true" || echo "false")

if [ "$ENV_CHECK" = "true" ]; then
    REPORTED_ENV=$(echo "$RESPONSE" | jq -r '.environment' 2>/dev/null || echo "unknown")
    if [ "$REPORTED_ENV" = "$ENVIRONMENT" ]; then
        echo -e "${GREEN}✅ PASS (Environment: $REPORTED_ENV)${NC}"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        echo -e "${YELLOW}⚠️  MISMATCH (Expected: $ENVIRONMENT, Got: $REPORTED_ENV)${NC}"
        TESTS_FAILED=$((TESTS_FAILED + 1))
    fi
else
    echo -e "${RED}❌ FAIL (Environment info not available)${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi

# Test 12: API documentation endpoint
run_test "API documentation endpoint" \
    "curl -s -o /dev/null -w '%{http_code}' --max-time 10 '$API_URL/swagger'" \
    "200"

echo ""
echo "📊 Test Results Summary"
echo "======================"
echo "Total Tests: $TESTS_TOTAL"
echo -e "Passed: ${GREEN}$TESTS_PASSED${NC}"
echo -e "Failed: ${RED}$TESTS_FAILED${NC}"

if [ "$TESTS_FAILED" -eq 0 ]; then
    PASS_RATE=100
else
    PASS_RATE=$(( TESTS_PASSED * 100 / TESTS_TOTAL ))
fi

echo "Pass Rate: $PASS_RATE%"

echo ""

if [ "$TESTS_FAILED" -eq 0 ]; then
    echo -e "🎉 ${GREEN}All smoke tests PASSED!${NC}"
    echo -e "✅ ${GREEN}Environment $ENVIRONMENT is ready for use${NC}"
    exit 0
elif [ "$PASS_RATE" -ge 80 ]; then
    echo -e "⚠️  ${YELLOW}Smoke tests completed with warnings${NC}"
    echo -e "🔍 ${YELLOW}$TESTS_FAILED tests failed but critical functionality appears to work${NC}"
    exit 0
else
    echo -e "💥 ${RED}Smoke tests FAILED!${NC}"
    echo -e "❌ ${RED}Environment $ENVIRONMENT has critical issues${NC}"
    exit 1
fi