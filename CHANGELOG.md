# Changelog

All notable changes to RealisticFrag are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] — 2026-04-27

First public release. Both v1 (server-side data overrides) and v2 (client-side velocity-gated fragmentation Harmony patch) are feature-complete and verified working in-raid.

A version string carrying a SemVer pre-release suffix (e.g., `1.0.0-rc.1`) was attempted briefly but reverted: BepInEx 5.4 uses `System.Version` for plugin version parsing, which rejects SemVer pre-release labels. The version reverts to plain `1.0.0` everywhere so the client and server use the same string and the BepInEx loader accepts the plugin.

### Verified
- v2 confirmed working in-raid (2026-04-27): 115 gating events across 4 ammo types in
  one raid session, zero exceptions, zero patch failures. `Ammo.TemplateId` resolution
  + `Vector3_1.magnitude` velocity reading + Prefix/Postfix `__state` flow all behave
  as designed. M856 tracer caught at 786 m/s vs 800 threshold validates that the
  formula-derived thresholds line up with real in-flight velocity decay.

### Added
- **v2 velocity-gated fragmentation (client-side Harmony patch)**: when a bullet impacts
  below its configured `MinimumVelocity` threshold, fragmentation is suppressed for that
  hit. Original `FragmentationChance` is restored for subsequent impacts (penetrating
  bullets keep their full base chance).
  - New project `RealisticFrag.Client/` (net471, BepInEx + Harmony, deploys to
    `BepInEx\plugins\RealisticFrag.Client\`)
  - Patches `EftBulletClass.method_8()` (the central impact resolver, called from
    `method_4()` after velocity is finalized for the frame)
  - Reads `__instance.Ammo.TemplateId` to look up the per-ammo override
  - Reads `__instance.Vector3_1.magnitude` for current bullet velocity
  - Auto-deploys to SPT install via `<DeployToSpt>` MSBuild target on every `dotnet build`
- **Shared `Models.cs`** — `ModConfig` / `AmmoOverride` records now live in one file
  compile-included by both server and client projects (single source of schema)
- **Cloned + patched + built `sp-tarkov/assembly-tool`** (one null-deref bug fixed in
  `AttributeFactory.UpdateAsyncAttributes`) for EFT DLL deobfuscation; output remapped
  DLL at `Projects/assembly-tool/work/Managed/Assembly-CSharp-cleaned-remapped-publicized.dll`
- **111 of 169 ammo entries have `MinimumVelocity` thresholds** — 50 hardcoded from
  published wound-ballistics studies (Fackler IWBA, Brassfit frag-fleet tables,
  m4carbine), 61 derived via the documented formula in `BALLISTICS.md`. The remaining
  58 are intentionally ungated (51 with `FragmentationChance: 0` → no frag to gate;
  7 lack Realism prop data).
- **`scripts/compute-thresholds.py`**: re-runnable threshold derivation script
- **`BALLISTICS.md` methodology section** documenting the published-vs-derived split
- **Comprehensive ammo coverage**: 169 ammo overrides spanning 24 caliber families
  (5.56, 5.45, 7.62×39, 7.62×51, 7.62×54R, 9×19, 9×18, 9×39, 12ga, 20ga, .338, .45 ACP,
  4.6×30, 5.7×28, .366 TKM, .300 BLK, 12.7×33/55/108, 6.8×51 SIG, .357, etc.). Values
  ported verbatim from Fontaine's Realism Mod 1.6.4 (SPT 3.11.4) where present and
  cross-referenced against SPT 4.0.13's items database (4 Realism IDs were skipped
  because the rounds were renamed/removed in 4.0.x). Provenance dump at
  `scripts/realism-ported.json` for review.
- xUnit test project (`RealisticFrag.Tests/`) with 13 unit tests covering
  override-application logic, config deserialization, and a sanity-check pass over
  the shipped `config.json`
- `scripts/verify-deploy.ps1` integration smoke test (build → deploy → boot → log-assert)
- `TESTING.md` with behavioral test scenarios per caliber family
- `BALLISTICS.md` documenting frag-value methodology and sources
- `CONTRIBUTING.md` with PR guidance for adding new ammo overrides
- Inline xmldoc on all public types
- README expanded for end users (hero, compatibility table, install/uninstall, FAQ)

### Changed
- `OnLoad()` refactored to extract a static, testable `ApplyOverrides()` method.
  Behavior identical; only structure changed.
- `AmmoOverride.MinFragments` and `MaxFragments` are now optional (`int?`). When
  unset, the vanilla EFT values are preserved. Lets ports from Realism (which often
  only writes `FragmentationChance`) match the source mod's behavior precisely.

## [0.1.0] — 2026-04-27

### Added
- Initial scaffold targeting SPT 4.0.13.
- Server-side mod that overrides `FragmentationChance`, `MinFragmentsCount`, and
  `MaxFragmentsCount` per ammo template ID.
- Three example overrides (M855, M855A1, M193) with researched values.
- `ModConfig` schema with forward-compat `MinimumVelocity` field for v2.
- README with build, deploy, iteration, and v2 roadmap.
- MIT license.

[Unreleased]:    https://github.com/EchoStarz/RealisticFrag/compare/v1.0.0...HEAD
[1.0.0]:         https://github.com/EchoStarz/RealisticFrag/releases/tag/v1.0.0
[0.1.0]:         https://github.com/EchoStarz/RealisticFrag/releases/tag/v0.1.0
