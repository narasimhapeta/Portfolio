# scripts/iac/create-platform-manual.ps1
# Manual alternative to `az deployment group create --template-file iac/platform.bicep`.
# Creates claims-assistant-openai (+ 7 model deployments) and claims-assistant-search
# via individual imperative `az` commands instead of one Bicep deployment group create.
# Written after platform.bicep's normal deploy path tripped Azure's real-time fraud
# protection (RTFP, error 715-123420) on OpenAI account creation; individual
# `az cognitiveservices account create` calls did not trip the same flag (2026-08-24).
# Safe to re-run: each `az ... create`/`deployment create` call is idempotent against
# an already-existing resource of matching name/config (no-op update, not an error).
# Assumes the resource group already exists: az group create --name claims-assistant-rg --location eastus2

$ErrorActionPreference = "Stop"

Write-Host "Creating Azure OpenAI account..."
az cognitiveservices account create `
  --name claims-assistant-openai `
  --resource-group claims-assistant-rg `
  --location eastus2 `
  --kind OpenAI `
  --sku S0 `
  --custom-domain claims-assistant-openai `
  --yes

Write-Host "Creating model deployments..."
$deployments = @(
    @{ Name = "extraction-agent";       Model = "gpt-5.4-mini";           Version = "2026-03-17" }
    @{ Name = "coverage-agent";         Model = "gpt-5.4";                Version = "2026-03-05" }
    @{ Name = "policy-embeddings";      Model = "text-embedding-3-small"; Version = "1" }
    @{ Name = "fraud-risk-agent";       Model = "gpt-5.5";                Version = "2026-04-24" }
    @{ Name = "adjuster-summary-agent"; Model = "gpt-5.4-mini";           Version = "2026-03-17" }
    @{ Name = "eval-judge-primary";     Model = "gpt-5.5";                Version = "2026-04-24" }
    @{ Name = "eval-judge-secondary";   Model = "gpt-4.1";                Version = "2025-04-14" }
)

foreach ($d in $deployments) {
    Write-Host "  Deploying $($d.Name) ($($d.Model) $($d.Version))..."
    az cognitiveservices account deployment create `
      --name claims-assistant-openai `
      --resource-group claims-assistant-rg `
      --deployment-name $d.Name `
      --model-name $d.Model `
      --model-version $d.Version `
      --model-format OpenAI `
      --sku-name GlobalStandard `
      --sku-capacity 10
}

Write-Host "Creating Azure AI Search service..."
az search service create `
  --name claims-assistant-search `
  --resource-group claims-assistant-rg `
  --location centralus `
  --sku free `
  --partition-count 1 `
  --replica-count 1

Write-Host "Platform resources created. Next steps (not automated by this script):"
Write-Host "  uv run pytest tests/test_indexer.py -v -m integration   # re-index the policy corpus"
Write-Host "  az cognitiveservices account keys list --name claims-assistant-openai --resource-group claims-assistant-rg --query key1 -o tsv"
Write-Host "  az search admin-key show --service-name claims-assistant-search --resource-group claims-assistant-rg --query primaryKey -o tsv"
Write-Host "  # update .env and the two GitHub secrets (AZURE_OPENAI_API_KEY, AZURE_SEARCH_API_KEY) with the keys above"
