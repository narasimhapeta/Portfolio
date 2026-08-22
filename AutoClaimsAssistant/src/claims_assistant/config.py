# src/claims_assistant/config.py
from functools import lru_cache
from urllib.parse import quote

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    app_env: str = "local"
    postgres_host: str = "localhost"
    postgres_port: int = 5432
    postgres_db: str = "claims_assistant"
    postgres_user: str = "claims_assistant"
    postgres_password: str = "devpassword"
    postgres_ssl_mode: str = "disable"
    azure_openai_endpoint: str = ""
    azure_openai_api_key: str = ""
    azure_openai_chat_deployment: str = ""
    azure_openai_api_version: str = "2024-12-01-preview"
    azure_openai_coverage_deployment: str = ""
    azure_openai_embedding_deployment: str = ""
    azure_openai_fraud_deployment: str = ""
    azure_openai_adjuster_summary_deployment: str = ""
    azure_openai_eval_judge_primary_deployment: str = ""
    azure_openai_eval_judge_secondary_deployment: str = ""
    azure_search_endpoint: str = ""
    azure_search_api_key: str = ""
    azure_search_index_name: str = "policy-documents"
    policy_db_mcp_url: str = "http://localhost:8101/mcp"
    claims_history_mcp_url: str = "http://localhost:8102/mcp"
    vin_vehicle_mcp_url: str = "http://localhost:8103/mcp"



    @property
    def postgres_dsn(self) -> str:
        user = quote(self.postgres_user, safe="")
        password = quote(self.postgres_password, safe="")
        return f"postgresql://{user}:{password}@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"

    @property
    def postgres_async_dsn(self) -> str:
        user = quote(self.postgres_user, safe="")
        password = quote(self.postgres_password, safe="")
        return (
            f"postgresql+asyncpg://{user}:{password}"
            f"@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"
        )



@lru_cache
def get_settings() -> Settings:
    return Settings()
