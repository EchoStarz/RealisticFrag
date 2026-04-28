# RealisticFrag

[![SPT version](https://img.shields.io/badge/SPT-4.0.13-blue)](https://forge.sp-tarkov.com/)
[![Release](https://img.shields.io/github/v/release/EchoStarz/RealisticFrag?include_prereleases&label=release)](https://github.com/EchoStarz/RealisticFrag/releases)
[![License](https://img.shields.io/github/license/EchoStarz/RealisticFrag)](LICENSE)
[![Last commit](https://img.shields.io/github/last-commit/EchoStarz/RealisticFrag)](https://github.com/EchoStarz/RealisticFrag/commits/main)

RealisticFrag is an SPT 4.0.13 mod that retunes per-ammo fragmentation behavior so each round behaves the way its real-world counterpart actually does in tissue. Vanilla EFT applies fragmentation chances that are flat and conservative, with the result that virtually every ball round in a given caliber feels interchangeable: M193 hits and breaks up about as readily as M855, slow pistol rounds roll for fragmentation they could never produce in life, and a long-range shot fragments as eagerly as a point-blank one. RealisticFrag rebalances every ammo entry against documented sources — Fackler's IWBA wound-ballistics papers, the Brassfit and m4carbine frag-fleet tables, and Fontaine's Realism Mod research where the modeling matches — and then layers a velocity-aware gate on top so that bullets actually stop fragmenting when they slow below the threshold real ammunition needs to break up.

The mod ships in two cooperating pieces. The server-side component, which runs as a normal SPT mod, rewrites the in-memory ammo database at server boot to apply per-round `FragmentationChance`, `MinFragmentsCount`, and `MaxFragmentsCount` values drawn from the configuration file. The client-side component, a small BepInEx plugin, applies a Harmony patch to EFT's bullet impact handler so that whenever a round's velocity at impact drops below the per-ammo threshold defined in the configuration, fragmentation is suppressed for that hit and then restored immediately afterwards. The two halves share a single `config.json` and were verified working in-raid, where they recorded over a hundred fragmentation-suppression events across multiple ammo types in a single session without any patch failures or runtime exceptions.

---

## Compatibility

| Target | Status |
|---|---|
| **SPT 4.0.13** | Tested and verified |
| **SPT 4.0.10–4.0.12** | Untested but expected to work — the underlying `TemplateItem.Properties` shape has not changed across these point releases |
| **SPT 3.x** | Incompatible. SPT 3.x's mod loader and server architecture differ fundamentally from 4.0's |
| **EFT live (non-SPT)** | Not applicable. RealisticFrag is built against SPT's NuGet packages and BepInEx plugin model |
| **Other ammo-tuning mods** | Load order matters. RealisticFrag loads at `OnLoadOrder.PostDBModLoader + 1`, so it generally writes its values after most other server mods. If another mod also writes `FragmentationChance` later in the load chain, that mod will win for the rounds it touches. The server log shows every mod's load order, which is the easiest way to confirm which side won a conflict |

## Installing the mod

End users have two paths. The simplest is to download the latest release archive from the Forge listing and drag it onto `SPT Mods Installer.exe`, which automatically routes the contents of the archive into both the BepInEx plugins folder and the SPT user/mods folder. If you prefer to install by hand, extract the archive at the SPT root so that the resulting layout looks like the structure below:

```
<your SPT root>/
├── BepInEx/plugins/RealisticFrag.Client/
│       └── RealisticFrag.Client.dll
└── SPT/user/mods/RealisticFrag/
    ├── RealisticFrag.dll
    └── config.json
```

After the files are in place, restart `SPT.Server.exe`. The startup log should report that the mod has loaded and applied its overrides, with output that looks like the following:

```
Mod: RealisticFrag version: x.y.z (GUID: com.echostarz.realisticfrag | targets SPT: ~4.0.13) by: EchoStarz loaded
[RealisticFrag] applied overrides to N ammo items (0 not found)
```

That last line is the important one. The number of overrides applied should match the number of ammo entries in your `config.json`, and the count of "not found" entries should always be zero — a non-zero "not found" count means one of the template IDs in the configuration file does not match anything in the items database, which usually points to a typo or to an ammo ID that was renamed in a recent SPT update.

The mod is safe with respect to your save: it only modifies the in-memory items database during server boot, never the on-disk JSON, and it does not touch your profile. Uninstalling — by deleting the two folders above and restarting the server — restores vanilla behavior on the next boot with no migration needed.

## Configuring overrides

Every ammo override lives in `config.json` next to the server DLL. Each entry is keyed by the 24-character ammo template ID and accepts the following fields:

```jsonc
"AmmoOverrides": {
  "<24-char ammo template ID>": {
    "Comment": "Round name + a short note about the round's frag behavior",
    "FragmentationChance": 0.0–1.0,
    "MinFragments": int (optional — omit to preserve the vanilla value),
    "MaxFragments": int (optional — omit to preserve the vanilla value),
    "MinimumVelocity": double in m/s (optional — when set, the v2 client patch
                                       suppresses fragmentation if the bullet's
                                       speed at impact is below this value)
  }
}
```

The easiest way to find a template ID is the searchable item database at [db.sp-tarkov.com/search](https://db.sp-tarkov.com/search), which lets you type a round name and copy the matching ID. Edits to `config.json` take effect on the next server restart, and there is no need to fully restart EFT or the launcher — just bounce `SPT.Server.exe` and load into your next raid. For the reasoning behind specific values and how the per-round numbers were chosen, see [`BALLISTICS.md`](BALLISTICS.md).

## Frequently asked questions

**Will this mess with my profile?** No. The mod operates exclusively against the in-memory items database that the server builds at boot time. Your profile JSON is never touched, and uninstalling the mod removes its effects on the next server boot without any migration or cleanup required.

**Do I need to restart the raid for changes to take effect?** Restart `SPT.Server.exe` after editing `config.json`, and the next raid you load will use the new values. There is no need to fully exit EFT or relaunch the SPT launcher; the server boot is the only thing that re-reads the configuration.

**Does this conflict with SVM (Server Value Modifier)?** Out of the box, no — SVM exposes knobs for several ammo properties but does not write `FragmentationChance` unless you've explicitly enabled its ammo-section overrides for that field. If you have, the last-writer-wins rule applies: RealisticFrag loads at `PostDBModLoader + 1`, which is later than SVM's typical load priority, so RealisticFrag's values normally take precedence. The server log lists each mod's writes, which is the most reliable way to check which side won.

**Does this conflict with Realism Mod?** Not currently, because Realism Mod is pinned to SPT 3.11 and won't load on 4.0.x at all. If and when Fontaine ports Realism to 4.0, you would generally want to pick one or the other rather than running both: Realism is a comprehensive overhaul, while RealisticFrag is intentionally narrow and only touches fragmentation behavior.

**Why are some buckshot and slug entries set to `FragmentationChance: 0`?** Because buckshot pellets and most slugs do not fragment in real ballistics — they either penetrate cleanly or deform without breaking up. The explicit zero value is a deliberate choice rather than an oversight; keeping the entry present (with a value of zero) signals to future maintainers that the round was considered and intentionally left non-fragmenting.

**Where do the fragmentation values come from?** They are drawn from a combination of published wound-ballistics studies, the Brassfit and m4carbine frag-fleet research for the canonical 5.56 family, ports from Fontaine's Realism Mod 1.6.4 where the modeling already matched what the literature would have produced, and a documented derivation formula for less-documented rounds that infers construction type from the bullet's mass, penetration power, and bleed-delta values. The full breakdown lives in [`BALLISTICS.md`](BALLISTICS.md).

**How do I propose a value change?** Open a pull request that lists the ammo template ID, the current value, your proposed value, and a citation supporting the change. The full process and source-quality requirements are documented in [`CONTRIBUTING.md`](CONTRIBUTING.md).

---

# Building from source

The remainder of this document is for developers who want to compile, modify, or contribute to the mod. Casual users can stop reading here.

## Prerequisites

You will need the **.NET 9 SDK**, which is available from the [official .NET download page](https://dotnet.microsoft.com/download/dotnet/9.0). Any reasonable C# development environment will work — Visual Studio 2022 Community, JetBrains Rider, and VS Code with the C# Dev Kit extension are all fine choices. You will also need a working SPT 4.0.13 install for testing, which the build scripts assume lives at `C:\SPT` unless you override the `SptRoot` MSBuild property. Strongly recommended on top of the basics is one of the SPT ID Highlighter extensions for your IDE — the [VS Code build by Lacyway](https://marketplace.visualstudio.com/items?itemName=Lacyway.SPTMongoIDHighlighter) or [the JetBrains plugin by madmanbeavis](https://plugins.jetbrains.com/plugin/28901-spt-id-highlighter). Both resolve the 24-character ammo template IDs to readable names inline as you type, which makes editing `config.json` substantially less error-prone.

## First-time setup and the build loop

The repository is laid out as a monorepo with three projects: the server mod at the repository root (`RealisticFrag.csproj`, targeting .NET 9), the client BepInEx plugin in `client/` (targeting .NET Framework 4.7.1 for Unity Mono runtime compatibility), and the xUnit test project in `tests/`. To bring everything online for the first time, clone the repository, change into the root, and run `dotnet restore` followed by `dotnet build`:

```powershell
cd <your local clone of RealisticFrag>
dotnet restore
dotnet build
dotnet build client/RealisticFrag.Client.csproj
dotnet test tests/RealisticFrag.Tests.csproj
```

The first restore pulls `SPTarkov.Common`, `SPTarkov.DI`, and `SPTarkov.Server.Core` from NuGet — these are published by the SPT team and require no special feed configuration. A successful test run reports thirteen passing tests, which together cover the override-application logic, configuration deserialization, and a sanity-check pass over the actually-shipped `config.json` file. The client project is configured to deploy its compiled DLL straight into the live SPT BepInEx plugins folder on every build, so once the project is restored, simply running `dotnet build` in the `client/` directory both compiles the patch and updates the deployed copy in one step.

For the server side, the development loop runs through `scripts/verify-deploy.ps1`, which builds, deploys to `<SPT root>/SPT/user/mods/RealisticFrag/` (defaulting to `C:\SPT` per the SPT installer's convention; override with the `-SptRoot` parameter if your install is elsewhere), boots the server, parses the log, and asserts that the override-application summary line reports the expected count with zero "not found" entries. Run it directly if your PowerShell execution policy allows scripts; otherwise, invoke it once with a bypass flag, or set `RemoteSigned` for the current user as a one-time fix:

```powershell
# If your execution policy allows it
.\scripts\verify-deploy.ps1

# One-shot bypass for a single run
powershell -ExecutionPolicy Bypass -File .\scripts\verify-deploy.ps1

# Permanent (per-user, no admin needed)
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

If you'd rather skip the script and iterate by hand, the manual deploy is two file copies. The server DLL lives at `bin\Debug\RealisticFrag\RealisticFrag\RealisticFrag.dll` after a build; copy that and `config.json` into `<SPT root>/SPT/user/mods/RealisticFrag/` (typically `C:\SPT\SPT\user\mods\RealisticFrag\`), then restart the server.

# Releasing to the Forge

Submitting to the Forge requires three things, all of which the project is set up to satisfy. The first is publicly accessible source code, which the GitHub repository at [github.com/EchoStarz/RealisticFrag](https://github.com/EchoStarz/RealisticFrag) provides; the codebase is plain, readable C# with no obfuscation. The second is verification on a fresh SPT installation, which is documented end-to-end in [`TESTING.md`](TESTING.md) along with the behavioral test scenarios used to confirm the velocity gate fires as expected. The third is consistent semantic versioning across every place the version appears: the `<Version>` element in `RealisticFrag.csproj` and `client/RealisticFrag.Client.csproj`, the `Version` property in the `ModMetadata` record inside `RealisticFrag.cs`, the git tag corresponding to the release, and the version field on the Forge listing itself. The release-note breakdown of what each version added or changed lives in [`CHANGELOG.md`](CHANGELOG.md).

When you're ready to package a release, run `scripts/package-release.ps1` from the repository root. It builds Server, Client, and Tests in Release configuration, stages the resulting files into the directory layout that `SPT Mods Installer.exe` expects, and produces a `.7z` archive in `dist/` ready to upload as the file attachment for a Forge listing version.

# Looking ahead

The current implementation is feature-complete for the use cases the mod was designed around. Future work falls into two natural categories: deeper refinement of the value set, where individual rounds get hand-tuned thresholds drawn from primary-source ballistics testing rather than the formula-derived defaults; and broader coverage of the rounds Fontaine's Realism Mod marked as non-fragmenting, where Tarkov's engagement distances might justify reintroducing a small base fragmentation chance combined with an aggressive velocity gate. Both directions are documented as open avenues in `BALLISTICS.md`, and contributions are welcome under the process described in [`CONTRIBUTING.md`](CONTRIBUTING.md).

When EFT updates change the obfuscated identifier of `EftBulletClass.method_8` (the Harmony patch target), the client patch will silently fail to attach — the symptom would be that the server-side data overrides still apply, but the velocity gate would no longer fire. The fix is a one-line update to the `[HarmonyPatch]` attribute once the new method name is identified in the remapped assembly produced by `sp-tarkov/assembly-tool`. The developer notes at `dev-notes/v2-rev-eng.md` walk through the process from scratch.

---

## License

Released under the [MIT License](LICENSE). The full text is in the `LICENSE` file at the repository root.
