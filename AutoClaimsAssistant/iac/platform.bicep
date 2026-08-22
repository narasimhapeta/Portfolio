// iac/platform.bicep
// Captures claims-assistant-openai + claims-assistant-search exactly as Phases 3/4
// provisioned them. Decompiled from the live resources via `az group export` +
// `az bicep decompile` during Phase 10 planning, then trimmed of Azure-managed
// defaults (RAI content-filter policies, Defender-for-AI settings) that exist
// automatically on any new account and were never something we configured.
// This template is NOT part of the routine deploy/teardown cycle (see plan
// Architecture) -- it exists so these resources are reproducible from code if
// ever lost, without needing to remember exact model versions/capacities by hand.

@description('Region for the OpenAI account')
param openAiLocation string = 'eastus2'

@description('Region for the Search service')
param searchLocation string = 'Central US'

resource openAi 'Microsoft.CognitiveServices/accounts@2026-05-01' = {
  name: 'claims-assistant-openai'
  location: openAiLocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: 'claims-assistant-openai'
    publicNetworkAccess: 'Enabled'
  }
}

resource extractionAgent 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  parent: openAi
  name: 'extraction-agent'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.4-mini'
      version: '2026-03-17'
    }
  }
}

resource coverageAgent 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  parent: openAi
  name: 'coverage-agent'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.4'
      version: '2026-03-05'
    }
  }
}

resource policyEmbeddings 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  parent: openAi
  name: 'policy-embeddings'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-small'
      version: '1'
    }
  }
}

resource fraudRiskAgent 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  parent: openAi
  name: 'fraud-risk-agent'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.5'
      version: '2026-04-24'
    }
  }
}

resource adjusterSummaryAgent 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  parent: openAi
  name: 'adjuster-summary-agent'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.4-mini'
      version: '2026-03-17'
    }
  }
}

resource evalJudgePrimary 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  parent: openAi
  name: 'eval-judge-primary'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.5'
      version: '2026-04-24'
    }
  }
}

resource evalJudgeSecondary 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  parent: openAi
  name: 'eval-judge-secondary'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4.1'
      version: '2025-04-14'
    }
  }
}

resource search 'Microsoft.Search/searchServices@2026-03-01-preview' = {
  name: 'claims-assistant-search'
  location: searchLocation
  sku: {
    name: 'free'
  }
  properties: {
    hostingMode: 'Default'
    partitionCount: 1
    replicaCount: 1
  }
}

output openAiEndpoint string = openAi.properties.endpoint
output searchEndpoint string = 'https://${search.name}.search.windows.net'
