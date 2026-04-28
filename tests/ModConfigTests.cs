using Newtonsoft.Json;
using RealisticFrag;
using Xunit;

namespace RealisticFrag.Tests;

/// <summary>
/// Tests for <see cref="ModConfig"/> deserialization.
/// SPT loads config via <c>ModHelper.GetJsonDataFromFile&lt;T&gt;</c> which uses Newtonsoft under the
/// hood (per the Forge ecosystem convention). We test the same deserializer directly.
/// </summary>
public class ModConfigTests
{
    [Fact]
    public void ValidConfig_DeserializesAllFields()
    {
        const string json = @"{
            ""AmmoOverrides"": {
                ""54527a984bdc2d4e668b4567"": {
                    ""Comment"": ""5.56 M855"",
                    ""FragmentationChance"": 0.35,
                    ""MinFragments"": 2,
                    ""MaxFragments"": 4,
                    ""MinimumVelocity"": 823
                }
            }
        }";

        var config = JsonConvert.DeserializeObject<ModConfig>(json);

        Assert.NotNull(config);
        Assert.Single(config.AmmoOverrides);
        var ovr = config.AmmoOverrides["54527a984bdc2d4e668b4567"];
        Assert.Equal("5.56 M855", ovr.Comment);
        Assert.Equal(0.35,        ovr.FragmentationChance);
        Assert.Equal(2,           ovr.MinFragments);
        Assert.Equal(4,           ovr.MaxFragments);
        Assert.Equal(823,         ovr.MinimumVelocity);
    }

    [Fact]
    public void MissingMinimumVelocity_DeserializesAsNull()
    {
        const string json = @"{
            ""AmmoOverrides"": {
                ""54527a984bdc2d4e668b4567"": {
                    ""FragmentationChance"": 0.35,
                    ""MinFragments"": 2,
                    ""MaxFragments"": 4
                }
            }
        }";

        var config = JsonConvert.DeserializeObject<ModConfig>(json);

        Assert.NotNull(config);
        Assert.Null(config.AmmoOverrides["54527a984bdc2d4e668b4567"].MinimumVelocity);
    }

    [Fact]
    public void MissingComment_DeserializesAsNull()
    {
        const string json = @"{
            ""AmmoOverrides"": {
                ""54527a984bdc2d4e668b4567"": {
                    ""FragmentationChance"": 0.35,
                    ""MinFragments"": 2,
                    ""MaxFragments"": 4
                }
            }
        }";

        var config = JsonConvert.DeserializeObject<ModConfig>(json);

        Assert.NotNull(config);
        Assert.Null(config.AmmoOverrides["54527a984bdc2d4e668b4567"].Comment);
    }

    [Fact]
    public void EmptyOverrides_DeserializesToEmptyDict()
    {
        const string json = @"{ ""AmmoOverrides"": {} }";

        var config = JsonConvert.DeserializeObject<ModConfig>(json);

        Assert.NotNull(config);
        Assert.Empty(config.AmmoOverrides);
    }

    [Fact]
    public void MalformedJson_Throws()
    {
        const string json = @"{ ""AmmoOverrides"": { ""bad"": {  // missing close braces";

        // Could be JsonReaderException or JsonSerializationException depending on where Newtonsoft
        // gives up. We just want to know it doesn't silently produce a half-baked object.
        Assert.ThrowsAny<JsonException>(() => JsonConvert.DeserializeObject<ModConfig>(json));
    }

    [Fact]
    public void RealConfigFile_LoadsCleanly()
    {
        // Read the actual shipped config to confirm it stays parsable across edits.
        // Repo layout: tests/bin/Debug/net9.0/  →  repo root is 4 levels up
        // (the server's config.json sits at the repo root, next to RealisticFrag.csproj).
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config.json");
        Assert.True(File.Exists(path), $"Expected config.json at {Path.GetFullPath(path)} — repo layout may have changed");

        var raw = File.ReadAllText(path);
        // Strip JSONC line comments before passing to Newtonsoft.
        // (Older Newtonsoft versions don't handle them by default; explicit strip is portable.)
        var stripped = System.Text.RegularExpressions.Regex.Replace(raw, @"^\s*//[^\n]*\n", "\n", System.Text.RegularExpressions.RegexOptions.Multiline);
        var config = JsonConvert.DeserializeObject<ModConfig>(stripped);

        Assert.NotNull(config);
        Assert.NotEmpty(config.AmmoOverrides);
        // Spot-check that every override has valid frag chance in range and consistent fragment counts
        foreach (var (id, ovr) in config.AmmoOverrides)
        {
            Assert.InRange(ovr.FragmentationChance, 0.0, 1.0);
            if (ovr.MinFragments is int min) Assert.True(min >= 0, $"{id} has negative MinFragments");
            if (ovr.MinFragments is int min2 && ovr.MaxFragments is int max)
                Assert.True(max >= min2, $"{id} MaxFragments < MinFragments");
        }
    }
}
