param location string = resourceGroup().location
var resourceGroupId = uniqueString(resourceGroup().id)
param blobContainerName string
param cognitiveSearchIndexName string

// Azure Storage
var storageName = 'str${resourceGroupId}'
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

// Azure Storage Blob Settings
resource storageBlob 'Microsoft.Storage/storageAccounts/blobServices@2022-05-01' = {
  name: 'default'
  parent: storageAccount
  properties: {
    cors: {
      corsRules: [
        {
          allowedHeaders: [
            '*'
          ]
          allowedMethods: [
            'PUT'
          ]
          allowedOrigins: [
            '*'
          ]
          exposedHeaders: [
            '*'
          ]
          maxAgeInSeconds: 3600
        }
      ]
    }
  }
}

// Azure Storage: Blob Container
resource storageBlobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: blobContainerName
  parent: storageBlob
}

// Cognitive Service - Speech Services
resource cognitiveService 'Microsoft.CognitiveServices/accounts@2021-10-01' = {
  name: 'cog-${resourceGroupId}'
  location: location
  sku: {
    name: 'S0'
  }
  kind: 'SpeechServices'
  properties: {
  }
}

// Cognitive Search Service
resource cognitiveSearch 'Microsoft.Search/searchServices@2021-04-01-preview' = {
  name: 'cogs-${resourceGroupId}'
  location: location
  sku: {
    name: 'standard'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    partitionCount: 1
    replicaCount: 1
  }
}

// User Assigned Identity -> Functions
resource managedId 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = {
  name: 'id-${resourceGroupId}'
  location: location
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: 'kv-${resourceGroupId}'
  location: location
  properties: {
    publicNetworkAccess: 'Enabled'
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: [
      {
        tenantId: managedId.properties.tenantId
        objectId: managedId.properties.principalId
        permissions: {
          secrets: [
            'Get'
          ]
        }
      }
    ]
    tenantId: tenant().tenantId
  }
}

// Key Vault - Secret : Cognitive Service API Key
resource keyVaultSecret_CognitiveService 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  name: 'cognitive-service-api-key'
  parent: keyVault
  properties: {
    value: listKeys(cognitiveService.id, cognitiveService.apiVersion).key1
  }
}

// // Key Vault - Secret : Cognitive Search Admin API Key
resource keyVaultSecret_CognitiveSearch 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  name: 'cognitive-search-api-key'
  parent: keyVault
  properties: {
    value: listAdminKeys(cognitiveSearch.id, cognitiveSearch.apiVersion).primaryKey
  }
}

// Define a name of Azure func App
var functionAppName = 'func-${resourceGroupId}'

// Azure Application Insights
var appInsightsName = 'appi-${resourceGroupId}'
resource appInsights 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
  tags: {
    // circular dependency means we can't reference functionApp directly  /subscriptions/<subscriptionId>/resourceGroups/<rg-name>/providers/Microsoft.Web/sites/<appName>"
    'hidden-link:/subscriptions/${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.Web/sites/${functionAppName}': 'Resource'
  }
}

// Azure App Service Plan
var hostingPlanName = 'plan-${resourceGroupId}'
resource hostingPlan 'Microsoft.Web/serverfarms@2020-10-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}

// Azure Function App
var functionExtentionVersion = '~4'
var functionsWorkerRuntime = 'dotnet'
resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedId.id}': {}
    }
  }
  properties: {
    httpsOnly: true
    serverFarmId: hostingPlan.id
    clientAffinityEnabled: true
    keyVaultReferenceIdentity: managedId.id
    siteConfig: {
      cors: {
        allowedOrigins: [
          '*' // Set host name of web site will be embedded the web chat
        ]
      }
      appSettings: [
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: functionExtentionVersion
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: functionsWorkerRuntime
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value}'
        }
        {
          name: 'MANAGED_IDENTITY_CLIENT_ID'
          value: managedId.properties.clientId
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: storageAccount.name
        }
        {
          name: 'STORAGE_CONTAINER_NAME'
          value: storageBlobContainer.name
        }
        {
          name: 'COGNITIVE_SERVICE_LOCATION'
          value: cognitiveService.location
        }
        {
          name: 'COGNITIVE_SERVICE_API_KEY'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultSecret_CognitiveService.properties.secretUriWithVersion})'
        }
        {
          name: 'COGNITIVE_SEARCH_NAME'
          value: cognitiveSearch.name
        }
        {
          name: 'COGNITIVE_SEARCH_INDEX_NAME'
          value: cognitiveSearchIndexName
        }
        {
          name: 'COGNITIVE_SEARCH_API_KEY'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultSecret_CognitiveSearch.properties.secretUriWithVersion})'
        }
      ]
    }
  }
}

// Role Definition: Storage Blob Data Contributor
resource roleDefinition 'Microsoft.Authorization/roleDefinitions@2018-01-01-preview' existing = {
  scope: storageAccount
  name: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
}

// Role Assignment: Function App -> Storage (Storage Blob Data Contributor)
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  scope: storageAccount
  name: guid(resourceGroup().id, functionApp.id, roleDefinition.id)
  properties: {
    roleDefinitionId: roleDefinition.id
    principalId: managedId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Output:
output storageAccountName string = storageAccount.name
output cognitiveSearchName string = cognitiveSearch.name
output functionAppName string = functionApp.name
