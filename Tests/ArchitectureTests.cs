using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Systems;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 5: the simulation layers compile without graphics concepts.</summary>
public class ArchitectureTests
{
    private static readonly string[] SimulationFolders =
        { "Game/Entities", "Game/Components", "Game/EventSystem", "Game/NavSystem", "Game/Simulation" };

    private static readonly Regex GraphicsTypes =
        new(@"\b(Texture2D|GraphicsDevice|SpriteBatch|SpriteFont|Color)\b", RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Strange Universe.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }

    private static string StripComments(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return Regex.Replace(source, @"//.*", "");
    }

    [Fact]
    public void SimulationCode_DoesNotReferenceGraphicsTypes()
    {
        string root = RepoRoot();
        var violations = SimulationFolders
            .SelectMany(f => Directory.GetFiles(Path.Combine(root, f), "*.cs", SearchOption.AllDirectories))
            .Where(file => GraphicsTypes.IsMatch(StripComments(File.ReadAllText(file))))
            .Select(file => Path.GetRelativePath(root, file))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void SimulationCode_DoesNotImportRenderingNamespaces()
    {
        string root = RepoRoot();
        var violations = SimulationFolders
            .SelectMany(f => Directory.GetFiles(Path.Combine(root, f), "*.cs", SearchOption.AllDirectories))
            .Where(file => Regex.IsMatch(StripComments(File.ReadAllText(file)),
                @"using\s+(Microsoft\.Xna\.Framework\.Graphics|Strange_Universe\.Game\.(Systems|UI|Screens))\b"))
            .Select(file => Path.GetRelativePath(root, file))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void WeaponLinger_EqualsVisualBurstDuration()
    {
        foreach (var weapon in Equipment.Presets.Where(e => e.PrimaryWeapon))
        {
            if (weapon.EquipmentName is "Light Missile") continue; // no dedicated visual
            Assert.Equal(ProjectileVisuals.For(weapon.EquipmentName).BurstDuration, weapon.ImpactLingerSeconds);
        }
    }

    [Fact]
    public void Projectile_ExpiresAfterImpactLinger()
    {
        var p = new Projectile { Lifetime = 5f, ImpactLingerSeconds = 0.2f };
        Assert.False(p.IsExpired);

        p.MarkHit();
        p.Update(0.1f);
        Assert.False(p.IsExpired);
        p.Update(0.1f);
        Assert.True(p.IsExpired);
    }
}
