#!/bin/bash

set -euo pipefail

# Function to check if a command exists
command_exists() {
    command -v "$1" &> /dev/null
}

# Check if 'azd' command is available
if ! command_exists azd; then
    echo "Error: 'azd' command is not found. Please ensure you have 'azd' installed. For installation instructions, visit: https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd"
    exit 1
fi

# Check if 'az' command is available
if ! command_exists az; then
    echo "Error: 'az' command is not found. Please ensure you have 'az' installed. For installation instructions, visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if 'docker' command is available
if ! command_exists docker; then
    echo "Error: 'docker' command is not found. Please ensure you have 'docker' installed. For installation instructions, visit: https://docs.docker.com/get-docker/"
    exit 1
fi

echo ""
echo "Loading azd .env file from current environment"
echo ""

while IFS='=' read -r key value; do
    if [[ $key =~ ^[^#]*$ ]]; then
        export "$key"="${value%\"}"
    fi
done < <(azd env get-values)

echo "Successfully loaded env vars from .env file."

if [[ "${AZD_IS_PROVISIONED,,}" != "true" ]]; then
    echo "Azure resources are not provisioned. Please run 'azd provision' to set up the necessary resources before running this script."
    exit 1
fi

resource_group="$AZURE_RESOURCE_GROUP_NAME"
environment="$AZURE_CONTAINER_APPS_ENVIRONMENT_NAME"
job_name="$AZURE_CONTAINER_APP_JOB_NAME"
login_server="$AZURE_CONTAINER_REGISTRY_LOGIN_SERVER"
tag="azd-$(date +%Y%m%d%H%M%S)"
image="$login_server/job:$tag"

echo "Resource Group: $resource_group"
echo "Environment: $environment"
echo "Job Name: $job_name"
echo "Login Server: $login_server"
echo "Image: $image"

project_dir=$(realpath "$(dirname "$0")/../app/AzureAiSearchWebsiteCrawler")

echo "Project Directory: $project_dir"

echo "Building Docker image..."
docker build -t "$image" "$project_dir"
echo "Docker build succeeded"

echo "Logging into Azure Container Registry..."
az acr login --name "$login_server"
echo "ACR login succeeded"

echo "Pushing Docker image..."
docker push "$image"
echo "Docker push succeeded"

echo "Updating Azure Container App Job..."
az containerapp job update --name "$job_name" --resource-group "$resource_group" --image "$image"
echo "Container App Job update succeeded"

echo "Deployed Azure Container App Job successfully"

portal_url="https://portal.azure.com/#@$AZURE_TENANT_ID/resource$AZURE_CONTAINER_APP_JOB_URL"

echo -n "You can view the Container App Job in the Azure Portal: "
echo "$portal_url"