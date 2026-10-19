using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.PlanetComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.UI;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>The docked screen's selection logic, and the services a spaceport offers.</summary>
public class SpaceportMenuTests
{
    private static Planet PlanetWithSpaceport() =>
        Enumerable.Range(0, 1000)
                  .Select(i => new Planet($"menu_Planet_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8))
                  .First(p => p.Spaceport != null);

    private static Universe DockedUniverse(out Planet planet)
    {
        planet = PlanetWithSpaceport();
        var u = new Universe("Menu", "menu-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
        u.Generate(new RecordingAssetRequests());
        u.Player.Navigator.Clear();
        u.ActiveStarSystem.Planets.Clear();
        u.ActiveStarSystem.Planets.Add(planet);
        u.Player.Position = planet.Position + new Vector2(planet.Radius + 20f, 0f);
        u.Dock(planet);
        return u;
    }

    private static readonly InputState Up = new() { MenuUp = true };
    private static readonly InputState Down = new() { MenuDown = true };
    private static readonly InputState Select = new() { MenuSelect = true };

    [Fact]
    public void ASpaceport_OffersRefuelRepairAndUndock_InThatOrder()
    {
        var menu = new SpaceportMenu(PlanetWithSpaceport());

        Assert.Equal(new[] { "Refuel", "Repair", "Undock" }, menu.Items.Select(s => s.Name));
        Assert.Equal(0, menu.Selected);
    }

    [Fact]
    public void UpAndDown_MoveTheHighlight_AndWrapAround()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet);
        int count = menu.Items.Count;

        menu.Update(Down, u);
        Assert.Equal(1, menu.Selected);

        menu.Update(Up, u);
        menu.Update(Up, u);
        Assert.Equal(count - 1, menu.Selected); // wrapped from the top to the bottom

        menu.Update(Down, u);
        Assert.Equal(0, menu.Selected);         // and from the bottom to the top
    }

    [Fact]
    public void NoKeys_ChangeNothing()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet);
        u.Player.CurrentFuelLevel = 0;

        menu.Update(new InputState(), u);

        Assert.Equal(0, menu.Selected);
        Assert.Equal(0, u.Player.CurrentFuelLevel);
        Assert.True(u.IsDocked);
    }

    [Fact]
    public void Select_CarriesOutTheHighlightedService()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet);
        u.Player.CurrentFuelLevel = 0;
        u.Player.CurrentHullStrength = 1;

        menu.Update(Select, u);                                           // Refuel is first
        Assert.Equal(u.Player.MaxFuelLevel, u.Player.CurrentFuelLevel);
        Assert.Equal(1, u.Player.CurrentHullStrength);

        menu.Update(Down, u);
        menu.Update(Select, u);                                           // then Repair
        Assert.Equal(u.Player.MaxHullStrength, u.Player.CurrentHullStrength);
        Assert.True(u.IsDocked);                                          // neither of them leaves
    }

    [Fact]
    public void SelectingUndock_LeavesTheSpaceport()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet);

        menu.Update(Up, u); // from the top, up wraps to the last service: Undock
        Assert.Equal("Undock", menu.Items[menu.Selected].Name);
        menu.Update(Select, u);

        Assert.False(u.IsDocked);
        Assert.NotNull(u.ActiveStarSystem);
    }

    [Fact]
    public void Services_ArePerformedByTheirOwnClasses()
    {
        using var u = DockedUniverse(out _);

        new UndockService().Perform(u);

        Assert.False(u.IsDocked);
    }
}
