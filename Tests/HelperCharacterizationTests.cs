using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace Strange_Universe.Tests;

public class SeedHashTests
{
    [Theory]
    [InlineData("", unchecked((int)2166136261u))]
    [InlineData("a", unchecked((int)0xE40C292Cu))]
    [InlineData("foobar", unchecked((int)0xBF9CF968u))]
    public void SeedHash_MatchesFnv1a(string input, int expected)
    {
        Assert.Equal(expected, ProceduralHelpers.SeedHash(input));
    }

    [Fact]
    public void SeedHash_NullEqualsEmpty()
    {
        Assert.Equal(ProceduralHelpers.SeedHash(""), ProceduralHelpers.SeedHash(null));
    }

    [Fact]
    public void SeedHash_IsStableAcrossCalls()
    {
        const string id = "37a2d42a-b4f7-476c-95c8-1215b2f225a2_Sol";
        Assert.Equal(ProceduralHelpers.SeedHash(id), ProceduralHelpers.SeedHash(id));
    }
}

public class NameGeneratorTests
{
    [Theory]
    [InlineData(CelestialNameType.System, OriginFaction.Human)]
    [InlineData(CelestialNameType.Star, OriginFaction.Corporate)]
    [InlineData(CelestialNameType.Planet, OriginFaction.Republic)]
    [InlineData(CelestialNameType.System, OriginFaction.Pirate)]
    [InlineData(CelestialNameType.Star, OriginFaction.Ancient)]
    [InlineData(CelestialNameType.Planet, OriginFaction.Alien)]
    public void GenerateCelestialName_SameSeed_SameSequence(CelestialNameType type, OriginFaction faction)
    {
        var a = new Random(1234);
        var b = new Random(1234);
        for (int i = 0; i < 20; i++)
            Assert.Equal(
                NameGenerator.GenerateCelestialName(type, faction, a),
                NameGenerator.GenerateCelestialName(type, faction, b));
    }

    [Fact]
    public void GenerateCelestialName_ReturnsNonEmpty()
    {
        var rng = new Random(42);
        for (int i = 0; i < 50; i++)
            Assert.False(string.IsNullOrWhiteSpace(
                NameGenerator.GenerateCelestialName(CelestialNameType.System, OriginFaction.Human, rng)));
    }

    [Fact]
    public void GetStarSystemName_WithoutExistingNodes_ReturnsSol()
    {
        Assert.Equal("Sol", NameGenerator.GetStarSystemName("any-seed"));
    }
}

public class MathHelpersTests
{
    private const float Eps = 1e-5f;

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(MathHelper.Pi / 2, MathHelper.Pi / 2)]
    [InlineData(-MathHelper.Pi / 2, -MathHelper.Pi / 2)]
    [InlineData(MathHelper.TwoPi, 0f)]
    [InlineData(3 * MathHelper.Pi / 2, -MathHelper.Pi / 2)]
    [InlineData(-3 * MathHelper.Pi / 2, MathHelper.Pi / 2)]
    [InlineData(4 * MathHelper.Pi + 1f, 1f)]
    public void WrapAngle_WrapsIntoRange(float input, float expected)
    {
        Assert.Equal(expected, MathHelpers.WrapAngle(input), Eps);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void WrapAngle_NonFinite_ReturnsZero(float input)
    {
        Assert.Equal(0f, MathHelpers.WrapAngle(input));
    }

    [Theory]
    [InlineData(0.1f, 0f, 0.1f)]
    [InlineData(0f, 0.1f, -0.1f)]
    [InlineData(0.1f, MathHelper.TwoPi - 0.1f, 0.2f)]
    public void StarDeltaAngle_ShortestSignedDifference(float a, float b, float expected)
    {
        Assert.Equal(expected, MathHelpers.StarDeltaAngle(a, b), 4);
    }

    [Fact]
    public void SafeNormalize_ZeroVector_ReturnsFallback()
    {
        Assert.Equal(Vector2.UnitY, MathHelpers.SafeNormalize(Vector2.Zero, Vector2.UnitY));
    }

    [Fact]
    public void SafeNormalize_NonZero_IsUnitLength()
    {
        var n = MathHelpers.SafeNormalize(new Vector2(3, 4), Vector2.Zero);
        Assert.Equal(0.6f, n.X, Eps);
        Assert.Equal(0.8f, n.Y, Eps);
    }

    [Fact]
    public void IsFinite_DetectsNaN()
    {
        Assert.True(MathHelpers.IsFinite(new Vector2(1, 2)));
        Assert.False(MathHelpers.IsFinite(new Vector2(float.NaN, 0)));
        Assert.False(MathHelpers.IsFinite(new Vector2(0, float.PositiveInfinity)));
    }

    [Fact]
    public void AsteroidInterpolatedRadius_AtControlAngle_ReturnsControlRadius()
    {
        float[] angles = { 0f, MathHelper.PiOver2, MathHelper.Pi, 3 * MathHelper.PiOver2 };
        float[] radii = { 10f, 20f, 30f, 40f };
        for (int i = 0; i < angles.Length; i++)
            Assert.Equal(radii[i], MathHelpers.AsteroidInterpolatedRadius(angles[i], angles, radii), 3);
    }
}
