using EFT;
using HarmonyLib;

namespace RealisticFrag.Client.Patches;

/// <summary>
/// Velocity-gated fragmentation patch.
///
/// Patches <c>EftBulletClass.method_8()</c> — the central impact resolver called as the
/// last line of <c>method_4(deltaTime, prevPosition, prevVelocity)</c>. By the time
/// <c>method_8</c> runs, the bullet's velocity has been fully degraded for this frame's
/// impact and lives in <c>Vector3_1.magnitude</c>. Inside method_8 the bullet rolls
/// fragmentation via <c>method_10()</c>, which reads <c>this.FragmentationChance</c>.
///
/// Strategy: in a Prefix, save the original <c>FragmentationChance</c>, then if the
/// bullet's current speed is below the per-ammo <c>MinimumVelocity</c> threshold,
/// zero out <c>FragmentationChance</c> for the duration of method_8. In the Postfix,
/// restore the original value so subsequent impacts (penetrating bullets) still see
/// the right base chance.
///
/// Why method_8 instead of method_4: method_4 is called even on non-impact frame steps
/// (it always runs the velocity-degradation math). method_8 is only called when an
/// impact actually needs to be resolved — which is exactly when fragmentation rolls.
/// Patching method_8 means our gate runs once per impact, not once per frame.
/// </summary>
[HarmonyPatch(typeof(EftBulletClass), "method_8")]
public static class FragmentationVelocityPatch
{
    /// <summary>
    /// Prefix: snapshot the original FragmentationChance so we can restore it post-impact,
    /// then zero it out if the bullet is below the configured minimum velocity.
    /// </summary>
    /// <param name="__instance">The bullet whose impact is being resolved.</param>
    /// <param name="__state">Harmony's per-call state — we stash the original chance here
    /// so the Postfix can put it back. Type must match across Prefix/Postfix.</param>
    static void Prefix(EftBulletClass __instance, out float __state)
    {
        __state = __instance.FragmentationChance;

        // Source ammo lives in `EftBulletClass.Ammo` (type EFT.InventoryLogic.Item),
        // assigned at IL_0002 of method_1 from the first ctor `Item` parameter.
        var ammo = __instance.Ammo;
        if (ammo is null) return;

        // Item.TemplateId is a MongoId (24-char hex) — convert to string for dict lookup.
        var ammoId = ammo.TemplateId.ToString();
        if (string.IsNullOrEmpty(ammoId)) return;

        if (Plugin.LoadedConfig is null) return;
        if (!Plugin.LoadedConfig.AmmoOverrides.TryGetValue(ammoId, out var ovr)) return;
        if (!ovr.MinimumVelocity.HasValue) return;

        // Bullet's current speed in m/s. Vector3_1 is the velocity vector at the moment
        // of impact (method_4 has already degraded it for this frame).
        var speedMps = __instance.Vector3_1.magnitude;

        if (speedMps < ovr.MinimumVelocity.Value)
        {
            // Suppress fragmentation for this impact only. Postfix restores.
            __instance.FragmentationChance = 0f;
            // Logged at Info so it surfaces under BepInEx's default LogLevel.
            // Step down to LogDebug once you trust the gate is firing correctly.
            Plugin.Log?.LogInfo(
                $"[RealisticFrag] gated frag for ammo {ammoId}: " +
                $"speed {speedMps:F1} m/s < threshold {ovr.MinimumVelocity.Value:F1} m/s");
        }
    }

    /// <summary>
    /// Postfix: restore the original FragmentationChance so penetrating bullets keep
    /// their original base chance for subsequent impacts.
    /// </summary>
    static void Postfix(EftBulletClass __instance, float __state)
    {
        __instance.FragmentationChance = __state;
    }
}
