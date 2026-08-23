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

// A single user-assigned identity shared by all 4 container apps for ACR pull.
// NOT system-assigned: a system-assigned identity's principalId only exists once
// the container app itself is created, so an AcrPull role assignment referencing
// it would depend ON the container app -- but the container app's first revision
// needs to pull its image (and thus needs the role already granted) before ARM
// considers it successfully provisioned. That circular dependency is a documented
// Azure Container Apps + Bicep limitation (fails with "Operation expired").
// A user-assigned identity's principalId is known immediately on creation, so the
// role assignment can be granted BEFORE any container app exists -- no cycle.
resource acrPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2025-05-31-PREVIEW' = {
  name: 'claims-assistant-acr-pull-identity'
  location: location
}

resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, acrPullIdentity.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: acrPullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource policyDbMcp 'Microsoft.App/containerApps@2026-03-02-preview' = {
  name: 'policy-db-mcp'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8101
      }
      registries: [
        { server: acrLoginServer, identity: acrPullIdentity.id }
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
  dependsOn: [
    acrPullRoleAssignment
  ]
}

resource claimsHistoryMcp 'Microsoft.App/containerApps@2026-03-02-preview' = {
  name: 'claims-history-mcp'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8102
      }
      registries: [
        { server: acrLoginServer, identity: acrPullIdentity.id }
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
  dependsOn: [
    acrPullRoleAssignment
  ]
}

resource vinVehicleMcp 'Microsoft.App/containerApps@2026-03-02-preview' = {
  name: 'vin-vehicle-mcp'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8103
      }
      registries: [
        { server: acrLoginServer, identity: acrPullIdentity.id }
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
  dependsOn: [
    acrPullRoleAssignment
  ]
}

resource api 'Microsoft.App/containerApps@2026-03-02-preview' = {
  name: 'claims-assistant-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentity.id}': {}
    }
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
        { server: acrLoginServer, identity: acrPullIdentity.id }
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
  dependsOn: [
    acrPullRoleAssignment
  ]
}

output apiFqdn string = api.properties.configuration.ingress.fqdn
