# scripts/iac/teardown-app-infra.ps1
# Deletes everything deploy-app-infra-base.ps1 / -apps.ps1 created. Does NOT
# touch claims-assistant-openai or claims-assistant-search (platform.bicep) --
# those are never deleted by this script, intentionally.

$ErrorActionPreference = "Continue"  # keep going even if one resource is already gone

Write-Host "Deleting container apps..."
az containerapp delete --name claims-assistant-api --resource-group claims-assistant-rg --yes
az containerapp delete --name policy-db-mcp --resource-group claims-assistant-rg --yes
az containerapp delete --name claims-history-mcp --resource-group claims-assistant-rg --yes
az containerapp delete --name vin-vehicle-mcp --resource-group claims-assistant-rg --yes

Write-Host "Deleting Container Apps environment..."
az containerapp env delete --name claims-assistant-env --resource-group claims-assistant-rg --yes

Write-Host "Deleting Postgres Flexible Server..."
az postgres flexible-server delete --name claims-assistant-pg --resource-group claims-assistant-rg --yes

Write-Host "Deleting Storage account..."
az storage account delete --name claimsassistantstorage --resource-group claims-assistant-rg --yes

Write-Host "Deleting ACR..."
az acr delete --name claimsassistantacr --resource-group claims-assistant-rg --yes

Write-Host "Deleting ACR-pull managed identity..."
az identity delete --name claims-assistant-acr-pull-identity --resource-group claims-assistant-rg


Write-Host "Deleting Log Analytics workspace..."
# --force is required: without it the workspace is soft-deleted and its name stays
# reserved for 14 days, which would break the next deploy-app-infra-base.ps1 run
# (Bicep can't create a new workspace with a name still held by a soft-deleted one).
az monitor log-analytics workspace delete --workspace-name claims-assistant-logs --resource-group claims-assistant-rg --yes --force

Write-Host "Teardown complete. claims-assistant-openai and claims-assistant-search were left untouched."
