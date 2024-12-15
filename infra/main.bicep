targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

param jobExists bool
@secure()
param azureAiSearchWebsiteCrawlerDefinition object

@description('Id of the user or app to assign application roles')
param principalId string

@description('Endpoint URL for Azure OpenAI service')
param azureOpenAiEndpointUrl string

@secure()
@description('API key for Azure OpenAI service')
param azureOpenAiApiKey string

@description('Endpoint URL for Azure AI Search service')
param azureAiSearchEndpointUrl string

@secure()
@description('API key for Azure AI Search service')
param azureAiSearchApiKey string

@description('URL for the web crawler')
param webCrawlerUrl string

@description('Deployment name for the Azure OpenAI embedding model')
param azureOpenAiEmbeddingModelDeployment string = 'text-embedding-ada-002'

@description('Dimensions for the Azure OpenAI embedding model')
param azureOpenAiEmbeddingModelDimensions int = 1536

@description('Maximum tokens for the Azure OpenAI embedding model')
param azureOpenAiEmbeddingModelMaxTokens int = 8190

@description('Index name for the Azure AI Search service')
param azureAiSearchIndexName string = ''

@description('Maximum pages to crawl for the web crawler')
param webCrawlerMaxPagesToCrawl int = 1000

@description('Maximum crawl depth for the web crawler')
param webCrawlerMaxCrawlDepth int = 5

@description('Maximum retry attempts for the web crawler')
param webCrawlerMaxRetryAttempts int = 3

@description('Maximum batch size for the web crawler')
param webCrawlerMaxBatchSize int = 100

var tags = {
  'azd-env-name': environmentName
  'azd-repo': 'https://github.com/amgdy/azure-ai-search-website-crawler'
}

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    tags: tags
    principalId: principalId
    jobExists: jobExists
    azureAiSearchWebsiteCrawlerDefinition: azureAiSearchWebsiteCrawlerDefinition
  }
}

output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_SUBSCRIPTION_ID string = subscription().subscriptionId
output AZURE_RESOURCE_GROUP_NAME string = rg.name

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_KEY_VAULT_ENDPOINT string = resources.outputs.AZURE_KEY_VAULT_ENDPOINT
output AZURE_KEY_VAULT_NAME string = resources.outputs.AZURE_KEY_VAULT_NAME
output AZURE_RESOURCE_AZURE_AI_SEARCH_WEBSITE_CRAWLER_ID string = resources.outputs.AZURE_RESOURCE_AZURE_AI_SEARCH_WEBSITE_CRAWLER_ID

output AZURE_OPENAI_ENDPOINT_URL string = azureOpenAiEndpointUrl
#disable-next-line outputs-should-not-contain-secrets
output AZURE_OPENAI_API_KEY string = azureOpenAiApiKey
output AZURE_AI_SEARCH_ENDPOINT_URL string = azureAiSearchEndpointUrl
#disable-next-line outputs-should-not-contain-secrets
output AZURE_AI_SEARCH_API_KEY string = azureAiSearchApiKey
output WEB_CRAWLER_URL string = webCrawlerUrl
output AZURE_OPENAI_EMBEDDING_MODEL_DEPLOYMENT string = azureOpenAiEmbeddingModelDeployment
output AZURE_OPENAI_EMBEDDING_MODEL_DIMENSIONS int = azureOpenAiEmbeddingModelDimensions
output AZURE_OPENAI_EMBEDDING_MODEL_MAX_TOKENS int = azureOpenAiEmbeddingModelMaxTokens
output AZURE_AI_SEARCH_INDEX_NAME string = azureAiSearchIndexName
output WEB_CRAWLER_MAX_PAGES_TO_CRAWL int = webCrawlerMaxPagesToCrawl
output WEB_CRAWLER_MAX_CRAWL_DEPTH int = webCrawlerMaxCrawlDepth
output WEB_CRAWLER_MAX_RETRY_ATTEMPTS int = webCrawlerMaxRetryAttempts
output WEB_CRAWLER_MAX_BATCH_SIZE int = webCrawlerMaxBatchSize

output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_NAME
output AZURE_CONTAINER_APP_JOB_NAME string = resources.outputs.AZURE_CONTAINER_APP_JOB_NAME
output AZURE_CONTAINER_APP_JOB_URL string = resources.outputs.AZURE_CONTAINER_APP_JOB_URL
output AZURE_CONTAINER_REGISTRY_NAME string = resources.outputs.AZURE_CONTAINER_REGISTRY_NAME
output AZURE_CONTAINER_REGISTRY_LOGIN_SERVER string = resources.outputs.AZURE_CONTAINER_REGISTRY_LOGIN_SERVER
output AZD_IS_PROVISIONED bool = true
