# scripts/iac/deploy-app-infra-base.ps1
# Deploys ACR, Log Analytics, Container Apps environment, Postgres, and Storage.
# Then seeds Postgres with the Phase 1 synthetic dataset. Run this once to stand
# the base layer up (fresh, or after a teardown-app-infra.ps1).

$ErrorActionPreference = "Stop"

$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org?format=text" -Headers @{ "User-Agent" = "curl" })
Write-Host "Detected IPv4: $myIp"

$postgresPassword = Read-Host "Postgres admin password (new or existing)" -AsSecureString
$postgresPasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($postgresPassword)
)

$deployment = az deployment group create `
    --resource-group claims-assistant-rg `
    --template-file iac/app-infra-base.bicep `
    --parameters postgresAdminPassword=$postgresPasswordPlain localSeedIpAddress=$myIp `
    --query "properties.outputs" -o json | ConvertFrom-Json

Write-Host "ACR: $($deployment.acrLoginServer.value)"
Write-Host "Postgres FQDN: $($deployment.postgresFqdn.value)"
Write-Host "Storage account: $($deployment.storageAccountName.value)"

Write-Host "Seeding Postgres..."
$env:POSTGRES_HOST = $deployment.postgresFqdn.value
$env:POSTGRES_PORT = "5432"
$env:POSTGRES_DB = "claims_assistant"
$env:POSTGRES_USER = "claimsadmin"
$env:POSTGRES_PASSWORD = $postgresPasswordPlain
$env:POSTGRES_SSL_MODE = "require"
uv run python scripts/seed_db.py

Write-Host "Base infra deployed and seeded. Save the Postgres password -- deploy-app-infra-apps.ps1 needs it again."
