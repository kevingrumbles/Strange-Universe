using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Tests.Fakes;

/// <summary>The setup the docking and spaceport tests share: a planet with (or without) a spaceport, a started universe around it, and the menu keys.</summary>
public static class DockedWorld
{
    public static readonly InputState PressDock = new() { Dock = true };
    public static readonly InputState Up = new() { MenuUp = true };
    public static readonly InputState Down = new() { MenuDown = true };
    public static readonly InputState Select = new() { MenuSelect = true };

    /// <summary>A planet that has a spaceport, or one that has none.</summary>
    public static Planet PlanetWith(bool spaceport) =>
        Enumerable.Range(0, 1000)
                  .Select(i => new Planet($"dock_Planet_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8))
                  .First(p => (p.Spaceport != null) == spaceport);

    /// <summary>A started universe whose only planet is <paramref name="planet"/>, with the player moving at <paramref name="speed"/> <paramref name="surfaceDistance"/> from its surface.</summary>
    public static Universe Started(Planet planet, float surfaceDistance = 50f, float speed = 0f)
    {
        var u = new Universe("Dock", "dock-seed");
        u.Generate(null);
        u.Player.Navigator.Clear(); // the opening spawn task
        u.ActiveStarSystem.Planets.Clear();
        u.ActiveStarSystem.Planets.Add(planet);
        planet.Position = new Vector2(2000f, 0f);
        u.Player.Position = planet.Position + new Vector2(planet.Radius + surfaceDistance, 0f);
        u.Player.Velocity = new Vector2(0f, speed);
        return u;
    }

    /// <summary>A started universe with the player docked at a planet that has a spaceport.</summary>
    public static Universe Docked(out Planet planet)
    {
        planet = PlanetWith(spaceport: true);
        var u = Started(planet);
        u.Dock(planet);
        return u;
    }
}
