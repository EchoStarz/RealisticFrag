# RealisticFrag — testing guide

RealisticFrag is tested at three layers: a fast xUnit unit test suite that exercises the pure logic, a PowerShell integration script that runs the full build-and-deploy pipeline against a live SPT install, and a set of behavioral test scenarios that verify the in-raid effects manually. This document describes each layer and lays out the scenarios used during pre-release verification.

## Test layers

### Unit tests (xUnit)

The unit tests live in `tests/` and run via `dotnet test` from that directory. The full suite executes in well under one second and runs on every build. Two test classes make up the suite. `OverrideApplicationTests` exercises the static `RealisticFrag.ApplyOverrides()` method against in-memory item dictionaries, covering the happy path where every override applies cleanly, the case where a template ID is not present in the items dictionary, the defensive null check for items whose `Properties` field is null, the no-op behavior when the override map is empty, and the case where the optional logger argument is null. `ModConfigTests` exercises the configuration deserialization path, covering all-fields-present, missing optional fields like `Comment` and `MinimumVelocity`, an empty overrides map, malformed JSON input, and a sanity-check pass that loads the actually-shipped `config.json` file from the repository to confirm it remains parsable across edits.

```powershell
cd Projects\RealisticFrag\tests
dotnet test
```

### Integration script

The integration smoke test lives at `scripts/verify-deploy.ps1`. It builds the project, copies the resulting DLL and configuration into your live SPT install (the script defaults to `C:\SPT` per the SPT installer's convention, but accepts `-SptRoot <path>` to point at a different install root), boots `SPT.Server.exe`, parses the resulting log, and asserts both that the override application succeeded with zero "not found" entries and that the count of applied overrides matches the number of entries in the configuration file. The script returns exit code zero on success and one on any failure step, which makes it suitable for use as a CI smoke test or as a chained step after `dotnet test` in a release pipeline.

If your PowerShell execution policy is `RemoteSigned` or higher, the script runs directly:

```powershell
.\scripts\verify-deploy.ps1
```

The Windows default of `Restricted` blocks unsigned scripts, in which case you have two options. The first is a one-shot bypass for a single invocation, which leaves your global policy unchanged:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-deploy.ps1
```

The second is a permanent per-user policy change, which does not require admin rights:

```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

Either is fine; the bypass invocation is the lighter touch if you only run the script occasionally. The script accepts an optional `-BuildConfig Release` argument when you want to test against a release build rather than the default debug build, and it accepts `-SptRoot <path>` to point at an alternate SPT install — most usefully, the parallel clean install used for pre-publish smoke testing.

This integration script is what we run after every change before declaring a version "good," because it catches the entire class of issues where the unit tests pass but the mod doesn't actually integrate with SPT correctly.

### Behavioral tests

The behavioral tests are manual in-raid scenarios documented later in this file. They run before each pre-release version bump (0.2.0, 0.3.0, 1.0.0, and so on) and after any large configuration edit, and the tester records observed versus expected fragmentation rates per scenario. The behavioral layer is what confirms the data layer actually drives the in-game behavior — the property writes can succeed at the code level while EFT computes fragmentation from a derived value somewhere else. The first behavioral test run during the v2 development cycle revealed exactly that kind of mismatch and led directly to the implementation of the velocity-gating Harmony patch.

## Test environment for behavioral runs

Behavioral tests assume a working SPT 4.0.13 install at whatever path your SPT installer placed it (the SPT default is `C:\SPT`, which is the path used in examples throughout this document; substitute your own if different), and ideally a parallel clean install nearby (something like `C:\SPT-clean` works) for pre-publish smoke testing. The test profile should have quick access to ammo for each caliber being tested, plus a basic weapon for each caliber. Factory is the preferred map for behavioral testing because raids load fast and engagement distances are predictable, though Customs offline is a reasonable alternative when longer-range tests are needed. Bots should be disabled or scav-only so kills don't interfere with the damage observations, and the after-action damage breakdown UI should be visible so the per-shot fragmentation events are countable.

## Behavioral test scenarios

Each scenario fires ten rounds at a stationary scav at the specified range, targeting the upper torso, and records the count of "Fragmentation" damage events shown in the post-raid damage breakdown.

### S-1: 5.56 high-frag (M193)

Round: M193 (`59e6920f86f77411d82aa167`). Override: `FragmentationChance: 0.75`, `MinFragments: 4`, `MaxFragments: 7`, `MinimumVelocity: 800`. Expected outcome: at point-blank range, six or more fragmentation events out of ten hits. M193's reputation in real-world wound ballistics is exactly this — it yaws and fragments violently in tissue at supersonic velocity, and a high frag rate at point-blank is the round's signature behavior. At long range (150 meters or more), the v2 velocity gate should fire and produce zero or near-zero fragmentation events, because the bullet has decayed below the 800 m/s threshold.

### S-2: 5.56 medium-frag (M855)

Round: M855 (`54527a984bdc2d4e668b4567`). Override: `FragmentationChance: 0.05`, `MinimumVelocity: 823`. Expected outcome: at point-blank range, fragmentation is rare but not impossible — perhaps zero to two events out of ten hits. M855 is more variable than M193, and the override is intentionally lower than vanilla's 0.5 to reflect the round's real-world frag-onset dependency. If you have access to a vanilla comparison run, the reduction relative to vanilla should be visible. At any meaningful range, the gate fires almost immediately because the round decays past 823 m/s within a short distance.

### S-3: 5.56 modern (M855A1)

Round: M855A1 (`54527ac44bdc2d36668b4567`). Override: `FragmentationChance: 0.5`, `MinimumVelocity: 750`. Expected outcome: four to six fragmentation events out of ten at point-blank, sitting between M193 and M855 in real-world testing because M855A1's bonded design produces more consistent terminal performance than M855 at lower velocities while not matching M193's outright violence.

### S-4: 7.62×39 ball (PS)

Round: 7.62×39 PS (`5656d7c34bdc2d9d198b4587`). Configuration value: `FragmentationChance: 0`, no `MinimumVelocity`. Expected outcome: zero fragmentation events. Realism's data marks 7.62×39 ball as non-fragmenting, and the v2 gate has nothing to suppress because there's no fragmentation to begin with. This is a good negative-control scenario.

### S-5: 9×19 standard (Pst gzh)

Round: 9mm Pst gzh (`56d59d3ad2720bdb418b4577`). Override: low `FragmentationChance` value, `MinimumVelocity: 340`. Expected outcome: very low fragmentation rate, two or fewer events out of ten, and at any range past point-blank the gate fires because 9mm decays past the threshold quickly. This scenario verifies that pistol rounds stay tame and the mod doesn't overcorrect.

### S-6: 12-gauge slug

Round: 12ga lead slug (`560d5e524bdc2d25448b4571`). Configuration value: `FragmentationChance: 0`. Expected outcome: zero fragmentation events. Slugs penetrate or deform without breaking up in real ballistics, and the explicit zero confirms that we're not accidentally giving slugs fragmentation behavior they shouldn't have.

### S-7: 12-gauge buckshot

Round: 12ga 8.5mm magnum buckshot (`560d5e4b4bdc2d25448b455d`). Configuration value: `FragmentationChance: 0`. Expected outcome: zero fragmentation events. Buckshot pellets, like slugs, do not fragment in real ballistics; this scenario serves as a second negative control for the no-frag class of rounds.

### S-8: Vanilla baseline (negative test)

This scenario exists to confirm that RealisticFrag's overrides actually drive in-game behavior. Disable the mod by removing the DLL from `BepInEx/plugins/RealisticFrag.Client/` and `SPT/user/mods/RealisticFrag/`, restart the server, and repeat S-1 (M193) and S-2 (M855) under vanilla conditions. The fragmentation rates should be visibly different from the RealisticFrag runs — if they aren't, the data layer isn't the in-game control point and the v2 Harmony patch is doing all the real work, which is information worth knowing.

## Recording results

For each test run, record the outcomes in a fresh table appended to `dev-notes/test-runs.md` (create the file if it does not exist). The table should look like the following:

```
## v0.x.y test run — YYYY-MM-DD

| Scenario | Frag count / 10 | Pass? | Notes |
|----------|-----------------|-------|-------|
| S-1 M193 (point-blank) | 7 | ✓ | within expected range |
| S-1 M193 (150m) | 0 | ✓ | gate fired correctly |
| S-2 M855 | 1 | ✓ | within expected range |
...
```

This is the artifact that gets reviewed before any release version bump. A successful test run is the gate between "feature complete" and "ready to ship."

## Pre-publish smoke test

Before submitting any version of the mod to the Forge, run the full pre-publish verification on a parallel clean SPT 4.0.13 installation. Spin up a second SPT install at a path of your choice (the convention used in this document's examples is `C:\SPT-clean`, but anything works) with no other mods, drop only the RealisticFrag files into it, run `verify-deploy.ps1 -SptRoot <path-to-clean-install>` to confirm the integration script exits cleanly, and then run all of the behavioral test scenarios above on the clean install, recording the results. Forge upload only proceeds if every behavioral scenario falls within the expected range and the integration script returns exit code zero. This pre-publish step exists specifically to catch the failure mode where a mod works fine on the developer's heavily-modded install but breaks under a clean configuration that exposes a missing dependency or a load-order assumption.
