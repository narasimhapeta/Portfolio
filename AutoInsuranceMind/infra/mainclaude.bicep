@description('Location for all resources')
param location string = resourceGroup().location

@description('Application name prefix — used to name all resources')
param appName string = 'autoinsurancemind'

@secure()
param azureOpenAIApiKey string

@secure()
param docIntelligenceApiKey string

@secure()
param searchAdminKey string

// ──────────────────────────────────────────────
// STORAGE ACCOUNT + BLOB CONTAINER
// ──────────────────────────────────────────────
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${appName}storage'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'policy-documents'
  properties: { publicAccess: 'None' }
}

// ──────────────────────────────────────────────
// AZURE COGNITIVE SEARCH
// ──────────────────────────────────────────────
resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: '${appName}-search'
  location: location
  sku: { name: 'basic' }
  properties: {
    replicaCount: 1
    partitionCount: 1
  }
}

// ──────────────────────────────────────────────
// AZURE OPENAI
// ──────────────────────────────────────────────
resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: '${appName}-openai'
  location: location
  kind: 'OpenAI'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: '${appName}openai'
  }
}

resource gpt4oMiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: 'gpt-4o-mini'
  sku: { name: 'Standard', capacity: 10 }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o-mini'
      version: '2024-07-18'
    }
  }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: 'text-embedding-ada-002'
  sku: { name: 'Standard', capacity: 10 }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-ada-002'
      version: '2'
    }
  }
  dependsOn: [ gpt4oMiniDeployment ] // only one deployment at a time
}

// ──────────────────────────────────────────────
// AZURE DOCUMENT INTELLIGENCE
// ──────────────────────────────────────────────
resource docIntelligence 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: '${appName}-docintelligence'
  location: location
  kind: 'FormRecognizer'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: '${appName}docintelligence'
  }
}

// ──────────────────────────────────────────────
// APP SERVICE PLAN + API WEB APP
// ──────────────────────────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true // required for Linux
  }
}

resource apiApp 'Microsoft.Web/sites@2022-09-01' = {
  name: '${appName}-api'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      appSettings: [
        { name: 'AzureOpenAI__Endpoint',                  value: openAI.properties.endpoint }
        { name: 'AzureOpenAI__ApiKey',                    value: azureOpenAIApiKey }
        { name: 'AzureOpenAI__DeploymentName',            value: 'gpt-4o-mini' }
        { name: 'AzureOpenAI__EmbeddingDeploymentName',   value: 'text-embedding-ada-002' }
        { name: 'AzureOpenAI__EmbeddingDimensions',       value: '1536' }
        { name: 'AzureBlob__ConnectionString',            value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net' }
        { name: 'AzureBlob__ContainerName',               value: 'policy-documents' }
        { name: 'AzureDocumentIntelligence__Endpoint',    value: docIntelligence.properties.endpoint }
        { name: 'AzureDocumentIntelligence__ApiKey',      value: docIntelligenceApiKey }
        { name: 'AzureCognitiveSearch__Endpoint',         value: 'https://${searchService.name}.search.windows.net' }
        { name: 'AzureCognitiveSearch__AdminKey',         value: searchAdminKey }
        { name: 'AzureCognitiveSearch__IndexName',        value: 'policy-documents' }
        { name: 'AllowedOrigins',                         value: 'https://${staticWebApp.properties.defaultHostname}' }
      ]
    }
  }
}

// ──────────────────────────────────────────────
// AZURE STATIC WEB APP (React frontend)
// ──────────────────────────────────────────────
resource staticWebApp 'Microsoft.Web/staticSites@2022-09-01' = {
  name: '${appName}-ui'
  location: 'eastus2'  // Static Web Apps have limited region support
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

// ──────────────────────────────────────────────
// OUTPUTS
// ──────────────────────────────────────────────
output apiUrl string = 'https://${apiApp.properties.defaultHostName}'
output frontendUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output openAIEndpoint string = openAI.properties.endpoint
output docIntelligenceEndpoint string = docIntelligence.properties.endpoint
output searchEndpoint string = 'https://${searchService.name}.search.windows.net'
output storageAccountName string = storageAccount.name
