#!/bin/bash
set -euo pipefail

# =============================================================================
# BookMeApp - AWS CLI Setup Script
# =============================================================================
# Installs AWS CLI v2 (if not present) and configures it with project defaults.
#
# Usage:
#   chmod +x infra/setup-aws-cli.sh
#
#   # Interactive (prompts for credentials):
#   ./infra/setup-aws-cli.sh
#
#   # Non-interactive (pass credentials via environment):
#   AWS_ACCESS_KEY_ID=AKIA... AWS_SECRET_ACCESS_KEY=... ./infra/setup-aws-cli.sh
#
#   # Or use GitHub secrets locally:
#   gh secret list  # to verify secrets exist
#   # (secret values must be provided manually — GitHub does not expose them)
# =============================================================================

AWS_REGION="ap-southeast-1"
AWS_OUTPUT="json"
APP_NAME="bookmeapp"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log()  { echo -e "${GREEN}[SETUP]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error(){ echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# ---------------------------------------------------------------------------
# 1. Install AWS CLI v2 if not present
# ---------------------------------------------------------------------------
install_aws_cli() {
    if command -v aws &>/dev/null; then
        log "AWS CLI already installed: $(aws --version)"
        return 0
    fi

    log "Installing AWS CLI v2..."
    local tmpdir
    tmpdir=$(mktemp -d)
    curl -fsSL "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "${tmpdir}/awscliv2.zip"
    unzip -q "${tmpdir}/awscliv2.zip" -d "${tmpdir}"
    "${tmpdir}/aws/install" --update
    rm -rf "${tmpdir}"

    if ! command -v aws &>/dev/null; then
        error "AWS CLI installation failed."
    fi
    log "AWS CLI installed: $(aws --version)"
}

# ---------------------------------------------------------------------------
# 2. Configure credentials and defaults
# ---------------------------------------------------------------------------
configure_aws() {
    log "Configuring AWS CLI for ${APP_NAME} (region: ${AWS_REGION})..."

    # Set region and output format (these are not secrets)
    aws configure set default.region "${AWS_REGION}"
    aws configure set default.output "${AWS_OUTPUT}"

    # Check if credentials are already set via environment variables
    if [[ -n "${AWS_ACCESS_KEY_ID:-}" && -n "${AWS_SECRET_ACCESS_KEY:-}" ]]; then
        log "Using credentials from environment variables."
        aws configure set aws_access_key_id "${AWS_ACCESS_KEY_ID}"
        aws configure set aws_secret_access_key "${AWS_SECRET_ACCESS_KEY}"
    else
        # Check if credentials are already configured
        local existing_key
        existing_key=$(aws configure get aws_access_key_id 2>/dev/null || echo "")
        if [[ -n "${existing_key}" ]]; then
            log "AWS credentials already configured (key: ${existing_key:0:8}...)."
            read -rp "Overwrite existing credentials? [y/N]: " overwrite
            if [[ "${overwrite}" != "y" && "${overwrite}" != "Y" ]]; then
                log "Keeping existing credentials."
                return 0
            fi
        fi

        # Prompt for credentials
        warn "No AWS credentials found. Please enter your AWS credentials."
        warn "These correspond to the GitHub secrets: AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY"
        echo ""
        read -rp "AWS Access Key ID: " access_key
        read -rsp "AWS Secret Access Key: " secret_key
        echo ""

        if [[ -z "${access_key}" || -z "${secret_key}" ]]; then
            error "Both Access Key ID and Secret Access Key are required."
        fi

        aws configure set aws_access_key_id "${access_key}"
        aws configure set aws_secret_access_key "${secret_key}"
    fi

    log "AWS CLI configured successfully."
}

# ---------------------------------------------------------------------------
# 3. Verify connectivity
# ---------------------------------------------------------------------------
verify_connection() {
    log "Verifying AWS connectivity..."
    local identity
    if identity=$(aws sts get-caller-identity --output json 2>&1); then
        log "Authenticated successfully:"
        echo "${identity}" | python3 -m json.tool 2>/dev/null || echo "${identity}"
    else
        warn "Could not verify AWS identity. Check your credentials."
        warn "Error: ${identity}"
        return 1
    fi
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
main() {
    log "============================================"
    log "BookMeApp AWS CLI Setup"
    log "============================================"

    install_aws_cli
    configure_aws
    verify_connection

    log ""
    log "============================================"
    log "Setup complete! You can now use:"
    log "  aws ecs list-services --cluster ${APP_NAME}-production"
    log "  aws cloudformation describe-stacks --stack-name ${APP_NAME}-production"
    log "  ./infra/deploy.sh"
    log "============================================"
}

main "$@"
