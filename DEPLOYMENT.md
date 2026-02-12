# Deployment Guide

This document explains how to deploy the BookingScheduleSystem and where to find your application after deployment.

## Deployment Overview

The application is automatically deployed to AWS ECS (Elastic Container Service) when changes are pushed to the `main` branch.

### Deployment Target

- **Platform**: AWS ECS (Elastic Container Service)
- **Cluster**: `bookmeapp-production`
- **Region**: Configured via `AWS_REGION` repository variable
- **Services**:
  - `bookmeapp-api` - Backend API service
  - `bookmeapp-web` - Frontend web application service

## Where to Find Your Application After Deployment

### 1. GitHub Actions Deployment Summary

After each deployment, GitHub Actions generates a deployment summary with all the important URLs and information.

**To view the deployment summary:**

1. Go to the [Actions tab](../../actions) in your GitHub repository
2. Click on the most recent "Deploy to AWS" workflow run
3. Scroll down to the bottom of the workflow run page
4. Look for the **"Deployment Summary"** section

The deployment summary includes:

| Item | Description |
|------|-------------|
| **Cluster** | The ECS cluster name (e.g., `bookmeapp-production`) |
| **API Image** | The Docker image URL for the API in ECR |
| **Web Image** | The Docker image URL for the Web app in ECR |
| **Commit** | The Git commit SHA that was deployed |
| **ALB URL** | **Your application URL** - Use this to access your app |
| **Health Check** | URL to check if the API is healthy |

### 2. AWS Console

You can also find your application URL in the AWS Console:

1. **Via CloudFormation:**
   - Open the [AWS CloudFormation Console](https://console.aws.amazon.com/cloudformation/)
   - Select your AWS region (check your `AWS_REGION` variable)
   - Find the stack named `bookmeapp-production`
   - Go to the **Outputs** tab
   - Look for the `AlbDnsName` output - this is your application URL

2. **Via ECS:**
   - Open the [AWS ECS Console](https://console.aws.amazon.com/ecs/)
   - Select your AWS region
   - Click on the `bookmeapp-production` cluster
   - Click on either the `bookmeapp-api` or `bookmeapp-web` service
   - Go to the **Load balancing** tab to find the load balancer
   - Click on the load balancer name to view its DNS name

3. **Via Load Balancers:**
   - Open the [AWS EC2 Console](https://console.aws.amazon.com/ec2/)
   - Select your AWS region
   - Click on **Load Balancers** in the left menu
   - Find the Application Load Balancer associated with your ECS cluster
   - The **DNS name** is your application URL

## Application URLs

Once you have your Application Load Balancer DNS name (e.g., `bookmeapp-prod-alb-123456789.us-east-1.elb.amazonaws.com`):

- **Web Application**: `http://<alb-dns-name>` or `http://<alb-dns-name>:5288`
- **API**: `http://<alb-dns-name>:5059`
- **API Health Check**: `http://<alb-dns-name>:5059/api/health`
- **API Documentation**: `http://<alb-dns-name>:5059/swagger` (if enabled)

> **Note**: The exact port configuration depends on your load balancer target group setup. Check your AWS infrastructure configuration for the specific ports.

## Deployment Workflow

The deployment is triggered automatically when:

1. Code is pushed to the `main` branch
2. A workflow is manually triggered via the "Actions" tab (using "workflow_dispatch")

### Deployment Steps

The deployment workflow performs the following steps:

1. **Build Docker Images**
   - Builds the API Docker image from `src/BookingScheduleSystem.Api/Dockerfile`
   - Builds the Web Docker image from `src/BookingScheduleSystem.Web/Dockerfile`

2. **Push to ECR**
   - Pushes images to Amazon Elastic Container Registry (ECR)
   - Tags images with both `latest` and the commit SHA

3. **Deploy to ECS**
   - Updates the ECS services with the new images
   - Forces a new deployment to roll out changes
   - Waits for services to stabilize before completing

4. **Generate Summary**
   - Creates a deployment summary with URLs and image information
   - Available in the GitHub Actions workflow run page

## Monitoring Your Deployment

### Check Deployment Status

1. **GitHub Actions**: View the workflow run in the Actions tab to see real-time deployment progress
2. **AWS ECS Console**: Monitor service health and task status in the ECS console
3. **CloudWatch Logs**: View application logs in AWS CloudWatch
4. **Health Check Endpoint**: Test the API health endpoint to verify the API is running

### Common Deployment Times

- **Build & Push**: ~5-10 minutes
- **ECS Service Update**: ~2-5 minutes
- **Total Deployment**: ~7-15 minutes

## Troubleshooting

### Deployment Failed

If the deployment workflow fails:

1. Check the GitHub Actions workflow logs for error messages
2. Verify AWS credentials are correctly configured in repository secrets
3. Check AWS CloudWatch Logs for application errors
4. Review ECS service events in the AWS Console

### Application Not Accessible

If you can't access the application after deployment:

1. Verify the deployment completed successfully in GitHub Actions
2. Check that the ECS services are running in the AWS Console
3. Verify security group rules allow inbound traffic on the required ports
4. Test the health check endpoint to see if the API is responding
5. Check CloudWatch Logs for application startup errors

## Manual Deployment

To manually trigger a deployment:

1. Go to the [Actions tab](../../actions)
2. Click on "Deploy to AWS" in the left sidebar
3. Click "Run workflow" button
4. Select the `main` branch
5. Click "Run workflow" to start the deployment

## Rollback

To rollback to a previous version:

1. Find the commit SHA of the version you want to rollback to
2. In the AWS ECS Console, update the service with the previous image tag
3. Or, revert the commit in Git and push to trigger a new deployment

## Security Notes

- The deployment uses GitHub Secrets for AWS credentials
- Never commit AWS credentials to the repository
- The JWT secret key should be changed in production
- Use AWS Secrets Manager or Parameter Store for production secrets

## Additional Resources

- [AWS ECS Documentation](https://docs.aws.amazon.com/ecs/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Docker Documentation](https://docs.docker.com/)

## Questions or Issues?

If you have questions about deployment or can't find your application:

1. Check the deployment summary in GitHub Actions
2. Review this documentation
3. Check AWS Console resources
4. Contact your infrastructure team or AWS administrator
