using System;
using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.PlanetComponents;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.UI;

/// <summary>
/// The selection logic of the docked screen: a list of the spaceport's services with one highlighted. Only the services that are available right
/// now are listed (no Refuel with a full tank, no Repair with an undamaged hull), so the list changes as the player uses them. Up and down move the
/// highlight (wrapping), select carries the highlighted service out. It draws nothing (<see cref="SpaceportOverlay"/> does).
/// </summary>
public sealed class SpaceportMenu
{
    private readonly Universe _universe;

    public SpaceportMenu(Planet planet, Universe universe)
    {
        Planet = planet;
        _universe = universe;
    }

    /// <summary>The planet being visited. Its name is the screen's title.</summary>
    public Planet Planet { get; }

    /// <summary>The services to list now, in the spaceport's order. It changes as the player's fuel and hull do.</summary>
    public IReadOnlyList<SpaceportService> Items =>
        Planet.Spaceport.Services.Where(service => service.IsAvailable(_universe)).ToList();

    /// <summary>Index of the highlighted service.</summary>
    public int Selected { get; private set; }

    public void Update(InputState input, Universe universe)
    {
        var items = Items;
        if (items.Count == 0) return;
        Selected = Math.Min(Selected, items.Count - 1);

        if (input.MenuUp)
            Selected = (Selected + items.Count - 1) % items.Count;
        if (input.MenuDown)
            Selected = (Selected + 1) % items.Count;
        if (input.MenuSelect)
        {
            items[Selected].Perform(universe);
            Selected = Math.Max(0, Math.Min(Selected, Items.Count - 1)); // the service may have taken itself off the list
        }
    }
}
