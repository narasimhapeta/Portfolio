// iac/app-infra-apps.bicep
// The 4 container apps. Deployed separately from app-infra-base.bicep because
// `image` must reference something that already exists in ACR -- run this only
// after app-infra-base.bicep has created the registry AND an image has been
// pushed to it (deploy-app-infra-apps.ps1 does both in the right order).

@description('Region for all resources in this template')
param location string = resourceGroup().location

@description('Image tag to deploy (e.g. a git short SHA) - image must already exist in ACR')
param imageTag string

@secure()
param postgresAdminPassword string

@secure()
param azureOpenAiApiKey string

@secure()
param azureSearchApiKey string

@secure()
param azureStorageConnectionString string

param azureOpenAiEndpoint string
param azureOpenAiApiVersion string
param azureSearchEndpoint string

var acrLoginServer = '${acr.name}.azurecr.io'
var image = '${acrLoginServer}/claims-assistant:${imageTag}'
var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')

resource acr 'Microsoft.ContainerRegistry/registries@2026-03-01-preview' existing = {
  name: 'claimsassistantacr'
}

resource containerAppsEnv 'Microsoft.App/managedEnvironments@2026-03-02-preview' existing = {
  name: 'claims-assistant-env'
}

resource policyDbMcp 'Microsoft.App/containerApps@2026-03-02-preview' = {
  name: 'policy-db-mcp'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8101
      }
      registries: [
        { server: acrLoginServer, identity: 'system' }
      ]
      secrets: [
        { name: 'postgres-password', value: postgresAdminPassword }
      ]
    }
    template: {
      containers: [
        {
          name: 'policy-db-mcp'
          image: image
          command: ['uv']
          args: ['run', 'python', '-m', 'claims_assistant.mcp_servers.policy_db']
          env: [
            { name: 'POSTGRES_HOST', value: 'claims-assistant-pg.postgres.database.azure.com' }
            { name: 'POSTGRES_PORT', value: '5432' }
            { name: 'POSTGRES_DB', value: 'claims_assistant' }
            { name: 'POSTGRES_USER', value: 'claimsadmin' }
            { name: 'POSTGRES_PASSWORD', secretRef: 'postgres-password' }
            { name: 'POSTGRES_SSL_MODE', value: 'require' }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
}

resource policyDbMcpAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, policyDbMcp.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: policyDbMcp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource claimsHistoryMcp 'Microsoft.App/containerApps@2026-03-02-preview' = {
  name: 'claims-history-mcp'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8102
      }
      registries: [
        { server: acrLoginServer, identity: 'system' }
      ]
      secrets: [
        { name: 'postgres-password', value: postgresAdminPassword }
      ]
    }
    template: {
      containers: [
        {
          name: 'claims-history-mcp'
          image: image
          command: ['uv']
          args: ['run', 'python', '-m', 'claims_assistant.mcp_servers.claims_history']
          env: [
            { name: 'POSTGRES_HOST', value: 'claims-assistant-pg.postgres.database.azure.com' }
            { name: 'POSTGRES_PORT', value: '5432' }
            { name: 'POSTGRES_DB', value: 'claims_assistant' }
            { name: 'POSTGRES_USER', value: 'claimsadmin' }
            { name: 'POSTGRES_PASSWORD', secretRef: 'postgres-password' }
            { name: 'POSTGRES_SSL_MODE', value: 'require' }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
}

resource claimsHistoryMcpAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, claimsHistoryMcp.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: claimsHistoryMcp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource vinVehicleMcp 'Microsoft.App/containerApps@2026-03-02-preview' = {
  name: 'vin-vehicle-mcp'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8103
      }
      registries: [
        { server: acrLoginServer, identity: 'system' }
      ]
      secrets: [
        { name: 'postgres-password', value: postgresAdminPassword }
      ]
    }
    template: {
      containers: [
        {
          name: 'vin-vehicle-mcp'
          image: image
          command: ['uv']
          args: ['run', 'python', '-m', 'claims_assistant.mcp_servers.vin_vehicle']
          env: [
            { name: 'POSTGRES_HOST', value: 'claims-assistant-pg.postgres.database.azure.com' }
            { name: 'POSTGRES_PORT', value: '5432' }
            { name: 'POSTGRES_DB', value: 'claims_assistant' }
            { name: 'POSTGRES_USER', value: 'claimsadmin' }
            { name: 'POSTGRES_PASSWORD', secretRef: 'postgres-password' }
            { name: 'POSTGRES_SSL_MODE', value: 'require' }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
}

resource vinVehicleMcpAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, vinVehicleMcp.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: vinVehicleMcp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource api 'Microsoft.App/containerApps@2026-03-02-preview' = {
  name: 'claims-assistant-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: {
        external: true
        targetPort: 8000
      }
      registries: [
        { server: acrLoginServer, identity: 'system' }
      ]
      secrets: [
        { name: 'postgres-password', value: postgresAdminPassword }
        { name: 'azure-openai-key', value: azureOpenAiApiKey }
        { name: 'azure-search-key', value: azureSearchApiKey }
        { name: 'azure-storage-conn', value: azureStorageConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'claims-assistant-api'
          image: image
          env: [
            { name: 'POSTGRES_HOST', value: 'claims-assistant-pg.postgres.database.azure.com' }
            { name: 'POSTGRES_PORT', value: '5432' }
            { name: 'POSTGRES_DB', value: 'claims_assistant' }
            { name: 'POSTGRES_USER', value: 'claimsadmin' }
            { name: 'POSTGRES_PASSWORD', secretRef: 'postgres-password' }
            { name: 'POSTGRES_SSL_MODE', value: 'require' }
            { name: 'POLICY_DB_MCP_URL', value: 'http://policy-db-mcp/mcp' }
            { name: 'CLAIMS_HISTORY_MCP_URL', value: 'http://claims-history-mcp/mcp' }
            { name: 'VIN_VEHICLE_MCP_URL', value: 'http://vin-vehicle-mcp/mcp' }
            { name: 'AZURE_OPENAI_ENDPOINT', value: azureOpenAiEndpoint }
            { name: 'AZURE_OPENAI_API_KEY', secretRef: 'azure-openai-key' }
            { name: 'AZURE_OPENAI_API_VERSION', value: azureOpenAiApiVersion }
            { name: 'AZURE_OPENAI_COVERAGE_DEPLOYMENT', value: 'coverage-agent' }
            { name: 'AZURE_OPENAI_EMBEDDING_DEPLOYMENT', value: 'policy-embeddings' }
            { name: 'AZURE_OPENAI_FRAUD_DEPLOYMENT', value: 'fraud-risk-agent' }
            { name: 'AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT', value: 'adjuster-summary-agent' }
            { name: 'AZURE_SEARCH_ENDPOINT', value: azureSearchEndpoint }
            { name: 'AZURE_SEARCH_API_KEY', secretRef: 'azure-search-key' }
            { name: 'AZURE_SEARCH_INDEX_NAME', value: 'policy-documents' }
            { name: 'AZURE_STORAGE_CONNECTION_STRING', secretRef: 'azure-storage-conn' }
            { name: 'AZURE_STORAGE_CONTAINER_NAME', value: 'claim-documents' }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 3 }
    }
  }
}

resource apiAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, api.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output apiFqdn string = api.properties.configuration.ingress.fqdn
