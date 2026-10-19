using System.Collections.Generic;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.PlanetComponents;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.UI;

/// <summary>
/// The selection logic of the docked screen: a list of the spaceport's services with one highlighted. Up and down move it (wrapping), select
/// carries the highlighted service out. It draws nothing (<see cref="SpaceportOverlay"/> does).
/// </summary>
public sealed class SpaceportMenu
{
    public SpaceportMenu(Planet planet)
    {
        Planet = planet;
        Items = planet.Spaceport.Services;
    }

    /// <summary>The planet being visited. Its name is the screen's title.</summary>
    public Planet Planet { get; }

    /// <summary>The services, in menu order.</summary>
    public IReadOnlyList<SpaceportService> Items { get; }

    /// <summary>Index of the highlighted service.</summary>
    public int Selected { get; private set; }

    public void Update(InputState input, Universe universe)
    {
        if (Items.Count == 0) return;

        if (input.MenuUp)
            Selected = (Selected + Items.Count - 1) % Items.Count;
        if (input.MenuDown)
            Selected = (Selected + 1) % Items.Count;
        if (input.MenuSelect)
            Items[Selected].Perform(universe);
    }
}
