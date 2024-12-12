$resourceGroup = "rg-websitecrawl-01"
$environment = "aca-env-websitecrawl-01"
$jobName = "aca-job-websitecrawl-01"

$resourceGroup = "rg-rag-aca-swc-01"
$environment = "rag-aca-aca-env"
$jobName = "cac-job-websitecrawl-01"
$image = "ghcr.io/amgdy/azure-ai-search-website-crawler/azure-ai-search-website-crawler:sha-dfa7a77"

# Environment Variables
$APPLICATIONINSIGHTS_CONNECTION_STRING = ""
$AzureOpenAi__EndpointUrl = ""
$AzureOpenAi__ApiKey = ""
$AzureAiSearch__EndpointUrl = ""
$AzureAiSearch__ApiKey = ""
$WebCrawler__Url = ""

az containerapp job create --name $jobName --resource-group $resourceGroup --environment $environment --trigger-type "Manual" --replica-timeout 1800 --image $image --cpu "2" --memory "4Gi" --mi-system-assigned --env-vars `
    APPLICATIONINSIGHTS_CONNECTION_STRING=$APPLICATIONINSIGHTS_CONNECTION_STRING `
    AzureOpenAi__EndpointUrl=$AzureOpenAi__EndpointUrl `
    AzureOpenAi__ApiKey=$AzureOpenAi__ApiKey `
    AzureAiSearch__EndpointUrl=$AzureAiSearch__EndpointUrl `
    AzureAiSearch__ApiKey=$AzureAiSearch__ApiKey `
    WebCrawler__Url=$WebCrawler__Url