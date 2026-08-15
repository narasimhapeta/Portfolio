# tests/test_few_shot_examples.py
from claims_assistant.agents.few_shot_examples import FEW_SHOT_EXAMPLES, render_few_shot_block


def test_four_few_shot_examples_are_defined():
    assert len(FEW_SHOT_EXAMPLES) == 4


def test_few_shot_examples_cover_the_required_categories():
    narratives = " ".join(narrative.lower() for narrative, _ in FEW_SHOT_EXAMPLES)

    assert "box truck" in narratives  # multi-vehicle pileup
    assert (
        "sped off" in narratives or "fled" in narratives or "speeding away" in narratives
    )  # hit-and-run, no other party
    assert "right of way" in narratives  # ambiguous fault language
    assert "sore" in narratives  # ambiguous injury mention


def test_render_few_shot_block_includes_every_example_narrative():
    block = render_few_shot_block()

    for narrative, _ in FEW_SHOT_EXAMPLES:
        assert narrative in block


def test_render_few_shot_block_includes_expected_json_output():
    block = render_few_shot_block()

    assert '"role": "policyholder"' in block
    assert '"confidence"' in block
