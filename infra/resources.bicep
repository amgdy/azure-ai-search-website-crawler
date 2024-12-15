@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param jobExists bool
@secure()
param azureAiSearchWebsiteCrawlerDefinition object

@description('Id of the user or app to assign application roles')
param principalId string

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location, environment().name)
var appShortName = 'crawler'

var additionalSettings = []
var combinedSettings = union(array(azureAiSearchWebsiteCrawlerDefinition.settings), additionalSettings)
var azureAiSearchWebsiteCrawlerAppSettingsArray = filter(combinedSettings, i => i.name != '')

var azureAiSearchWebsiteCrawlerSecrets = map(
  filter(azureAiSearchWebsiteCrawlerAppSettingsArray, i => i.?secret != null),
  i => {
    name: i.name
    value: i.value
    secretRef: i.?secretRef ?? take(replace(replace(toLower(i.name), '_', ''), '.', ''), 32)
  }
)
var azureAiSearchWebsiteCrawlerEnv = map(
  filter(azureAiSearchWebsiteCrawlerAppSettingsArray, i => i.?secret == null),
  i => {
    name: i.name
    value: i.value
  }
)

// User assigned identity
module userAssignedIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.4.0' = {
  name: 'azureAiSearchWebsiteCrawleridentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}${resourceToken}-${appShortName}'
    location: location
  }
}

// Monitor application with Azure Monitor
module monitoring 'br/public:avm/ptn/azd/monitoring:0.1.0' = {
  name: 'monitoring'
  params: {
    logAnalyticsName: '${abbrs.operationalInsightsWorkspaces}${resourceToken}-${appShortName}'
    applicationInsightsName: '${abbrs.insightsComponents}${resourceToken}-${appShortName}'
    applicationInsightsDashboardName: '${abbrs.portalDashboards}${resourceToken}-${appShortName}'
    location: location
    tags: tags
  }
}

// Create a keyvault to store secrets
module keyVault 'br/public:avm/res/key-vault/vault:0.11.0' = {
  name: 'keyvault'
  params: {
    name: '${abbrs.keyVaultVaults}${resourceToken}-${appShortName}'
    location: location
    tags: tags
    enableRbacAuthorization: false
    enablePurgeProtection: false
    enableSoftDelete: false
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

// Container registry
module containerRegistry 'br/public:avm/res/container-registry/registry:0.6.0' = {
  name: 'registry'
  params: {
    name: '${abbrs.containerRegistryRegistries}${resourceToken}${appShortName}'
    location: location
    acrAdminUserEnabled: false
    exportPolicyStatus: 'enabled'
    publicNetworkAccess: 'Enabled'
    tags: tags
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
    name: '${abbrs.appManagedEnvironments}${resourceToken}-${appShortName}'
    location: location
    zoneRedundant: false
  }
}

// Fetch latest image for the container app
module latestImage './modules/fetch-container-image.bicep' = {
  name: 'azureAiSearchWebsiteCrawler-fetch-image'
  params: {
    exists: jobExists
    name: 'container-apps-job'
  }
}

// Container apps job
module containerAppJob 'br/public:avm/res/app/job:0.5.1' = {
  name: 'container-apps-job'
  params: {
    name: '${abbrs.appContainerAppsJobs}${resourceToken}-${appShortName}'
    triggerType: 'Manual'
    manualTriggerConfig: {}
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
    tags: union(tags, { 'azd-service-name': 'job' })
    secrets: [
      for secret in azureAiSearchWebsiteCrawlerSecrets: {
        name: secret.secretRef
        keyVaultUrl: '${keyVault.outputs.uri}secrets/${secret.secretRef}'
        identity: userAssignedIdentity.outputs.resourceId
      }
    ]
    containers: [
      {
        image: latestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
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
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.uri
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = containerAppsEnvironment.outputs.resourceId
output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = containerAppsEnvironment.outputs.name
output AZURE_CONTAINER_APP_JOB_NAME string = containerAppJob.outputs.name
output AZURE_CONTAINER_APP_JOB_URL string = containerAppJob.outputs.resourceId
output AZURE_CONTAINER_REGISTRY_NAME string = containerRegistry.outputs.name
output AZURE_CONTAINER_REGISTRY_LOGIN_SERVER string = containerRegistry.outputs.loginServer
output AZURE_RESOURCE_AZURE_AI_SEARCH_WEBSITE_CRAWLER_ID string = containerAppJob.outputs.resourceId
