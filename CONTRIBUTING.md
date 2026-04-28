# Contributing to RealisticFrag

Most contributions to RealisticFrag fall into one of two shapes. The more common is adding or correcting an ammo override — proposing a new value, or improving an existing one with better source data. The other is fixing a bug in the C# code or test suite. Both are welcome, and the rest of this document walks through how to do each cleanly.

## Adding or correcting an ammo override

This is by far the most common contribution and the easiest one to get right. The process has four parts: locating the ammo template, editing the configuration, citing your source, and running the local checks.

The first step is to find the round's template ID. The simplest way is the searchable item database hosted at [db.sp-tarkov.com/search](https://db.sp-tarkov.com/search), which lets you type a round name and copy the matching ID directly. If you'd rather work from your local SPT install, you can grep `<SPT root>/SPT/SPT_Data/database/templates/items.json` (substitute your actual SPT install path) — the file is large but the per-round entries are easy to spot. Alternatively, install one of the SPT ID Highlighter extensions for your IDE — [the VS Code build by Lacyway](https://marketplace.visualstudio.com/items?itemName=Lacyway.SPTMongoIDHighlighter) or [the JetBrains plugin by madmanbeavis](https://plugins.jetbrains.com/plugin/28901-spt-id-highlighter) — both of which resolve template IDs to readable names inline as you read or write configuration files. Whichever route you take, what you need at the end of it is the 24-character hex ID for the round, looking something like `54527a984bdc2d4e668b4567`.

The second step is to add or edit the entry in `config.json`. Each entry in the `AmmoOverrides` map takes the same shape:

```jsonc
"AmmoOverrides": {
  "<template-id>": {
    "Comment": "Round name + a short note about the round's frag behavior",
    "FragmentationChance": 0.0–1.0,
    "MinFragments": int,
    "MaxFragments": int,
    "MinimumVelocity": optional double in m/s, consumed by the v2 client patch
  }
}
```

The `Comment` field is for human readers only; the server ignores it at runtime. Keep the comment short and useful for diff readability — the round's name plus a one-line note about its real-world fragmentation behavior is the right amount of detail. The `MinFragments` and `MaxFragments` fields are optional; omitting them preserves the vanilla EFT values for that round, which is often what you want.

The third step is justifying the value with a source. Pull requests that change a value without a citation will not be merged, because the entire point of RealisticFrag's data set is its traceability back to documented research. Acceptable sources fall into a small handful of categories. Fackler's IWBA papers, the Brassfit testing tables, and the m4carbine frag-fleet research are the gold standard for military rounds and are the sources we prefer wherever they apply. Realism Mod 1.6.4's values are an acceptable secondary source as long as the attribution is clear in the PR description. AmmoOracle and similar consumer-facing wound-ballistics summaries are useful for pistol calibers and less-documented rounds where the rigorous literature is sparse. Wikipedia and military doctrine documents are acceptable as supplementary references but never as a stand-alone source for an actual numeric value. "I think it should be X" is not a source, and neither is a feeling — please do the research first.

The fourth step is running the local checks before you open the pull request. The test suite catches most schema-level issues:

```powershell
cd Projects\RealisticFrag\tests
dotnet test
```

The `RealConfigFile_LoadsCleanly` test in particular will fail if your additions break the JSON schema or introduce out-of-range values. Once the unit tests are green, run the integration smoke test:

```powershell
.\Projects\RealisticFrag\scripts\verify-deploy.ps1
```

This boots `SPT.Server.exe` against your local SPT install, applies the modified configuration, and confirms that every template ID resolves cleanly with no "not found" warnings. If your PowerShell execution policy blocks the script, see `TESTING.md` for the bypass syntax.

When you open the pull request, branch off `main`, push, and include in the description the round name (or names) being added or changed, the old and new values for any changes, the source citation for each value, and ideally the result of any in-raid behavioral testing you ran (the `TESTING.md` document describes the test scenarios).

## Fixing code or tests

Code contributions follow the conventions of the existing codebase: four-space indentation, brace-on-new-line for type definitions, expression bodies for trivial members, xmldoc on public types and public static methods. The full test suite must pass on every PR (`dotnet test` from the test project's directory), and any change to public-facing behavior needs a corresponding test that exercises it. Public API changes — anything that ships in the Models record set or the static `ApplyOverrides` signature — also need updated xmldoc. Commit messages should be in the imperative mood with a short, descriptive subject line; if the reason for the change is non-obvious, follow the subject with a longer body explaining the why.

## What won't be merged

A few categories of pull request are not accepted, and it's worth knowing them up front so you don't spend time on something that won't land. Value changes without a source citation are the most common rejection — the data set's value depends entirely on its traceability, so this is non-negotiable. New fields on the `AmmoOverride` record that aren't actually wired to anything in the code are also rejected; if you want a `Damage` override or a `RicochetChance` override, those are interesting ideas but they belong in a different mod, and adding them to RealisticFrag would dilute its scope. Removing the `Comment` field from existing entries "for cleanliness" is rejected because the comments are load-bearing for diff review and PR readability — they stay. And finally, changes that bump the SPT version target without testing on that target are rejected on principle, because compatibility claims have to be testable.

## Style notes for documentation contributions

If your contribution touches Markdown — the README, BALLISTICS, TESTING, or other prose files — match the style of what's already there: flowing paragraphs rather than bullet stubs, full sentences with proper transitions, code fences with language hints where applicable, and tables for genuinely tabular data rather than for prose. The CHANGELOG is the one exception; it follows the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) convention and stays structured by design.
