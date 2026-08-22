// iac/app-infra-base.bicep
// The teardown/redeploy-able layer: registry, environment, database, storage.
// Split from app-infra-apps.bicep because a container app's image must already
// exist in ACR before the app resource can be created -- this template creates
// the empty ACR first; deploy-app-infra-apps.ps1 pushes an image, then deploys
// the container apps.

@description('Region for all resources in this template')
param location string = resourceGroup().location

@description('Admin username for the Postgres Flexible Server')
param postgresAdminUsername string = 'claimsadmin'

@secure()
@description('Admin password for the Postgres Flexible Server')
param postgresAdminPassword string

@description('Your current public IPv4 address, for local seeding access to Postgres')
param localSeedIpAddress string

resource acr 'Microsoft.ContainerRegistry/registries@2026-03-01-preview' = {
  name: 'claimsassistantacr'
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2026-03-01' = {
  name: 'claims-assistant-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource containerAppsEnv 'Microsoft.App/managedEnvironments@2026-03-02-preview' = {
  name: 'claims-assistant-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2026-04-01-preview' = {
  name: 'claims-assistant-pg'
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminUsername
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

resource postgresDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2026-04-01-preview' = {
  parent: postgres
  name: 'claims_assistant'
}

resource postgresFirewallAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2026-04-01-preview' = {
  parent: postgres
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource postgresFirewallLocalSeed 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2026-04-01-preview' = {
  parent: postgres
  name: 'allow-local-seed'
  properties: {
    startIpAddress: localSeedIpAddress
    endIpAddress: localSeedIpAddress
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2026-04-01' = {
  name: 'claimsassistantstorage'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2026-04-01' = {
  parent: storage
  name: 'default'
}

resource claimDocumentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2026-04-01' = {
  parent: blobService
  name: 'claim-documents'
  properties: {
    publicAccess: 'None'
  }
}

output acrLoginServer string = acr.properties.loginServer
output postgresFqdn string = postgres.properties.fullyQualifiedDomainName
output storageAccountName string = storage.name
