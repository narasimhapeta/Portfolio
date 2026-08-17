# src/claims_assistant/agents/few_shot_examples.py
from __future__ import annotations

from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo

# Distinct from the Phase 1 eval fixtures (data/eval_fixtures/extraction/) on purpose —
# those are held-out variants of these same categories (spec §6), used to test
# generalization, not to be echoed back in the prompt.

_MULTI_VEHICLE_PILEUP = (
    "On March 12, 2026 around 7:45 AM, I (Marcus Webb) was driving my Subaru Outback "
    "on I-95 in Providence, RI during heavy fog when the car ahead of me braked "
    "suddenly. I couldn't stop in time and hit it, and then a box truck behind me hit "
    "my rear bumper, pushing me further into the car ahead. Three vehicles total were "
    "involved. The driver of the car I hit, Priya Nair, said her neck hurt but she "
    "could move it fine. The box truck driver, Sam Ostrowski, seemed unhurt. No one "
    "else was injured.",
    FNOLExtraction(
        facts=FNOLFacts(
            incident_datetime="2026-03-12T07:45",
            location="I-95, Providence, RI",
            parties=[
                Party(role="policyholder", name="Marcus Webb"),
                Party(role="other_driver", name="Priya Nair"),
                Party(role="other_driver", name="Sam Ostrowski"),
            ],
            vehicles=[
                VehicleInfo(role="policyholder_vehicle", description="Subaru Outback"),
                VehicleInfo(
                    role="other_vehicle",
                    description="car driven by Priya Nair, struck from behind by policyholder",
                ),
                VehicleInfo(
                    role="other_vehicle",
                    description=(
                        "box truck driven by Sam Ostrowski, struck policyholder's vehicle "
                        "from behind"
                    ),
                ),
            ],
            injuries=True,
            injury_description=(
                "Priya Nair reported neck pain but retained range of motion; Sam Ostrowski "
                "and the policyholder were not injured."
            ),
            narrative_summary=(
                "Three-vehicle chain-reaction collision on I-95 in Providence, RI during "
                "heavy fog; policyholder's Subaru Outback struck the vehicle ahead after it "
                "braked suddenly, then was struck from behind by a box truck. One other "
                "driver reported minor neck pain."
            ),
        ),
        confidence=FieldConfidence(
            incident_datetime=0.95,
            location=0.9,
            parties=0.85,
            vehicles=0.85,
            injuries=0.8,
            narrative_summary=0.9,
        ),
    ),
)

_HIT_AND_RUN = (
    "On April 2, 2026 at about 6:30 AM, I (Denise Ochoa) was driving my Kia Sportage "
    "on Route 9 in Poughkeepsie, NY when an SUV I couldn't identify merged into my "
    "lane and clipped my front fender before speeding away. I didn't get a plate "
    "number and there were no witnesses nearby. I wasn't hurt.",
    FNOLExtraction(
        facts=FNOLFacts(
            incident_datetime="2026-04-02T06:30",
            location="Route 9, Poughkeepsie, NY",
            parties=[Party(role="policyholder", name="Denise Ochoa")],
            vehicles=[
                VehicleInfo(role="policyholder_vehicle", description="Kia Sportage"),
                VehicleInfo(
                    role="other_vehicle",
                    description="unidentified SUV, fled scene, no plate captured",
                ),
            ],
            injuries=False,
            narrative_summary=(
                "Hit-and-run sideswipe on Route 9, Poughkeepsie, NY; an unidentified SUV "
                "merged into the policyholder's lane, clipped the front fender, and sped off "
                "without a plate number captured. No witnesses, no injuries."
            ),
        ),
        confidence=FieldConfidence(
            incident_datetime=0.95,
            location=0.9,
            parties=0.9,
            vehicles=0.75,
            injuries=0.95,
            narrative_summary=0.9,
        ),
    ),
)

_AMBIGUOUS_FAULT = (
    "On May 18, 2026 around 4:00 PM, I (Grant Okafor) was merging onto the ramp for "
    "Highway 101 near San Jose, CA when my Mazda CX-5 collided with a Chevy Malibu "
    "driven by Renee Castillo. We're not totally sure who had the right of way — I "
    "thought I had space to merge, but Renee says she was already in the lane. "
    "There's damage to both cars' sides. No injuries reported by either of us.",
    FNOLExtraction(
        facts=FNOLFacts(
            incident_datetime="2026-05-18T16:00",
            location="Highway 101 on-ramp, San Jose, CA",
            parties=[
                Party(role="policyholder", name="Grant Okafor"),
                Party(role="other_driver", name="Renee Castillo"),
            ],
            vehicles=[
                VehicleInfo(role="policyholder_vehicle", description="Mazda CX-5"),
                VehicleInfo(
                    role="other_vehicle", description="Chevy Malibu driven by Renee Castillo"
                ),
            ],
            injuries=False,
            narrative_summary=(
                "Side-swipe collision while merging onto the Highway 101 on-ramp near San "
                "Jose, CA; fault is disputed, with both the policyholder and the other "
                "driver believing they had the right of way. No injuries reported."
            ),
        ),
        confidence=FieldConfidence(
            incident_datetime=0.9,
            location=0.85,
            parties=0.9,
            vehicles=0.9,
            injuries=0.9,
            narrative_summary=0.7,
        ),
    ),
)

_AMBIGUOUS_INJURY = (
    "On June 30, 2026 around 2:10 PM, I (Yuki Tanaka) was stopped in traffic on "
    "Peachtree St in Atlanta, GA when a Ford Escape driven by Cody Lindgren bumped "
    "into the back of my Nissan Altima. It was a light tap, no visible damage, but I "
    "felt a little sore in my lower back afterward. I'm not sure if it's from the "
    "accident or just from sitting in the car all day. I haven't seen a doctor.",
    FNOLExtraction(
        facts=FNOLFacts(
            incident_datetime="2026-06-30T14:10",
            location="Peachtree St, Atlanta, GA",
            parties=[
                Party(role="policyholder", name="Yuki Tanaka"),
                Party(role="other_driver", name="Cody Lindgren"),
            ],
            vehicles=[
                VehicleInfo(role="policyholder_vehicle", description="Nissan Altima"),
                VehicleInfo(
                    role="other_vehicle", description="Ford Escape driven by Cody Lindgren"
                ),
            ],
            injuries=True,
            injury_description=(
                "Policyholder reported mild lower-back soreness after the collision but was "
                "uncertain whether it was related to the accident; no medical care sought."
            ),
            narrative_summary=(
                "Minor rear-end tap on Peachtree St, Atlanta, GA with no visible vehicle "
                "damage; policyholder reports uncertain, mild lower-back soreness not yet "
                "evaluated by a doctor."
            ),
        ),
        confidence=FieldConfidence(
            incident_datetime=0.95,
            location=0.9,
            parties=0.9,
            vehicles=0.9,
            injuries=0.55,
            narrative_summary=0.85,
        ),
    ),
)

FEW_SHOT_EXAMPLES: list[tuple[str, FNOLExtraction]] = [
    _MULTI_VEHICLE_PILEUP,
    _HIT_AND_RUN,
    _AMBIGUOUS_FAULT,
    _AMBIGUOUS_INJURY,
]


def render_few_shot_block() -> str:
    sections = []
    for i, (narrative, extraction) in enumerate(FEW_SHOT_EXAMPLES, start=1):
        sections.append(
            f"Example {i}:\nFNOL Report:\n{narrative}\n\n"
            f"Extracted JSON:\n{extraction.model_dump_json(indent=2)}"
        )
    return "\n\n".join(sections)
