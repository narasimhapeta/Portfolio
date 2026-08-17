# tests/test_agent_framework_setup.py
from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient


def test_openai_chat_completion_client_constructs_without_network_call():
    client = OpenAIChatCompletionClient(
        model="test-deployment",
        azure_endpoint="https://example.openai.azure.com",
        api_key="test-key",
        api_version="2024-12-01-preview",
    )

    assert client.azure_endpoint == "https://example.openai.azure.com"


def test_agent_constructs_around_a_client():
    client = OpenAIChatCompletionClient(
        model="test-deployment",
        azure_endpoint="https://example.openai.azure.com",
        api_key="test-key",
    )

    agent = Agent(client=client, instructions="You are a test agent.")

    # Agent has no public `.instructions` attribute — the constructor folds it into
    # `default_options["instructions"]`, which is what actually gets sent per-call.
    assert agent.default_options["instructions"] == "You are a test agent."


def test_chat_options_is_a_plain_dict_with_response_format():
    options = ChatOptions(response_format=dict)

    assert options == {"response_format": dict}
