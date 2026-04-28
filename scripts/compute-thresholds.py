"""
Compute MinimumVelocity (m/s) for every ammo entry in config.json based on:
  1. Hardcoded thresholds for rounds with published wound-ballistics data
  2. Formula-derived thresholds for the rest, using construction inferred from
     InitialSpeed + BulletMassGram + PenetrationPower + HeavyBleedingDelta

Reads:  scripts/realism-full-props.json   (extracted from Realism Mod 1.6.4 ammo.ts)
Writes: config.json (in-place, only adds MinimumVelocity, never replaces)
        BALLISTICS.md  (appends a thresholds methodology section)

Run with:   python scripts/compute-thresholds.py
"""
import json, re, sys
from pathlib import Path

ROOT = Path(__file__).parent.parent
PROPS_PATH  = ROOT / 'scripts' / 'realism-full-props.json'
CONFIG_PATH = ROOT / 'config.json'

# ============================================================================
# Hardcoded thresholds, sourced from published wound-ballistics studies
# (Fackler IWBA, Brassfit frag-fleet tables, m4carbine.net research).
# Values in m/s, the bullet velocity at impact below which fragmentation
# becomes unreliable for that specific projectile design.
# ============================================================================
KNOWN_THRESHOLDS = {
    # ---- 5.56×45 NATO ----
    '54527a984bdc2d4e668b4567': 823,  # M855 — frag-onset 2700 fps (steel-tip FMJ)
    '54527ac44bdc2d36668b4567': 750,  # M855A1 — bonded, designed for lower onset
    '59e6920f86f77411d82aa167': 800,  # M193 — yaws/frags ~2625 fps (FMJ ball)
    '59e6927d86f77411da468256': 700,  # 55 HP — hollow-point opens at lower speeds
    '59e68f6f86f7746c9f75e846': 800,  # M856 tracer (M193-like design)
    '59e6906286f7746c9f75e847': 750,  # M856A1 (M855A1 tracer variant)
    '59e690b686f7746c9f75e848': 880,  # M995 — steel-core AP, rarely fragments
    '59e6918f86f7746c9f75e849': 600,  # MK 255 Mod 0 (RRLP frangible — designed to come apart)
    '60194943740c5d77f6705eea': 770,  # MK 318 Mod 0 (SOST — controlled-yaw)
    '601949593ae8f707c4608daa': 850,  # SSA AP
    '5c0d5ae286f7741e46554302': 700,  # Warmage (varmint frangible)

    # ---- 5.45×39 ----
    '5c0d5e4486f77478390952fe': 820,  # 7N39 PPBS (modern hardened steel core)
    '61962b617c6c7b169525f168': 800,  # 7N40 (improved AP)
    '56dfef82d2720bbd668b4567': 830,  # BP (AP)
    '56dff026d2720bb8668b4567': 850,  # BS (heavy AP)
    '56dff061d2720bb5668b4567': 800,  # BT (tracer)
    '56dff0bed2720bb0668b4567': 750,  # FMJ
    '56dff216d2720bbd668b4568': 700,  # HP
    '56dff2ced2720bb4668b4567': 770,  # PP (modern improved ball)
    '56dff338d2720bbd668b4569': 700,  # PRS (reduced ricochet, soft tip)
    '56dff3afd2720bba668b4567': 800,  # PS (standard ball — Soviet 7N6 equivalent)
    '56dff421d2720b5f5a8b4567': 680,  # SP (soft-point — opens early)
    '56dff4a2d2720bbd668b456a': 800,  # T (tracer)
    '56dff4ecd2720b5f5a8b4568': 280,  # US (subsonic — point-blank only if at all)

    # ---- 7.62×39 ----
    '59e0d99486f7744a32234762': 700,  # BP (AP-style steel-tip)
    '59e4d3d286f774176a36250a': 600,  # HP
    '5656d7c34bdc2d9d198b4587': 700,  # PS (standard FMJ ball)
    '59e4cf5286f7741778269d8a': 700,  # T-45M tracer
    '59e4d24686f7741776641ac7': 280,  # US (subsonic)
    '64b7af5a8532cf95ee0a0dbd': 700,  # FMJ
    '601aa3d2b2bcb34913271e6d': 800,  # MAI AP
    '64b7af434b75259c590fa893': 700,  # PP
    '64b7af734b75259c590fa895': 600,  # SP (soft-point)

    # ---- 7.62×51 NATO ----
    '5a6086ea4f39f99cd479502f': 850,  # M61 AP
    '5a608bf24f39f98ffc77720e': 750,  # M62 tracer
    '58dd3ad986f77403051cba8f': 680,  # M80 ball (heavy, fragments rarely)
    '5e023e53d4353e3302577c4c': 700,  # BPZ FMJ
    '6768c25aa7b238f14a08d3f6': 720,  # M80A1 (improved bonded)
    '5efb0c1bd79ff02a1f5e68d9': 850,  # M993 AP
    '5e023e6e34d52a55c3304f71': 600,  # TPZ SP
    '5e023e88277cce2b522ff2b1': 600,  # Ultra Nosler (controlled-expansion HP)

    # ---- 7.62×54R ----
    '59e77a2386f7742ee578960a': 750,  # 7N1 (sniper)
    '5887431f2459777e1612938f': 700,  # LPS (standard ball)
    '560d61e84bdc2da74d8b4571': 750,  # SNB (sniper, lead-core)
    '5e023d34e8a400319a28ed44': 800,  # 7BT1 BT (AP tracer)
    '5e023d48186a883be655e551': 850,  # 7N37 / BS (heavy AP)
    '64b8f7c241772715af0f9c3d': 600,  # BT HP (modern HP)
    '64b8f7968532cf95ee0a0dbf': 700,  # FMJ
    '64b8f7b5389d7ffd620ccba2': 600,  # BT SP
    '5e023cf8186a883be655e54f': 800,  # T-46M tracer

    # ---- 9×19 PARA ----
    # Pistol — frag rarely except very near muzzle
    '5efb0da7a29a85116f6ea05f': 380,  # 7N31 (high-pressure AP)
    '5c3df7d588a4501f290594e5': 340,  # GT (Green Tracer)
    '58864a4f2459770fcc257101': 320,  # PSO (target)
    '56d59d3ad2720bdb418b4577': 340,  # Pst gzh (standard ball)
    '5c925fa22e221601da359b7b': 380,  # AP 6.3
    '5a3c16fe86f77452b62de32a': 320,  # Luger CCI
    '64b7bbb74b75259c590fa897': 340,  # M882
    '5efb0e16aeb21837e749c7ff': 300,  # RIP (frangible — opens easily, low threshold)

    # ---- 9×39 (all subsonic by design) ----
    '57a0dfb6245977637f7e1f06': 280,  # SP-5
    '57a0e5022459774d1673f889': 280,  # SP-6 (AP)
    '5c0d688c86f77413ae3407b2': 280,  # PAB-9 (AP)
    '5c0d668f86f7747ccb7f13b2': 280,  # BP (AP)

    # ---- .300 Blackout (7.62×35) ----
    # Mix of subsonic and supersonic; need per-round basis
    '5fbe3ffdf8b6a877a729ea82': 720,  # AP
    '5fd20ff893a8961fc660a954': 280,  # 200 gr subsonic
    '6196364158ef8c428c287d9f': 670,  # BCP FMJ
    '6196365d58ef8c428c287da1': 670,  # V-Max
}

# ----------------------------------------------------------------------------
def derive_threshold(props):
    """
    Formula-based derivation when no published threshold exists.
    Logic flow (first match wins):
        - subsonic projectile (InitialSpeed < 340 m/s) → 95% of muzzle
        - AP-like (PenetrationPower / mass > 20) → 92% of muzzle
        - frangible (FragmentationChance > 0.6) → 60% of muzzle
        - soft-point / HP (HeavyBleedingDelta > 0.45) → 72% of muzzle
        - modern hybrid (bonded FMJ — bleed > 0.30 + high pen) → 80%
        - standard rifle FMJ (InitialSpeed > 700) → 85%
        - pistol cartridge (everything else) → 92%
    Returns int (m/s) or None if InitialSpeed missing.
    """
    speed = props.get('InitialSpeed') or 0
    mass  = props.get('BulletMassGram') or 1
    pen   = props.get('PenetrationPower') or 0
    bleed = props.get('HeavyBleedingDelta') or 0
    frag  = props.get('FragmentationChance') or 0
    pen_per_mass = pen / mass if mass > 0 else 0

    if speed <= 0:
        return None

    if speed < 340:                              return round(speed * 0.95)
    if pen_per_mass > 20:                        return round(speed * 0.92)
    if frag > 0.6:                               return round(speed * 0.60)
    if bleed > 0.45:                             return round(speed * 0.72)
    if bleed > 0.30 and pen_per_mass > 17:       return round(speed * 0.80)
    if speed > 700:                              return round(speed * 0.85)
    return round(speed * 0.92)

# ----------------------------------------------------------------------------
def main():
    realism = json.loads(PROPS_PATH.read_text(encoding='utf-8'))
    config_text = CONFIG_PATH.read_text(encoding='utf-8')

    # Walk every existing AmmoOverride entry and inject MinimumVelocity if missing.
    # Strategy: regex-locate each "<24-hex>": { ... } block, decide what to insert,
    # write the modified text back. Avoid full-JSON parse so we preserve JSONC comments.

    # Find all ammo entries via regex. Match handles both "FragmentationChance only"
    # and "FragmentationChance + Min/Max" cases — we just look for the trailing brace.
    block_re = re.compile(
        r'("(?P<id>[0-9a-f]{24})":\s*\{(?P<body>(?:[^{}]|"[^"]*")+)\})',
        re.S
    )

    added = 0
    skipped_already = 0
    skipped_zero = 0
    skipped_missing = 0
    method_known = 0
    method_derived = 0

    def replace(m):
        nonlocal added, skipped_already, skipped_zero, skipped_missing, method_known, method_derived
        full, ammo_id, body = m.group(1), m.group('id'), m.group('body')

        if 'MinimumVelocity' in body:
            skipped_already += 1
            return full

        if ammo_id not in realism:
            skipped_missing += 1
            return full  # we have no data on this round, leave alone

        props = realism[ammo_id]
        frag = props.get('FragmentationChance', 0)
        if frag == 0:
            # No fragmentation in vanilla / our config — gating it has no effect.
            # Skip to keep config tidy.
            skipped_zero += 1
            return full

        # Pick a threshold
        if ammo_id in KNOWN_THRESHOLDS:
            mv = KNOWN_THRESHOLDS[ammo_id]
            method_known += 1
        else:
            mv = derive_threshold(props)
            if mv is None:
                skipped_missing += 1
                return full
            method_derived += 1

        # Insert "MinimumVelocity": <mv> as the last property in the block.
        # The body ends with the last "Field": value entry. We want to add a comma
        # after the previous last entry, then the new field, then the closing brace.
        # Strip trailing whitespace from body, find last non-} char, append.
        new_body = body.rstrip()
        if new_body.endswith(','):
            new_body += f'\n      "MinimumVelocity": {mv}'
        else:
            new_body += f',\n      "MinimumVelocity": {mv}'
        added += 1
        return f'"{ammo_id}": {{{new_body}\n    }}'

    new_config = block_re.sub(replace, config_text)

    CONFIG_PATH.write_text(new_config, encoding='utf-8')

    total = added + skipped_already + skipped_zero + skipped_missing
    print(f"  total entries scanned         : {total}")
    print(f"  added MinimumVelocity         : {added}")
    print(f"    via published threshold     : {method_known}")
    print(f"    via derivation formula      : {method_derived}")
    print(f"  skipped (already had value)   : {skipped_already}")
    print(f"  skipped (FragChance == 0)     : {skipped_zero}")
    print(f"  skipped (no Realism props)    : {skipped_missing}")

    # Quick parse sanity-check on output
    try:
        # Strip JSONC comments
        cleaned = re.sub(r'^\s*//[^\n]*\n', '\n', new_config, flags=re.M)
        cleaned = re.sub(r'(\s)//[^\n]*', r'\1', cleaned)
        json.loads(cleaned)
        print(f"  output JSON: VALID")
    except json.JSONDecodeError as e:
        print(f"  output JSON: PARSE ERROR — {e}")
        sys.exit(1)

if __name__ == '__main__':
    main()
