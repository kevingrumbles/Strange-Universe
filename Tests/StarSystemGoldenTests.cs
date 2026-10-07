using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>
/// Golden values recorded from the pre-split StarSystem implementation.
/// Generation must keep the exact order of Random draws so these never change.
/// </summary>
public class StarSystemGoldenTests
{
    private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    private static string V(Vector2 v) => $"({F(v.X)},{F(v.Y)})";

    internal static string Fingerprint(string seed, string nodeName, Vector2 galaxyPos)
    {
        var universe = new Universe("Golden", seed) { NebulaFactory = Nebula.CreateWithoutPixels };
        StarSystemNode node;
        if (nodeName == null)
        {
            node = new StarSystemNode(seed, Vector2.Zero, existingNodes: universe.StarSystemNodes);
        }
        else
        {
            node = new StarSystemNode { SystemId = $"{seed}_{nodeName}", Name = nodeName, GalaxyPosition = galaxyPos };
        }
        universe.StarSystemNodes.Add(node);
        universe.Player.CurrentStarSystemID = node.SystemId;

        var s = universe.EnterSystem(node);

        var sb = new StringBuilder();
        sb.Append($"P{s.PlanetCount} A{s.AsteroidCount} S{s.StarCount} R{F(s.SystemRadius)} ");
        sb.Append($"Belt{F(s.AsteroidBeltInnerRadius)}-{F(s.AsteroidBeltOuterRadius)} BG{s.BackgroundStarCount} C{s.SystemConnectionCount} ");
        sb.Append($"Stars[{string.Join(";", s.Stars.Select(x => x.Name + V(x.Position) + x.ColorIndex))}] ");
        if (s.Planets.Count > 0)
            sb.Append($"P0{V(s.Planets[0].Position)}r{F(s.Planets[0].Radius)} ");
        sb.Append($"Ast[{string.Join(";", s.Asteroids.Take(3).Select(a => V(a.Position)))}] ");
        sb.Append($"BGS[{string.Join(";", s.BackgroundStars.Take(3).Select(b => V(b.Position) + b.Layer))}] ");
        double astSum = s.Asteroids.Sum(a => (double)a.Position.X + a.Position.Y);
        sb.Append($"AstSum{astSum.ToString("R", CultureInfo.InvariantCulture)} ");
        sb.Append($"Nodes[{string.Join(";", universe.StarSystemNodes.Select(n => n.SystemId + V(n.GalaxyPosition)))}] ");
        sb.Append($"Conn[{string.Join(";", node.SystemConnectionIds.OrderBy(x => x))}]");
        return sb.ToString();
    }

    public static TheoryData<string, string, float, float> Cases => new()
    {
        { "seed-1", null, 0, 0 },
        { "golden-a", null, 0, 0 },
        { "golden-a", "Alpha", 5, 5 },
        { "golden-b", "Beta", -3, 7 },
        { "golden-c", "Gamma", 12, -4 },
        { "golden-d", "Delta", 0, 9 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Generation_MatchesGolden(string seed, string nodeName, float x, float y)
    {
        string actual = Fingerprint(seed, nodeName, new Vector2(x, y));
        string key = $"{seed}|{nodeName}";
        string record = Environment.GetEnvironmentVariable("STRANGE_GOLDEN_RECORD");
        if (!string.IsNullOrEmpty(record))
        {
            System.IO.File.AppendAllText(record, $"{{ \"{key}\", @\"{actual.Replace("\"", "\"\"")}\" }},\n");
            return;
        }
        Assert.Equal(StarSystemGoldenData.Expected[key], actual);
    }
}
