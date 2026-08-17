# tests/test_config.py

from claims_assistant.config import Settings, get_settings


def test_settings_reads_from_env(monkeypatch):
    monkeypatch.setenv("APP_ENV", "test")
    monkeypatch.setenv("POSTGRES_HOST", "db.example")
    monkeypatch.setenv("POSTGRES_PORT", "5433")
    monkeypatch.setenv("POSTGRES_DB", "testdb")
    monkeypatch.setenv("POSTGRES_USER", "testuser")
    monkeypatch.setenv("POSTGRES_PASSWORD", "testpass")
    monkeypatch.setenv("AZURE_OPENAI_ENDPOINT", "https://example.openai.azure.com")
    monkeypatch.setenv("AZURE_OPENAI_API_KEY", "test-key")
    monkeypatch.setenv("AZURE_OPENAI_CHAT_DEPLOYMENT", "test-deployment")
    monkeypatch.setenv("AZURE_OPENAI_API_VERSION", "2024-12-01-preview")
    monkeypatch.setenv("AZURE_OPENAI_COVERAGE_DEPLOYMENT", "test-coverage-deployment")
    monkeypatch.setenv("AZURE_OPENAI_EMBEDDING_DEPLOYMENT", "test-embedding-deployment")
    monkeypatch.setenv("AZURE_OPENAI_FRAUD_DEPLOYMENT", "test-fraud-deployment")
    monkeypatch.setenv(
        "AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT", "test-adjuster-summary-deployment"
        )
    monkeypatch.setenv(
        "AZURE_OPENAI_EVAL_JUDGE_PRIMARY_DEPLOYMENT", "test-judge-primary-deployment"
    )
    monkeypatch.setenv(
        "AZURE_OPENAI_EVAL_JUDGE_SECONDARY_DEPLOYMENT", "test-judge-secondary-deployment"
    )
    monkeypatch.setenv("AZURE_SEARCH_ENDPOINT", "https://example.search.windows.net")
    monkeypatch.setenv("AZURE_SEARCH_API_KEY", "test-search-key")
    monkeypatch.setenv("AZURE_SEARCH_INDEX_NAME", "test-policy-documents")
    monkeypatch.setenv("POLICY_DB_MCP_URL", "http://policy-db-test:8101/mcp")
    monkeypatch.setenv("CLAIMS_HISTORY_MCP_URL", "http://claims-history-test:8102/mcp")
    monkeypatch.setenv("VIN_VEHICLE_MCP_URL", "http://vin-vehicle-test:8103/mcp")


    settings = Settings()

    assert settings.app_env == "test"
    assert settings.postgres_host == "db.example"
    assert settings.postgres_port == 5433
    assert settings.postgres_dsn == (
        "postgresql://testuser:testpass@db.example:5433/testdb"
    )
    assert settings.postgres_async_dsn == (
        "postgresql+asyncpg://testuser:testpass@db.example:5433/testdb"
    )
    assert settings.azure_openai_endpoint == "https://example.openai.azure.com"
    assert settings.azure_openai_api_key == "test-key"
    assert settings.azure_openai_chat_deployment == "test-deployment"
    assert settings.azure_openai_api_version == "2024-12-01-preview"
    assert settings.azure_openai_coverage_deployment == "test-coverage-deployment"
    assert settings.azure_openai_embedding_deployment == "test-embedding-deployment"
    assert settings.azure_openai_fraud_deployment == "test-fraud-deployment"
    assert settings.azure_openai_adjuster_summary_deployment == "test-adjuster-summary-deployment"
    assert settings.azure_openai_eval_judge_primary_deployment == "test-judge-primary-deployment"
    assert (
        settings.azure_openai_eval_judge_secondary_deployment
        == "test-judge-secondary-deployment"
    )

    assert settings.azure_search_endpoint == "https://example.search.windows.net"
    assert settings.azure_search_api_key == "test-search-key"
    assert settings.azure_search_index_name == "test-policy-documents"
    assert settings.policy_db_mcp_url == "http://policy-db-test:8101/mcp"
    assert settings.claims_history_mcp_url == "http://claims-history-test:8102/mcp"
    assert settings.vin_vehicle_mcp_url == "http://vin-vehicle-test:8103/mcp"



def test_get_settings_is_cached():
    assert get_settings() is get_settings()
    
