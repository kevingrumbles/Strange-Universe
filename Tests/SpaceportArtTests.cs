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

    // -- the edge fade ------------------------------------------------------------

    [Fact]
    public void ThePictureEndsInThePanelColour_AtEveryEdgeAndCorner()
    {
        foreach (var (x, y) in new[] { (0f, 0.5f), (1f, 0.5f), (0.5f, 0f), (0.5f, 1f), (0f, 0f), (1f, 1f), (0f, 1f), (1f, 0f) })
            Assert.Equal(1f, SpaceportArt.EdgeFade(x, y), 4);
    }

    [Fact]
    public void ThePictureIsUntouched_InsideTheFade()
    {
        Assert.Equal(0f, SpaceportArt.EdgeFade(0.5f, 0.5f), 4);
        Assert.Equal(0f, SpaceportArt.EdgeFade(SpaceportArt.FadeWidth, 0.5f), 4);       // the fade ends exactly where it says
        Assert.Equal(0f, SpaceportArt.EdgeFade(0.5f, 1f - SpaceportArt.FadeWidth - 0.01f), 4);
    }

    [Fact]
    public void TheFade_EasesSmoothlyFromTheEdgeInwards_AndIsTheSameOnEverySide()
    {
        float previous = 1f;
        for (int i = 0; i <= 20; i++)
        {
            float fromEdge = SpaceportArt.FadeWidth * i / 20f;
            float alpha = SpaceportArt.EdgeFade(fromEdge, 0.5f);
            Assert.True(alpha <= previous + 1e-6f, "the fade never gets stronger further in");
            Assert.InRange(alpha, 0f, 1f);
            previous = alpha;

            Assert.Equal(alpha, SpaceportArt.EdgeFade(1f - fromEdge, 0.5f), 3); // right = left
            Assert.Equal(alpha, SpaceportArt.EdgeFade(0.5f, fromEdge), 3);      // top = left
            Assert.Equal(alpha, SpaceportArt.EdgeFade(0.5f, 1f - fromEdge), 3); // bottom = left
        }

        Assert.Equal(0.5f, SpaceportArt.EdgeFade(SpaceportArt.FadeWidth / 2f, 0.5f), 3); // half way in, half covered
    }
}
