#!/bin/bash

# Check if 'azd' command is available
if ! command -v azd &> /dev/null
then
    echo "Error: 'azd' command is not found. Please ensure you have 'azd' installed. For installation instructions, visit: https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd"
    exit 1
fi

# Check if 'az' command is available
if ! command -v az &> /dev/null
then
    echo "Error: 'az' command is not found. Please ensure you have 'az' installed. For installation instructions, visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if 'docker' command is available
if ! command -v docker &> /dev/null
then
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

if [ $? -ne 0 ]; then
    echo "Failed to load environment variables from azd environment"
    exit $?
else
    echo "Successfully loaded env vars from .env file."
fi

if [ "$AZD_IS_PROVISIONED" != "true" ]; then
    echo "Azure resources are not provisioned. Please run 'azd provision' to set up the necessary resources before running this script."
    exit 1
fi

resourceGroup=$AZURE_RESOURCE_GROUP_NAME
environment=$AZURE_CONTAINER_APPS_ENVIRONMENT_NAME
jobName=$AZURE_CONTAINER_APP_JOB_NAME
loginServer=$AZURE_CONTAINER_REGISTRY_LOGIN_SERVER
tag="azd-$(date +%Y%m%d%H%M%S)"
image="$AZURE_CONTAINER_REGISTRY_LOGIN_SERVER/job:$tag"

echo "Resource Group: $resourceGroup"
echo "Environment: $environment"
echo "Job Name: $jobName"
echo "Login Server: $loginServer"
echo "Image: $image"

projectDir=$(realpath "$BASH_SOURCE/../app/AzureAiSearchWebsiteCrawler")

echo "Project Directory: $projectDir"

echo "Building Docker image..."
docker build -t "$image" "$projectDir"
if [ $? -ne 0 ]; then
    echo "Docker build failed"
    exit $?
fi
echo "Docker build succeeded"

echo "Logging into Azure Container Registry..."
az acr login --name $loginServer
if [ $? -ne 0 ]; then
    echo "ACR login failed"
    exit $?
fi
echo "ACR login succeeded"

echo "Pushing Docker image..."
docker push $image
if [ $? -ne 0 ]; then
    echo "Docker push failed"
    exit $?
fi
echo "Docker push succeeded"

echo "Updating Azure Container App Job..."
az containerapp job update --name $jobName --resource-group $resourceGroup --image $image
if [ $? -ne 0 ]; then
    echo "Container App Job update failed"
    exit $?
fi
echo "Container App Job update succeeded"

echo "Deployed Azure Container App Job successfully"

portalUrl="https://portal.azure.com/#@$AZURE_TENANT_ID/resource$AZURE_CONTAINER_APP_JOB_URL"

echo -n "You can view the Container App Job in the Azure Portal: "
echo "$portalUrl"