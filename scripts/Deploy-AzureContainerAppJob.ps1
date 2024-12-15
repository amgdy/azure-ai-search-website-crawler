if (-not $script:azdCmd) {
    $script:azdCmd = Get-Command "azd" -ErrorAction SilentlyContinue
}

# Check if 'azd' command is available
if (-not $script:azdCmd) {
    throw "Error: 'azd' command is not found. Please ensure you have 'azd' installed. For installation instructions, visit: https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd"
}

if (-not $script:azCmd) {
    $script:azCmd = Get-Command "az" -ErrorAction SilentlyContinue
}

# Check if 'az' command is available
if (-not $script:azCmd) {
    throw "Error: 'az' command is not found. Please ensure you have 'az' installed. For installation instructions, visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
}

if (-not $script:dockerCmd) {
    $script:dockerCmd = Get-Command "docker" -ErrorAction SilentlyContinue
}

# Check if 'docker' command is available
if (-not $script:dockerCmd) {
    throw "Error: 'docker' command is not found. Please ensure you have 'docker' installed. For installation instructions, visit: https://docs.docker.com/get-docker/"
}

Write-Host ""
Write-Host "Loading azd .env file from current environment" -ForegroundColor Green
Write-Host ""

foreach ($line in (& azd env get-values)) {
    if ($line -match "([^=]+)=(.*)") {
        $key = $matches[1]
        $value = $matches[2] -replace '^"|"$'
        Set-Item -Path "Env:$key" -Value $value
    }
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to load environment variables from azd environment" -ForegroundColor Red
    exit $LASTEXITCODE
}
else {
    Write-Host "Successfully loaded env vars from .env file." -ForegroundColor Green
}

if (-not [bool]::Parse($env:AZD_IS_PROVISIONED)) {
    Write-Host "Azure resources are not provisioned. Please run 'azd provision' to set up the necessary resources before running this script." -ForegroundColor Yellow
    exit 1
}

$resourceGroup = $($env:AZURE_RESOURCE_GROUP_NAME)
$environment = $($env:AZURE_CONTAINER_APPS_ENVIRONMENT_NAME)
$jobName = $($env:AZURE_CONTAINER_APP_JOB_NAME)
$loginServer = $($env:AZURE_CONTAINER_REGISTRY_LOGIN_SERVER)
$tag = "azd"
$tag += "-$(Get-Date -Format 'yyyyMMddHHmmss')"
$image = "$($env:AZURE_CONTAINER_REGISTRY_LOGIN_SERVER)/job:$($tag)"

Write-Host "Resource Group: $resourceGroup" -ForegroundColor Green
Write-Host "Environment: $environment" -ForegroundColor Green
Write-Host "Job Name: $jobName" -ForegroundColor Green
Write-Host "Login Server: $loginServer" -ForegroundColor Green
Write-Host "Image: $image" -ForegroundColor Green

$projectDir = Resolve-Path "$PSScriptRoot/../app/AzureAiSearchWebsiteCrawler"

Write-Host "Project Directory: $projectDir" -ForegroundColor Green

Write-Host "Building Docker image..." -ForegroundColor Green
docker build -t "$image" "$projectDir"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker build failed" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Docker build succeeded" -ForegroundColor Green

Write-Host "Logging into Azure Container Registry..." -ForegroundColor Green
az acr login --name $loginServer
if ($LASTEXITCODE -ne 0) {
    Write-Host "ACR login failed" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "ACR login succeeded" -ForegroundColor Green

Write-Host "Pushing Docker image..." -ForegroundColor Green
docker push $image
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker push failed" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Docker push succeeded" -ForegroundColor Green

Write-Host "Updating Azure Container App Job..." -ForegroundColor Green
az containerapp job update --name $jobName --resource-group $resourceGroup --image $image
if ($LASTEXITCODE -ne 0) {
    Write-Host "Container App Job update failed" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Container App Job update succeeded" -ForegroundColor Green

Write-Host "Deployed Azure Container App Job successfully" -ForegroundColor Green

$portalUrl = "https://portal.azure.com/#@$($env:AZURE_TENANT_ID)/resource$($env:AZURE_CONTAINER_APP_JOB_URL)"

Write-Host "You can view the Container App Job in the Azure Portal: " -ForegroundColor Green -NoNewline
Write-Host "$portalUrl" -ForegroundColor Blue