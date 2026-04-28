using Moq;
using RealisticFrag;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using Xunit;

namespace RealisticFrag.Tests;

/// <summary>
/// Tests for the pure-logic <see cref="RealisticFrag.RealisticFrag.ApplyOverrides"/> method.
/// Builds in-memory items + override fixtures, runs the method, asserts state changes.
/// No SPT DI container, no actual server boot.
/// </summary>
public class OverrideApplicationTests
{
    // 24-char hex template IDs from SPT items.json. Real values, but in tests they're just strings/MongoIds.
    private const string M855_Id   = "54527a984bdc2d4e668b4567";
    private const string M855A1_Id = "54527ac44bdc2d36668b4567";
    private const string M193_Id   = "59e6920f86f77411d82aa167";
    private const string Bogus_Id  = "000000000000000000000000";

    private static TemplateItem MakeItem(string id) => new()
    {
        Id = (MongoId)id,
        Properties = new TemplateItemProperties
        {
            // Vanilla baseline frag values; we expect ApplyOverrides to overwrite these
            FragmentationChance = 0.5,
            MinFragmentsCount   = 2,
            MaxFragmentsCount   = 4,
        },
    };

    private static AmmoOverride MakeOverride(double frag, int? min = null, int? max = null, double? velocity = null) => new()
    {
        FragmentationChance = frag,
        MinFragments        = min,
        MaxFragments        = max,
        MinimumVelocity     = velocity,
    };

    [Fact]
    public void KnownIds_AllApplied()
    {
        var items = new Dictionary<MongoId, TemplateItem>
        {
            [(MongoId)M855_Id]   = MakeItem(M855_Id),
            [(MongoId)M855A1_Id] = MakeItem(M855A1_Id),
            [(MongoId)M193_Id]   = MakeItem(M193_Id),
        };
        var overrides = new Dictionary<string, AmmoOverride>
        {
            [M855_Id]   = MakeOverride(0.35, 2, 4),
            [M855A1_Id] = MakeOverride(0.55, 3, 5),
            [M193_Id]   = MakeOverride(0.75, 4, 7),
        };

        var (applied, skipped) = RealisticFrag.ApplyOverrides(items, overrides);

        Assert.Equal(3, applied);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void OverrideValues_WrittenCorrectly()
    {
        var items = new Dictionary<MongoId, TemplateItem>
        {
            [(MongoId)M193_Id] = MakeItem(M193_Id),
        };
        var overrides = new Dictionary<string, AmmoOverride>
        {
            [M193_Id] = MakeOverride(0.75, 4, 7, velocity: 800),
        };

        RealisticFrag.ApplyOverrides(items, overrides);

        var props = items[(MongoId)M193_Id].Properties!;
        Assert.Equal(0.75, props.FragmentationChance);
        Assert.Equal(4,    props.MinFragmentsCount);
        Assert.Equal(7,    props.MaxFragmentsCount);
        // MinimumVelocity is config-only, must NOT be written to TemplateItemProperties (no such field on EFT side)
    }

    [Fact]
    public void NullMinMax_PreservesVanillaCounts()
    {
        var items = new Dictionary<MongoId, TemplateItem>
        {
            [(MongoId)M193_Id] = MakeItem(M193_Id),  // vanilla baseline: Min=2, Max=4
        };
        var overrides = new Dictionary<string, AmmoOverride>
        {
            // Only frag chance set; Min/Max omitted (null)
            [M193_Id] = MakeOverride(0.6),
        };

        RealisticFrag.ApplyOverrides(items, overrides);

        var props = items[(MongoId)M193_Id].Properties!;
        Assert.Equal(0.6, props.FragmentationChance);
        // Vanilla counts preserved because override left them null
        Assert.Equal(2, props.MinFragmentsCount);
        Assert.Equal(4, props.MaxFragmentsCount);
    }

    [Fact]
    public void UnknownId_LoggedAndSkipped()
    {
        var items = new Dictionary<MongoId, TemplateItem>
        {
            [(MongoId)M855_Id] = MakeItem(M855_Id),
        };
        var overrides = new Dictionary<string, AmmoOverride>
        {
            [M855_Id]  = MakeOverride(0.35, 2, 4),
            [Bogus_Id] = MakeOverride(0.50, 1, 2),  // not in items dict
        };
        var logger = new Mock<ISptLogger<RealisticFrag>>();

        var (applied, skipped) = RealisticFrag.ApplyOverrides(items, overrides, logger.Object);

        Assert.Equal(1, applied);
        Assert.Equal(1, skipped);
        // Confirm the warn message went out and mentioned the bogus ID.
        // ISptLogger.Warning has signature `Warning(string, Exception? = null)` — Moq's expression
        // tree can't elide the optional arg, so we pass `null` explicitly.
        logger.Verify(
            l => l.Warning(It.Is<string>(s => s.Contains(Bogus_Id)), null),
            Times.Once);
    }

    [Fact]
    public void NullProperties_LoggedAndSkipped()
    {
        var items = new Dictionary<MongoId, TemplateItem>
        {
            [(MongoId)M855_Id] = new TemplateItem
            {
                Id = (MongoId)M855_Id,
                Properties = null,  // pathological: item exists but no _props block
            },
        };
        var overrides = new Dictionary<string, AmmoOverride>
        {
            [M855_Id] = MakeOverride(0.35, 2, 4),
        };

        var (applied, skipped) = RealisticFrag.ApplyOverrides(items, overrides);

        Assert.Equal(0, applied);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void EmptyOverrides_NoOp()
    {
        var items = new Dictionary<MongoId, TemplateItem>
        {
            [(MongoId)M855_Id] = MakeItem(M855_Id),
        };
        var overrides = new Dictionary<string, AmmoOverride>();

        var (applied, skipped) = RealisticFrag.ApplyOverrides(items, overrides);

        Assert.Equal(0, applied);
        Assert.Equal(0, skipped);
        // Item should be untouched
        Assert.Equal(0.5, items[(MongoId)M855_Id].Properties!.FragmentationChance);
    }

    [Fact]
    public void NullLogger_DoesNotThrowOnSkip()
    {
        var items     = new Dictionary<MongoId, TemplateItem>();
        var overrides = new Dictionary<string, AmmoOverride>
        {
            [Bogus_Id] = MakeOverride(0.5, 1, 2),
        };

        // Should not throw — null logger is allowed
        var (applied, skipped) = RealisticFrag.ApplyOverrides(items, overrides, logger: null);

        Assert.Equal(0, applied);
        Assert.Equal(1, skipped);
    }
}
