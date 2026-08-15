# src/claims_assistant/agents/extraction_agent.py
from __future__ import annotations

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient

from claims_assistant.agents.extraction_schema import FNOLExtraction
from claims_assistant.agents.few_shot_examples import render_few_shot_block
from claims_assistant.config import Settings

INSTRUCTIONS_TEMPLATE = """\
You are an insurance claims intake specialist. You convert a First Notice of Loss \
(FNOL) narrative — a policyholder's own description of an accident — into structured \
JSON matching the required schema exactly.

Rules:
- Extract only what the narrative states or clearly implies. Do not invent names, \
VINs, or details that are not present.
- If a VIN is not mentioned for a vehicle, leave it null.
- "injuries" is true if any injury, however minor or uncertain, is mentioned for \
anyone involved; injury_description should summarize what was said, including any \
uncertainty the narrator expressed.
- Assign each of the six confidence fields a score from 0.0 to 1.0 reflecting how \
directly the source narrative supports that field. Vague, hedged, or inferred \
information should get a lower score than information stated plainly. For example, \
"I felt a little sore, not sure if it's from the accident" should produce a lower \
injuries confidence than "the paramedics confirmed I broke my arm."

Here are examples of narratives and their correct extractions:

{few_shot_block}

Now extract the following FNOL report into the same JSON structure.
"""


def build_chat_client(settings: Settings) -> OpenAIChatCompletionClient:
    return OpenAIChatCompletionClient(
        model=settings.azure_openai_chat_deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


def build_extraction_agent(settings: Settings) -> Agent:
    client = build_chat_client(settings)
    instructions = INSTRUCTIONS_TEMPLATE.format(few_shot_block=render_few_shot_block())
    return Agent(client=client, instructions=instructions)


async def extract_fnol_facts(agent: Agent, narrative_text: str) -> FNOLExtraction:
    response = await agent.run(
        narrative_text, options=ChatOptions(response_format=FNOLExtraction)
    )
    value = response.value
    assert isinstance(value, FNOLExtraction)
    return value
