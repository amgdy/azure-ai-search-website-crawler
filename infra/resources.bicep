@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param azureAiSearchWebsiteCrawlerExists bool
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

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)

var appShortName = 'crawler'

var additionalSettings = [
  {
    name: 'AzureOpenAi__EndpointUrl'
    value: azureOpenAiEndpointUrl
    secret: false
  }
  {
    name: 'AzureOpenAi__ApiKey'
    value: azureOpenAiApiKey
    secret: true
  }
  {
    name: 'AzureAiSearch__EndpointUrl'
    value: azureAiSearchEndpointUrl
    secret: false
  }
  {
    name: 'AzureAiSearch__ApiKey'
    value: azureAiSearchApiKey
    secret: true
  }
  {
    name: 'WebCrawler__Url'
    value: webCrawlerUrl
    secret: false
  }
]

var combinedSettings = union(array(azureAiSearchWebsiteCrawlerDefinition.settings), additionalSettings)
var azureAiSearchWebsiteCrawlerAppSettingsArray = filter(combinedSettings, i => i.name != '')

// var azureAiSearchWebsiteCrawlerAppSettingsArray = filter(
//   array(azureAiSearchWebsiteCrawlerDefinition.settings),
//   i => i.name != ''
// )
var azureAiSearchWebsiteCrawlerSecrets = map(
  filter(azureAiSearchWebsiteCrawlerAppSettingsArray, i => i.?secret != null),
  i => {
    name: i.name
    value: i.value
    secretRef: i.?secretRef ?? take(replace(replace(toLower(i.name), '_', '-'), '.', '-'), 32)
  }
)
var azureAiSearchWebsiteCrawlerEnv = map(
  filter(azureAiSearchWebsiteCrawlerAppSettingsArray, i => i.?secret == null),
  i => {
    name: i.name
    value: i.value
  }
)

// Monitor application with Azure Monitor
module monitoring 'br/public:avm/ptn/azd/monitoring:0.1.0' = {
  name: 'monitoring'
  params: {
    logAnalyticsName: '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: '${abbrs.insightsComponents}${resourceToken}'
    applicationInsightsDashboardName: '${abbrs.portalDashboards}${resourceToken}'
    location: location
    tags: tags
  }
}

// Container registry
module containerRegistry 'br/public:avm/res/container-registry/registry:0.1.1' = {
  name: 'registry'
  params: {
    name: '${abbrs.containerRegistryRegistries}${appShortName}${resourceToken}'
    location: location
    acrAdminUserEnabled: true
    
    tags: tags
    publicNetworkAccess: 'Enabled'

    roleAssignments: [
      {
        principalId: userAssignedIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: subscriptionResourceId(
          'Microsoft.Authorization/roleDefinitions',
          '7f951dda-4ed3-4680-a7ca-43fe172d538d'
        )
      }
    ]
  }
}

// Container apps environment
module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.8.1' = {
  name: 'container-apps-environment'
  params: {
    logAnalyticsWorkspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
    name: '${abbrs.appManagedEnvironments}${appShortName}-${resourceToken}'
    location: location
    zoneRedundant: false
  }
}

module userAssignedIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'azureAiSearchWebsiteCrawleridentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}${appShortName}-${resourceToken}'
    location: location
  }
}

module azureAiSearchWebsiteCrawlerFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'azureAiSearchWebsiteCrawler-fetch-image'
  params: {
    exists: azureAiSearchWebsiteCrawlerExists
    name: 'container-apps-job'
  }
}

module containerAppsJob 'br/public:avm/res/app/job:0.5.1' = {
  name: 'container-apps-job'
  params: {
    name: '${abbrs.appContainerAppsJobs}${appShortName}-${resourceToken}'
    triggerType: 'Manual'
    manualTriggerConfig: {}
    containers: [
      {
        image: azureAiSearchWebsiteCrawlerFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
        name: 'crawler'
        resources: {
          cpu: '2'
          memory: '4Gi'
        }
        env: union(
          [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: monitoring.outputs.applicationInsightsConnectionString
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: userAssignedIdentity.outputs.clientId
            }
          ],
          azureAiSearchWebsiteCrawlerEnv,
          map(azureAiSearchWebsiteCrawlerSecrets, secret => {
            name: secret.name
            secretRef: secret.secretRef
          })
        )
      }
    ]
    managedIdentities: {
      systemAssigned: false
      userAssignedResourceIds: [userAssignedIdentity.outputs.resourceId]
    }
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: userAssignedIdentity.outputs.resourceId
      }
    ]
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    location: location
    tags: union(tags, { 'azd-service-name': 'azure-ai-search-website-crawler' })
  }
}

// module azureAiSearchWebsiteCrawler 'br/public:avm/res/app/container-app:0.8.0' = {
//   name: 'azureAiSearchWebsiteCrawler'
//   params: {
//     name: 'azure-ai-search-website-crawler'
//     ingressTargetPort: 8080
//     scaleMinReplicas: 1
//     scaleMaxReplicas: 10
//     secrets: {
//       secureList: union(
//         [],
//         map(azureAiSearchWebsiteCrawlerSecrets, secret => {
//           name: secret.secretRef
//           value: secret.value
//         })
//       )
//     }
//     containers: [
//       {
//         image: azureAiSearchWebsiteCrawlerFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
//         name: 'main'
//         resources: {
//           cpu: json('2')
//           memory: '4Gi'
//         }
//         env: union(
//           [
//             {
//               name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
//               value: monitoring.outputs.applicationInsightsConnectionString
//             }
//             {
//               name: 'AZURE_CLIENT_ID'
//               value: azureAiSearchWebsiteCrawlerIdentity.outputs.clientId
//             }
//             {
//               name: 'PORT'
//               value: '8080'
//             }
//           ],
//           azureAiSearchWebsiteCrawlerEnv,
//           map(azureAiSearchWebsiteCrawlerSecrets, secret => {
//             name: secret.name
//             secretRef: secret.secretRef
//           })
//         )
//       }
//     ]
//     managedIdentities: {
//       systemAssigned: false
//       userAssignedResourceIds: [azureAiSearchWebsiteCrawlerIdentity.outputs.resourceId]
//     }
//     registries: [
//       {
//         server: containerRegistry.outputs.loginServer
//         identity: azureAiSearchWebsiteCrawlerIdentity.outputs.resourceId
//       }
//     ]
//     environmentResourceId: containerAppsEnvironment.outputs.resourceId
//     location: location
//     tags: union(tags, { 'azd-service-name': 'azure-ai-search-website-crawler' })
//   }
// }
// Create a keyvault to store secrets
module keyVault 'br/public:avm/res/key-vault/vault:0.6.1' = {
  name: 'keyvault'
  params: {
    name: '${abbrs.keyVaultVaults}${appShortName}-${resourceToken}'
    location: location
    tags: tags
    enableRbacAuthorization: false
    accessPolicies: [
      {
        objectId: principalId
        permissions: {
          secrets: ['get', 'list']
        }
      }
      {
        objectId: userAssignedIdentity.outputs.principalId
        permissions: {
          secrets: ['get', 'list']
        }
      }
    ]
    secrets: [
      for secret in azureAiSearchWebsiteCrawlerSecrets: {
        name: secret.secretRef
        value: secret.value
      }
    ]
  }
}
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.uri
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output AZURE_RESOURCE_AZURE_AI_SEARCH_WEBSITE_CRAWLER_ID string = containerAppsJob.outputs.resourceId
