using System;
using System.IO;
using System.Linq;
using Strange_Universe.Game.UI;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>The spaceport screen shows one picture per type of planet.</summary>
public class SpaceportArtTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Strange Universe.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }

    [Fact]
    public void TerranAndLavaPlanets_UseTheirOwnPictures()
    {
        Assert.Equal("Art/Terran_Planet_sprite.png", SpaceportArt.PathFor(PlanetType.Terran));
        Assert.Equal("Art/Lava_Planet_sprite.png", SpaceportArt.PathFor(PlanetType.Lava));
    }

    [Fact]
    public void EveryPlanetType_HasAPicture_ThatExistsInTheArtFolder()
    {
        foreach (var type in Enum.GetValues<PlanetType>())
        {
            string path = SpaceportArt.PathFor(type);

            Assert.False(string.IsNullOrEmpty(path), $"{type} has no picture");
            Assert.True(File.Exists(Path.Combine(RepoRoot(), path)), $"{type}: {path} is missing");
        }
    }

    [Fact]
    public void EachPlanetType_HasADifferentPicture()
    {
        var paths = Enum.GetValues<PlanetType>().Select(SpaceportArt.PathFor).ToList();

        Assert.Equal(paths.Count, paths.Distinct().Count());
    }
}
