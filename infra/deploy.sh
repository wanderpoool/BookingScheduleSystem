#!/bin/bash
set -euo pipefail

# =============================================================================
# BookMeApp - AWS Deployment Script
# =============================================================================
# Prerequisites:
#   1. AWS CLI v2 installed and configured (aws configure)
#   2. Docker installed and running
#   3. Permissions: CloudFormation, ECS, ECR, RDS, ALB, SNS, SES, IAM, VPC
#
# Usage:
#   chmod +x deploy.sh
#   ./deploy.sh                    # Full deploy (infra + containers)
#   ./deploy.sh --infra-only       # Deploy infrastructure only
#   ./deploy.sh --app-only         # Build and push containers only
# =============================================================================

APP_NAME="bookmeapp"
ENVIRONMENT="production"
AWS_REGION="ap-southeast-1"
STACK_NAME="${APP_NAME}-${ENVIRONMENT}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log() { echo -e "${GREEN}[DEPLOY]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# Get AWS account ID
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text 2>/dev/null) || error "AWS CLI not configured. Run 'aws configure' first."
ECR_BASE="${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"

deploy_infra() {
    log "Deploying CloudFormation stack: ${STACK_NAME}..."

    # First deploy with placeholder images (ECR repos need to exist first)
    # Use a simple placeholder - actual images will be pushed after
    local API_IMAGE="${ECR_BASE}/${APP_NAME}-api:latest"
    local WEB_IMAGE="${ECR_BASE}/${APP_NAME}-web:latest"

    aws cloudformation deploy \
        --template-file main.yaml \
        --stack-name "${STACK_NAME}" \
        --parameter-overrides \
            Environment="${ENVIRONMENT}" \
            AppName="${APP_NAME}" \
            ApiImageUri="${API_IMAGE}" \
            WebImageUri="${WEB_IMAGE}" \
        --capabilities CAPABILITY_NAMED_IAM \
        --region "${AWS_REGION}" \
        --no-fail-on-empty-changeset

    log "Infrastructure deployed successfully!"

    # Print outputs
    log "Stack outputs:"
    aws cloudformation describe-stacks \
        --stack-name "${STACK_NAME}" \
        --query 'Stacks[0].Outputs' \
        --output table \
        --region "${AWS_REGION}"
}

build_and_push() {
    log "Logging into ECR..."
    aws ecr get-login-password --region "${AWS_REGION}" | \
        docker login --username AWS --password-stdin "${ECR_BASE}"

    local SRC_DIR="../src"
    local TIMESTAMP=$(date +%Y%m%d%H%M%S)

    # Build and push API
    log "Building API image..."
    docker build \
        -t "${APP_NAME}-api:latest" \
        -f "${SRC_DIR}/BookingScheduleSystem.Api/Dockerfile" \
        "${SRC_DIR}"

    docker tag "${APP_NAME}-api:latest" "${ECR_BASE}/${APP_NAME}-api:latest"
    docker tag "${APP_NAME}-api:latest" "${ECR_BASE}/${APP_NAME}-api:${TIMESTAMP}"

    log "Pushing API image..."
    docker push "${ECR_BASE}/${APP_NAME}-api:latest"
    docker push "${ECR_BASE}/${APP_NAME}-api:${TIMESTAMP}"

    # Build and push Web
    log "Building Web image..."
    docker build \
        -t "${APP_NAME}-web:latest" \
        -f "${SRC_DIR}/BookingScheduleSystem.Web/Dockerfile" \
        "${SRC_DIR}"

    docker tag "${APP_NAME}-web:latest" "${ECR_BASE}/${APP_NAME}-web:latest"
    docker tag "${APP_NAME}-web:latest" "${ECR_BASE}/${APP_NAME}-web:${TIMESTAMP}"

    log "Pushing Web image..."
    docker push "${ECR_BASE}/${APP_NAME}-web:latest"
    docker push "${ECR_BASE}/${APP_NAME}-web:${TIMESTAMP}"

    log "All images pushed successfully!"

    # Force new deployment
    log "Forcing ECS service update..."
    aws ecs update-service \
        --cluster "${APP_NAME}-${ENVIRONMENT}" \
        --service "${APP_NAME}-api" \
        --force-new-deployment \
        --region "${AWS_REGION}" > /dev/null

    aws ecs update-service \
        --cluster "${APP_NAME}-${ENVIRONMENT}" \
        --service "${APP_NAME}-web" \
        --force-new-deployment \
        --region "${AWS_REGION}" > /dev/null

    log "ECS services updated. New tasks rolling out..."
}

verify_ses() {
    local SENDER_EMAIL="noreply@bookmeapp.com"
    log "Checking SES email identity verification..."
    local STATUS=$(aws ses get-identity-verification-attributes \
        --identities "${SENDER_EMAIL}" \
        --query "VerificationAttributes.\"${SENDER_EMAIL}\".VerificationStatus" \
        --output text \
        --region "${AWS_REGION}" 2>/dev/null || echo "NotFound")

    if [ "${STATUS}" != "Success" ]; then
        warn "SES email '${SENDER_EMAIL}' is NOT verified (status: ${STATUS})."
        warn "Check your email inbox and click the verification link from AWS."
        warn "Or verify a domain instead: aws ses verify-domain-identity --domain bookmeapp.com"
    else
        log "SES email '${SENDER_EMAIL}' is verified."
    fi
}

# =============================================================================
# MAIN
# =============================================================================
case "${1:-}" in
    --infra-only)
        deploy_infra
        verify_ses
        ;;
    --app-only)
        build_and_push
        ;;
    *)
        deploy_infra
        build_and_push
        verify_ses

        log "============================================"
        log "Deployment complete!"
        ALB_DNS=$(aws cloudformation describe-stacks \
            --stack-name "${STACK_NAME}" \
            --query 'Stacks[0].Outputs[?OutputKey==`AlbDnsName`].OutputValue' \
            --output text \
            --region "${AWS_REGION}")
        log "App URL: ${ALB_DNS}"
        log "============================================"
        ;;
esac
