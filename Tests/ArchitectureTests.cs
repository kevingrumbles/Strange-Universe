using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Systems;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 5: the simulation layers compile without graphics concepts.</summary>
public class ArchitectureTests
{
    private static readonly string[] SimulationFolders =
        { "Game/Entities", "Game/Components", "Game/EventSystem", "Game/NavSystem", "Game/Simulation" };

    /// <summary>
    /// The one place simulation code may name MonoGame's <c>Color</c>: how a weapon's projectiles look is plain colour data owned by the
    /// weapon (<c>ProjectileVisual</c>, filled in by the catalog). Nothing here draws, loads textures or uses a SpriteBatch.
    /// </summary>
    private static readonly string[] ColorDataFiles = { "ProjectileVisual.cs", "EquipmentCatalog.cs" };

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
            .Where(file => !ColorDataFiles.Contains(Path.GetFileName(file)))
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
    public void NavTasks_DependOnWorldContext_NotConcreteSystem()
    {
        string root = RepoRoot();
        var violations = Directory.GetFiles(Path.Combine(root, "Game/NavSystem"), "*.cs")
            .Where(f => Path.GetFileName(f) != "NavTask.cs") // NavTask supplies the default world from the owner
            .Where(f => Regex.IsMatch(StripComments(File.ReadAllText(f)), @"(StarSystem|Universe)"))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.Empty(violations);
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
