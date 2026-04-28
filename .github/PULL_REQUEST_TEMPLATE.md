<!--
Thanks for contributing to RealisticFrag.

Before submitting, please review CONTRIBUTING.md for the source-citation
requirements (especially if you're proposing changes to ammo override values).
-->

## What this changes

<!-- One or two sentences describing the change. For ammo override edits, list
     the round name and the field(s) being changed. -->

## Source citation
<!-- Required for any change to a FragmentationChance, MinFragments, MaxFragments,
     or MinimumVelocity value. Cite a wound-ballistics study, Brassfit/m4carbine
     test data, Realism Mod 1.6.4, or an equivalent published source. "I think it
     should be X" is not a source. -->

- Round name + template ID:
- Old value(s):
- New value(s):
- Source:

## Testing

- [ ] `dotnet test tests/RealisticFrag.Tests.csproj` passes (13/13)
- [ ] `scripts/verify-deploy.ps1` exits 0 (server applies overrides cleanly)
- [ ] Behavioral test in raid (if your change affects an ammo most players use — see `TESTING.md`)

## Notes for the reviewer
<!-- Anything else that's worth flagging. Skip this section if not applicable. -->
