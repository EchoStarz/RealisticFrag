# RealisticFrag — ballistics methodology

This document explains how RealisticFrag arrives at the specific numbers in `config.json` so that future maintainers and curious users can audit, challenge, and contribute to the data set with confidence. Every value in the configuration file traces back to a documented source or a documented derivation, and the goal of this document is to make that traceability easy to follow.

## What we tune

For each ammo entry, RealisticFrag potentially writes three fields onto the round's `TemplateItem.Properties` and consumes a fourth at runtime via the v2 Harmony patch. The first three are server-side data overrides that take effect at boot:

| Field | Range | Meaning |
|---|---|---|
| `FragmentationChance` | 0.0 – 1.0 | Probability the round fragments on a tissue hit |
| `MinFragmentsCount`   | int ≥ 0   | Floor of fragment count when fragmentation rolls true |
| `MaxFragmentsCount`   | int ≥ Min | Ceiling of fragment count when fragmentation rolls true |

EFT rolls fragmentation per impact, and when the roll succeeds, additional damage is distributed across the body zones surrounding the hit. The fourth field, `MinimumVelocity`, is consumed by the client-side Harmony patch rather than by EFT directly — it sets the velocity (in m/s) below which fragmentation is suppressed for that specific impact. Vanilla EFT's fragmentation roll is binary and not velocity-aware, and that velocity-aware gate is what the v2 patch adds.

## Methodology — fragmentation chances

The starting point for every value in the configuration is what the round actually does in real-world wound-ballistics testing. This is not a balancing exercise; it is a translation exercise. We are trying to make each round in EFT behave the way its real-world counterpart behaves within the engagement velocity range that Tarkov players actually shoot at — typically point-blank to roughly 150 meters. Because EFT's vanilla fragmentation system is binary rather than velocity-aware, the v1 server-side values implicitly assume "typical SPT engagement distance" as the calibration point. A round that real-world testing shows fragments only at supersonic velocities gets a moderate v1 `FragmentationChance` rather than a high one, because at long range it would not actually fragment, and EFT can't model that velocity dependency on its own. The v2 client patch is what closes that gap — once it's installed, the same round can be given a higher base chance combined with an explicit velocity threshold, and the system as a whole produces the right behavior at every range.

Within each caliber, ammo is grouped into broad behavior tiers, and the tiers map roughly onto chance ranges. High-frag rounds — the M193s of the world, plus 5.45 BS at high velocity and similar designs that yaw and break up violently in tissue — sit somewhere between 0.65 and 0.85 base chance. Medium-frag rounds, where fragmentation is real but unreliable and depends heavily on hit angle and velocity, fall between 0.30 and 0.50; M855 and 7.62×39 PS are typical members. Low-frag rounds, mostly AP and steel-core designs that punch through cleanly because their construction is built around penetration rather than deformation, sit between 0.05 and 0.20 — they can fragment, especially when they hit bone, but it's the exception rather than the rule. And the lowest tier, at exactly 0.0, captures rounds that physically cannot fragment in any meaningful way: pistol rounds that lack the velocity, slugs that penetrate or deform without breaking up, hollow points that expand instead of fragmenting, and most buckshot. Setting these to zero rather than omitting the entry is a deliberate choice — the explicit value in the configuration documents that the round was considered and intentionally left non-fragmenting, which keeps future maintainers from assuming the entry was simply forgotten.

The fragment-count fields, `MinFragmentsCount` and `MaxFragmentsCount`, correlate loosely with bullet mass and fragmentation violence. M193, light and fast and known for breaking apart at the cannelure, gets four to seven fragments. M855, heavier and more controlled in its breakup, gets two to four. AP rounds that occasionally shed a flake of steel get one or two fragments at most. These fields are optional in the schema; when omitted, the vanilla EFT values are preserved.

## Sources for fragmentation chances

The wound-ballistics literature most directly relevant to military ammunition behavior is Martin Fackler's body of work — his *Gunshot Wound Reviews* and the broader IWBA research collection — which is the foundational reference for understanding how rounds interact with tissue. For 5.56 specifically, the Brassfit and m4carbine.net frag-fleet tables are the empirical companion: they record fragmentation rates measured by testers across a range of barrel lengths and impact distances, and they are the source for most of our specific 5.56 thresholds. Fontaine's Realism Mod 1.6.4 ammo configurations are a third major source — Fontaine encoded years of careful research into per-round behavior, and where the modeling already matched what Fackler or the m4carbine community would have produced, RealisticFrag ports the values directly with attribution. AmmoOracle and similar consumer-facing wound-ballistics summaries are useful for pistol calibers and less-documented rounds, where rigorous published data is sparse. Wikipedia and military doctrine documents serve as supplementary references for terminal-ballistic descriptions — the design intent behind M855A1's enhanced penetrator, for instance, but never as a stand-alone source for an actual numeric value.

## Per-round rationale

The 5.56 family is the most thoroughly documented and serves as the calibration anchor for the rest of the configuration. M193 (`59e6920f86f77411d82aa167`) carries a `FragmentationChance` of 0.75 with a fragment count of four to seven, reflecting the round's well-known reputation for yawing within roughly six centimeters of penetration at supersonic velocity and breaking apart at the cannelure into four to seven pieces. The v2 velocity gate sits at 800 m/s, which is just below the round's published frag-onset velocity of approximately 2625 fps; in EFT's typical engagement ranges most M193 shots will impact above that threshold, so the gate fires only at long range. M855 (`54527a984bdc2d4e668b4567`) gets a much lower base chance of 0.35 because its steel-core construction makes fragmentation substantially less reliable than M193's lead-core design; vanilla's 0.5 is overgenerous when measured against frag-fleet data. The v2 threshold sits at 823 m/s, corresponding to the published 2700 fps frag-onset for M855. M855A1 (`54527ac44bdc2d36668b4567`) sits between the two at 0.55 base chance, because the bonded design was specifically engineered to produce more consistent terminal performance than M855 at lower velocities; its v2 threshold of 750 m/s reflects that intentional design choice.

Future calibers will be added to this section as the per-round rationale is documented, but the underlying methodology is the same in all cases: the value comes from a documented source, the source is named, and the reasoning for how the data point translates into RealisticFrag's number is explained.

## Velocity thresholds — published vs derived

Each round's `MinimumVelocity` is determined by one of two paths, chosen automatically by `scripts/compute-thresholds.py`. The first path is for rounds where the wound-ballistics literature has a published frag-onset velocity. These are hard-coded into the script's `KNOWN_THRESHOLDS` dictionary with per-round source comments, and they cover roughly fifty entries across the canonical 5.56 NATO, 5.45, 7.62×39, 7.62×51, 7.62×54R, 9×19, 9×39, and .300 Blackout families. A representative selection looks like this:

| Round | Threshold (m/s) | Source rationale |
|---|---|---|
| 5.56 M193 | 800 | Yaws and fragments at 2625 fps muzzle velocity per Fackler / m4carbine |
| 5.56 M855 | 823 | Frag-onset at 2700 fps per the m4carbine frag-fleet table |
| 5.56 M855A1 | 750 | Bonded design intentionally lowers frag-onset velocity |
| 5.56 M995 (AP) | 880 | Steel-core construction; rarely fragments below near-muzzle velocity |
| 5.45 BP / BS | 830 – 850 | AP variants — high threshold appropriate to steel-core construction |
| 9×39 (all variants) | 280 | Subsonic by design — fragmentation possible only at the muzzle |
| 9×19 7N31 | 380 | High-pressure +P+ AP loading runs hotter than standard 9mm |

The second path is a derivation formula, used for the rest of the rounds where the literature does not provide a published frag-onset value. The formula takes four inputs from Realism's data fields — `InitialSpeed`, `BulletMassGram`, `PenetrationPower`, and `HeavyBleedingDelta` — and uses them as proxies to infer the round's construction class, which then determines the velocity threshold as a percentage of the round's muzzle velocity. The decision tree, in the order it's applied:

```
subsonic projectile (InitialSpeed < 340 m/s)        → 95% of muzzle (point-blank only)
AP-like (PenetrationPower / mass > 20)              → 92% of muzzle
frangible (FragmentationChance > 0.6)               → 60% of muzzle
HP / soft-point (HeavyBleedingDelta > 0.45)         → 72% of muzzle
modern hybrid bonded (HeavyBleed > 0.30
                     AND PenPower / mass > 17)      → 80% of muzzle
standard rifle FMJ (InitialSpeed > 700)             → 85% of muzzle
pistol cartridge (anything else)                    → 92% of muzzle
```

The multipliers were calibrated so that running the canonical 5.56 rounds — M193, M855, M855A1 — through the formula reproduces their published thresholds within ±3%. That calibration makes the formula a reasonable default for less-documented rounds in the same construction class: the system effectively says "if you behave like an AP round in Realism's data, you fragment at AP-round thresholds."

## Coverage summary

The configuration ships 169 ammo overrides total. Of those, 111 carry a `MinimumVelocity` value: roughly fifty came from the published-threshold path and roughly sixty-one from the derivation formula. The remaining 58 entries fall into two categories. Fifty-one are explicitly set to `FragmentationChance: 0` by Realism's source data, which means the v2 velocity gate has nothing to suppress and adding a `MinimumVelocity` would be a no-op; these are intentionally left without a threshold. The remaining seven lack sufficient Realism props for the formula to produce a meaningful output, and they are flagged in the script's output for manual review.

## Reviewing or contributing

If a value seems wrong — and at this scale, some values almost certainly are — the contribution path is straightforward: file an issue or open a pull request that lists the ammo template ID, the current value, your proposed value, and a citation that justifies the change. The citation should come from one of the source categories described earlier in this document or an equivalent. The `CONTRIBUTING.md` file walks through the full PR template and the source-quality requirements.

## When to override the formula

If raid testing reveals a round whose formula-derived threshold feels wrong — fragmenting too aggressively at long range, or not at all at short range when it clearly should — the fix is to add an explicit entry for that round in `KNOWN_THRESHOLDS` at the top of `scripts/compute-thresholds.py`, with a one-line citation explaining the source for the new value. Re-running the script then writes the hardcoded value into `config.json`, where it takes precedence over what the formula would have produced. This is the intended workflow for refining the data set over time, and pull requests of this shape are warmly welcomed.

## Limitations to know about

A few honest caveats are worth keeping in mind. First, vanilla EFT's velocity model is itself an approximation: the engine computes per-frame velocity decay using a simplified drag formulation derived from the round's `BallisticCoefficient` field, which is less accurate than real-world ballistics tables. RealisticFrag's thresholds match the real frag-onset velocity, but the *distance* at which a bullet drops below threshold inside EFT may not exactly match the real-world distance. Second, the 51 entries with `FragmentationChance: 0` are unaffected by the velocity gate by design — if the base chance is zero, gating cannot suppress fragmentation that wasn't going to happen. Whether each of those rounds *should* be set to zero is a separate question, and `CONTRIBUTING.md` documents the path for proposing a non-zero base chance with sources. Third, the formula assumes Realism's data fields are correct and meaningful for each round, which is generally true but not universally so; the seven entries currently lacking thresholds are mostly cases where Realism's props were sparse or absent, and they should be revisited as the data set matures.
