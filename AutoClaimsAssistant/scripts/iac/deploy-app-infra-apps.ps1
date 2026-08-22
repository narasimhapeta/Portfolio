# scripts/iac/deploy-app-infra-apps.ps1
# Builds + pushes the image to ACR, then deploys the 4 container apps.
# Run after deploy-app-infra-base.ps1 has created the registry.

$ErrorActionPreference = "Stop"

$postgresPassword = Read-Host "Postgres admin password" -AsSecureString
$postgresPasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($postgresPassword)
)
$openAiKey = Read-Host "Azure OpenAI API key" -AsSecureString
$openAiKeyPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($openAiKey)
)
$searchKey = Read-Host "Azure Search API key" -AsSecureString
$searchKeyPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($searchKey)
)

az acr login --name claimsassistantacr
$sha = (git rev-parse --short HEAD)
docker build -t "claimsassistantacr.azurecr.io/claims-assistant:$sha" .
docker push "claimsassistantacr.azurecr.io/claims-assistant:$sha"

$storageConn = az storage account show-connection-string --name claimsassistantstorage --resource-group claims-assistant-rg --query connectionString -o tsv
$openAiEndpoint = az cognitiveservices account show --name claims-assistant-openai --resource-group claims-assistant-rg --query properties.endpoint -o tsv
$searchEndpoint = "https://claims-assistant-search.search.windows.net"

$deployment = az deployment group create `
    --resource-group claims-assistant-rg `
    --template-file iac/app-infra-apps.bicep `
    --parameters imageTag=$sha postgresAdminPassword=$postgresPasswordPlain azureOpenAiApiKey=$openAiKeyPlain azureSearchApiKey=$searchKeyPlain azureStorageConnectionString=$storageConn azureOpenAiEndpoint=$openAiEndpoint azureOpenAiApiVersion="2024-12-01-preview" azureSearchEndpoint=$searchEndpoint `
    --query "properties.outputs" -o json | ConvertFrom-Json

Write-Host "API deployed at: https://$($deployment.apiFqdn.value)"
