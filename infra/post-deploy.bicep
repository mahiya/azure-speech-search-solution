param location string = resourceGroup().location
param storageAccountName string
param blobContainerName string
param functionAppName string
param functionName string
var resourceGroupId = uniqueString(resourceGroup().id)

// EventGrid: Topic
resource systemTopic 'Microsoft.EventGrid/systemTopics@2021-12-01' = {
  name: 'evgt-${resourceGroupId}'
  location: location
  properties: {
    source: resourceId('Microsoft.Storage/storageAccounts', storageAccountName)
    topicType: 'Microsoft.Storage.StorageAccounts'
  }
}

// EventGrid: Subscription
resource eventSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2021-12-01' = {
  parent: systemTopic
  name: 'evgs-${resourceGroupId}'
  properties: {
    destination: {
      endpointType: 'AzureFunction'
      properties: {
        maxEventsPerBatch: 1
        preferredBatchSizeInKilobytes: 64
        resourceId: resourceId('Microsoft.Web/sites/functions', functionAppName, functionName)
      }
    }
    filter: {
      includedEventTypes: [
        'Microsoft.Storage.BlobCreated'
      ]
      subjectBeginsWith: '/blobServices/default/containers/${blobContainerName}'
      subjectEndsWith: '.wav'
    }
  }
}
