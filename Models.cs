// `required` keyword removed: this file is compile-included by both the server project
// (net9.0, supports `required`) and the client project (net471, lacks the polyfill
// attributes). For our deserialization-driven config, default values handle absence
// safely — no need for compile-time required-member enforcement.

namespace RealisticFrag;

/// <summary>
/// Top-level config schema. Bound from <c>config.json</c>.
/// Shared between the server-side <see cref="RealisticFrag"/> mod and the client-side
/// (v2+) <c>RealisticFrag.Client</c> Harmony plugin via compile-include.
/// </summary>
public record ModConfig
{
    /// <summary>Map of EFT ammo template IDs (24-char hex) to override values.</summary>
    public Dictionary<string, AmmoOverride> AmmoOverrides { get; set; } = new();
}

/// <summary>
/// Per-ammo override values. Maps onto <c>TemplateItem.Properties.FragmentationChance</c>,
/// <c>MinFragmentsCount</c>, and <c>MaxFragmentsCount</c> (server side, v1).
/// <see cref="MinimumVelocity"/> is consumed by the client-side v2 Harmony patch.
/// </summary>
public record AmmoOverride
{
    /// <summary>Optional human-readable label (e.g., the round's name). Runtime ignores it.</summary>
    public string? Comment { get; set; }

    /// <summary>0.0–1.0. Probability the bullet fragments on impact. Defaults to 0 if missing.</summary>
    public double FragmentationChance { get; set; }

    /// <summary>Lower bound of fragment count when fragmentation rolls true.
    /// Leave <c>null</c> to preserve the vanilla value.</summary>
    public int? MinFragments { get; set; }

    /// <summary>Upper bound of fragment count when fragmentation rolls true.
    /// Leave <c>null</c> to preserve the vanilla value.</summary>
    public int? MaxFragments { get; set; }

    /// <summary>m/s. v1 (server) ignores this. v2 (client) Harmony patch gates fragmentation
    /// to <c>bulletVelocity &gt;= MinimumVelocity</c>; below threshold the round won't fragment
    /// regardless of <see cref="FragmentationChance"/>.</summary>
    public double? MinimumVelocity { get; set; }
}
