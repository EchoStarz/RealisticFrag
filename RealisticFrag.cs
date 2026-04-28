using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace RealisticFrag;

/// <summary>
/// Mod metadata for SPT 4.0's mod loader. Replaces the old <c>package.json</c>.
/// All <c>override</c>-marked properties on <see cref="AbstractModMetadata"/> must be set;
/// optional ones can be left as <c>null</c>.
/// </summary>
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid    { get; init; } = "com.echostarz.realisticfrag";
    public override string Name       { get; init; } = "RealisticFrag";
    public override string Author     { get; init; } = "EchoStarz";
    public override SemanticVersioning.Version Version    { get; init; } = new("1.0.0");
    public override SemanticVersioning.Range   SptVersion { get; init; } = new("~4.0.13");
    public override string License { get; init; } = "MIT";

    public override List<string>?                              Contributors      { get; init; }
    public override List<string>?                              Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url          { get; init; }
    public override bool?   IsBundleMod  { get; init; }
}

/// <summary>
/// Server-side entry point. Loads <c>config.json</c> from the mod folder and rewrites
/// fragmentation-related fields on each ammo template's <see cref="TemplateItem.Properties"/>
/// after the database has fully loaded.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class RealisticFrag(
    ISptLogger<RealisticFrag> logger,
    DatabaseService           databaseService,
    ModHelper                 modHelper) : IOnLoad
{
    public Task OnLoad()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config  = modHelper.GetJsonDataFromFile<ModConfig>(modPath, "config.json");
        var items   = databaseService.GetItems();

        var (applied, skipped) = ApplyOverrides(items, config.AmmoOverrides, logger);

        logger.Success($"[RealisticFrag] applied overrides to {applied} ammo items ({skipped} not found)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Pure logic: writes <see cref="AmmoOverride"/> values onto matching <see cref="TemplateItem"/>
    /// entries in the items dictionary. Items whose template ID is missing or whose
    /// <see cref="TemplateItem.Properties"/> is <c>null</c> are skipped (counted, logged if a
    /// logger is provided). Extracted from <see cref="OnLoad"/> so it can be unit-tested without
    /// needing to spin up SPT's DI container.
    /// </summary>
    /// <param name="items">Items dictionary, typically from <see cref="DatabaseService.GetItems"/>.</param>
    /// <param name="overrides">Per-template-ID frag value overrides.</param>
    /// <param name="logger">Optional logger; if <c>null</c>, skip messages are silent.</param>
    /// <returns>(applied, skipped) — counts of overrides that took effect vs. were dropped.</returns>
    public static (int applied, int skipped) ApplyOverrides(
        Dictionary<MongoId, TemplateItem>     items,
        Dictionary<string, AmmoOverride>      overrides,
        ISptLogger<RealisticFrag>?            logger = null)
    {
        var applied = 0;
        var skipped = 0;
        foreach (var (tplId, ovr) in overrides)
        {
            if (!items.TryGetValue(tplId, out var item) || item.Properties is null)
            {
                logger?.Warning($"[RealisticFrag] template {tplId} not found, skipping");
                skipped++;
                continue;
            }

            item.Properties.FragmentationChance = ovr.FragmentationChance;
            // MinFragments/MaxFragments are optional — leave the vanilla value alone if not set
            if (ovr.MinFragments.HasValue) item.Properties.MinFragmentsCount = ovr.MinFragments.Value;
            if (ovr.MaxFragments.HasValue) item.Properties.MaxFragmentsCount = ovr.MaxFragments.Value;

            applied++;
        }
        return (applied, skipped);
    }
}

// ModConfig and AmmoOverride moved to Models.cs (compile-included from RealisticFrag.Client too).
