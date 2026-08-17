# src/claims_assistant/policy_documents.py
from __future__ import annotations

STATE_MINIMUMS: dict[str, dict[str, int]] = {
    "CA": {"bi_per_person": 15_000, "bi_per_accident": 30_000, "property_damage": 5_000},
    "TX": {"bi_per_person": 30_000, "bi_per_accident": 60_000, "property_damage": 25_000},
    "NY": {"bi_per_person": 25_000, "bi_per_accident": 50_000, "property_damage": 10_000},
}

STATE_ENDORSEMENTS: dict[str, str] = {
    "CA": (
        "This policy's premium has been calculated and filed in accordance with "
        "California Proposition 103. Rate changes greater than 6.9% require prior "
        "approval from the California Department of Insurance."
    ),
    "TX": (
        "Uninsured/Underinsured Motorist Coverage (UM/UIM) is included at the same "
        "limits as Bodily Injury Liability unless rejected in writing by the "
        "policyholder, per Texas Insurance Code Sec. 1952.101."
    ),
    "NY": (
        "This policy includes No-Fault Personal Injury Protection (PIP) coverage of "
        "$50,000 per person for basic economic loss, regardless of fault, per New "
        "York Insurance Law Article 51."
    ),
}

TIER_TEXT: dict[str, dict[str, object]] = {
    "liability_only": {
        "label": "Liability Only",
        "collision": None,
        "comprehensive": None,
        "summary": (
            "This policy provides Bodily Injury Liability and Property Damage "
            "Liability coverage only. It does NOT cover damage to the "
            "policyholder's own vehicle from any cause, including collision, "
            "theft, fire, or weather."
        ),
    },
    "full_coverage": {
        "label": "Full Coverage",
        "collision": "$500 deductible",
        "comprehensive": "$500 deductible",
        "summary": (
            "This policy provides Bodily Injury Liability and Property Damage "
            "Liability at the state-mandated minimum limits, plus Collision and "
            "Comprehensive coverage for the policyholder's own vehicle, each "
            "subject to a $500 deductible."
        ),
    },
    "comprehensive_collision": {
        "label": "Comprehensive/Collision (Premium)",
        "collision": "$250 deductible",
        "comprehensive": "$100 deductible",
        "summary": (
            "This policy provides Bodily Injury Liability and Property Damage "
            "Liability at 2x the state-mandated minimum limits, plus Collision "
            "coverage ($250 deductible) and Comprehensive coverage ($100 "
            "deductible) for the policyholder's own vehicle."
        ),
    },
}


def render_policy_document(state: str, tier: str) -> str:
    form_id = f"{state}-{tier.upper().replace('_', '-')}"
    minimums = STATE_MINIMUMS[state]
    tier_info = TIER_TEXT[tier]
    bi_pp = minimums["bi_per_person"]
    bi_pa = minimums["bi_per_accident"]
    pd = minimums["property_damage"]
    if tier == "comprehensive_collision":
        bi_pp *= 2
        bi_pa *= 2
        pd *= 2

    lines = [
        f"# Auto Insurance Policy — {form_id}",
        "",
        f"**Coverage Tier:** {tier_info['label']}",
        f"**State:** {state}",
        "",
        "## Section 1. Definitions",
        "",
        "Sec. 1.1 \"Policyholder\" means the named insured on the declarations page.",
        "Sec. 1.2 \"Covered Vehicle\" means a vehicle listed on the declarations page.",
        "Sec. 1.3 \"Accident\" means a sudden, unintended event causing bodily "
        "injury or property damage.",
        "",
        "## Section 2. Liability Coverage",
        "",
        f"Sec. 2.1 Bodily Injury Liability: ${bi_pp:,} per person / ${bi_pa:,} per "
        "accident.",
        f"Sec. 2.2 Property Damage Liability: ${pd:,} per accident.",
        "Sec. 2.3 This coverage pays for injury or damage the Policyholder causes "
        "to others. It does not cover the Policyholder's own vehicle.",
        "",
        "## Section 3. Physical Damage Coverage",
        "",
    ]

    if tier_info["collision"] is None:
        lines.append(
            "Sec. 3.1 This policy does NOT include Collision or Comprehensive "
            "coverage."
        )
    else:
        lines.append(
            f"Sec. 3.1 Collision Coverage: pays for damage to the Covered Vehicle "
            f"from a collision, subject to a {tier_info['collision']}."
        )
        lines.append(
            f"Sec. 3.2 Comprehensive Coverage: pays for damage to the Covered "
            f"Vehicle from non-collision causes (theft, fire, weather, "
            f"vandalism), subject to a {tier_info['comprehensive']}."
        )

    lines += [
        "",
        "## Section 4. Exclusions",
        "",
        "Sec. 4.1 This policy does not cover damage or injury that occurs while "
        "the Covered Vehicle is being used to carry persons or property for a "
        "fee (ride-share or delivery use), unless a commercial-use endorsement "
        "has been added.",
        "Sec. 4.2 This policy does not cover intentional damage caused by the "
        "Policyholder.",
        "Sec. 4.3 This policy does not cover damage that occurred before the "
        "Effective Date or after the Expiration Date on the declarations page.",
        "",
        "## Section 5. Claims Filing Procedures",
        "",
        "Sec. 5.1 The Policyholder must report a claim within 30 days of the "
        "Accident.",
        "Sec. 5.2 The Policyholder must cooperate with the claims investigation, "
        "including providing a written statement and access to the Covered "
        "Vehicle for inspection.",
        "",
        "## Section 6. State-Specific Endorsement",
        "",
        f"Sec. 6.1 {STATE_ENDORSEMENTS[state]}",
        "",
        "## Summary",
        "",
        str(tier_info["summary"]),
        "",
    ]
    return "\n".join(lines)


def all_policy_forms() -> dict[str, str]:
    states = ["CA", "TX", "NY"]
    tiers = ["liability_only", "full_coverage", "comprehensive_collision"]
    return {
        f"{state}-{tier.upper().replace('_', '-')}": render_policy_document(state, tier)
        for state in states
        for tier in tiers
    }
