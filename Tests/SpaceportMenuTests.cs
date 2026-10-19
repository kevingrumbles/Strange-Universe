using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.PlanetComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.UI;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>The docked screen's selection logic, and which services it lists.</summary>
public class SpaceportMenuTests
{
    private static Planet PlanetWithSpaceport() =>
        Enumerable.Range(0, 1000)
                  .Select(i => new Planet($"menu_Planet_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8))
                  .First(p => p.Spaceport != null);

    /// <summary>A docked player with the given fuel and hull.</summary>
    private static Universe DockedUniverse(out Planet planet, bool needsFuel = true, bool needsRepair = true)
    {
        planet = PlanetWithSpaceport();
        var u = new Universe("Menu", "menu-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
        u.Generate(new RecordingAssetRequests());
        u.Player.Navigator.Clear();
        u.ActiveStarSystem.Planets.Clear();
        u.ActiveStarSystem.Planets.Add(planet);
        u.Player.Position = planet.Position + new Vector2(planet.Radius + 20f, 0f);
        u.Dock(planet);
        u.Player.CurrentFuelLevel = needsFuel ? 0 : u.Player.MaxFuelLevel;
        u.Player.CurrentHullStrength = needsRepair ? 1 : u.Player.MaxHullStrength;
        return u;
    }

    private static readonly InputState Up = new() { MenuUp = true };
    private static readonly InputState Down = new() { MenuDown = true };
    private static readonly InputState Select = new() { MenuSelect = true };

    private static string[] Names(SpaceportMenu menu) => menu.Items.Select(s => s.Name).ToArray();

    // -- which services are listed ---------------------------------------------

    [Fact]
    public void Refuel_IsListedOnlyWhenFuelIsMissing_AndRepairOnlyWhenTheHullIsDamaged()
    {
        using var both = DockedUniverse(out var planet, needsFuel: true, needsRepair: true);
        Assert.Equal(new[] { "Refuel", "Repair", "Undock" }, Names(new SpaceportMenu(planet, both)));

        using var fuelOnly = DockedUniverse(out planet, needsFuel: true, needsRepair: false);
        Assert.Equal(new[] { "Refuel", "Undock" }, Names(new SpaceportMenu(planet, fuelOnly)));

        using var hullOnly = DockedUniverse(out planet, needsFuel: false, needsRepair: true);
        Assert.Equal(new[] { "Repair", "Undock" }, Names(new SpaceportMenu(planet, hullOnly)));

        using var neither = DockedUniverse(out planet, needsFuel: false, needsRepair: false);
        Assert.Equal(new[] { "Undock" }, Names(new SpaceportMenu(planet, neither)));
    }

    [Fact]
    public void AServiceLeavesTheList_OnceItHasDoneItsJob()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet, u);

        menu.Update(Select, u); // Refuel is first
        Assert.Equal(new[] { "Repair", "Undock" }, Names(menu));

        menu.Update(Select, u); // Repair is first now
        Assert.Equal(new[] { "Undock" }, Names(menu));
    }

    [Fact]
    public void ThePlayerStillNeedingFuelOrRepairs_SeesTheServiceAgain()
    {
        using var u = DockedUniverse(out var planet, needsFuel: false, needsRepair: false);
        var menu = new SpaceportMenu(planet, u);
        Assert.Equal(new[] { "Undock" }, Names(menu));

        u.Player.CurrentFuelLevel = 1; // the list follows the player's state, it is not fixed when the menu opens

        Assert.Equal(new[] { "Refuel", "Undock" }, Names(menu));
    }

    // -- the highlight --------------------------------------------------------------

    [Fact]
    public void UpAndDown_MoveTheHighlight_AndWrapAround()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet, u);
        int count = menu.Items.Count;
        Assert.Equal(0, menu.Selected);

        menu.Update(Down, u);
        Assert.Equal(1, menu.Selected);

        menu.Update(Up, u);
        menu.Update(Up, u);
        Assert.Equal(count - 1, menu.Selected); // wrapped from the top to the bottom

        menu.Update(Down, u);
        Assert.Equal(0, menu.Selected);         // and from the bottom to the top
    }

    [Fact]
    public void TheHighlight_StaysOnTheList_WhenTheServiceItWasOnLeaves()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet, u);
        menu.Update(Down, u);                  // on Repair, in the middle of [Refuel, Repair, Undock]

        menu.Update(Select, u);                // repair: the list becomes [Refuel, Undock]

        Assert.InRange(menu.Selected, 0, menu.Items.Count - 1);
        Assert.Equal(new[] { "Refuel", "Undock" }, Names(menu));

        u.Player.CurrentFuelLevel = u.Player.MaxFuelLevel; // and Refuel goes too, with the highlight on the last entry
        menu.Update(Down, u);
        menu.Update(new InputState(), u);
        Assert.Equal(0, menu.Selected);        // [Undock]
    }

    [Fact]
    public void NoKeys_ChangeNothing()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet, u);

        menu.Update(new InputState(), u);

        Assert.Equal(0, menu.Selected);
        Assert.Equal(0, u.Player.CurrentFuelLevel);
        Assert.True(u.IsDocked);
    }

    // -- select -------------------------------------------------------------------------

    [Fact]
    public void Select_CarriesOutTheHighlightedService()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet, u);

        menu.Update(Select, u);                                           // Refuel is first
        Assert.Equal(u.Player.MaxFuelLevel, u.Player.CurrentFuelLevel);
        Assert.Equal(1, u.Player.CurrentHullStrength);                    // refuelling does not repair

        menu.Update(Select, u);                                           // Repair is first now
        Assert.Equal(u.Player.MaxHullStrength, u.Player.CurrentHullStrength);
        Assert.True(u.IsDocked);                                          // neither of them leaves
    }

    [Fact]
    public void SelectingUndock_LeavesTheSpaceport()
    {
        using var u = DockedUniverse(out var planet);
        var menu = new SpaceportMenu(planet, u);

        menu.Update(Up, u); // from the top, up wraps to the last service: Undock
        Assert.Equal("Undock", menu.Items[menu.Selected].Name);
        menu.Update(Select, u);

        Assert.False(u.IsDocked);
        Assert.NotNull(u.ActiveStarSystem);
    }

    [Fact]
    public void UndockIsAlwaysThere()
    {
        using var u = DockedUniverse(out var planet, needsFuel: false, needsRepair: false);
        var menu = new SpaceportMenu(planet, u);

        menu.Update(Select, u); // the only entry

        Assert.False(u.IsDocked);
    }

    [Fact]
    public void Services_ArePerformedByTheirOwnClasses()
    {
        using var u = DockedUniverse(out _);

        new UndockService().Perform(u);

        Assert.False(u.IsDocked);
    }
}
