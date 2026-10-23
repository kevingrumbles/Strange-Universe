using System;
using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.PlanetComponents;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.UI;

/// <summary>
/// The selection logic of the docked screen: a list with one line highlighted. The list is the spaceport's services (only those available right now:
/// no Refuel with a full tank, no Repair with an undamaged hull); choosing a service that opens a list (a store) shows that service's rows instead,
/// and <see cref="Back"/> returns to the services. Up and down move the highlight (wrapping), select carries the highlighted line out.
/// It draws nothing (<see cref="SpaceportOverlay"/> does).
/// </summary>
public sealed class SpaceportMenu
{
    private readonly Universe _universe;
    private int _servicesSelected; // where the highlight was in the services when a store was opened

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

    /// <summary>The store that is open, or null while the services are listed.</summary>
    public SpaceportService Open { get; private set; }

    /// <summary>The lines to draw now: the open store's rows, or the names of the services.</summary>
    public IReadOnlyList<string> Lines =>
        Open != null ? Open.Rows(_universe) : Items.Select(service => service.Name).ToList();

    /// <summary>The heading over the list: the open store's name, or null for the services.</summary>
    public string Heading => Open?.Name;

    /// <summary>The player's credits, shown on the spaceport screen.</summary>
    public int Credits => _universe.Player.Credits;

    /// <summary>Index of the highlighted line.</summary>
    public int Selected { get; private set; }

    /// <summary>Closes the open store and returns to the services. Returns false when no store was open.</summary>
    public bool Back()
    {
        if (Open == null)
            return false;

        Open = null;
        Selected = Math.Min(_servicesSelected, Math.Max(0, Items.Count - 1));
        return true;
    }

    public void Update(InputState input)
    {
        int count = Lines.Count;
        if (count == 0) return;
        Selected = Math.Min(Selected, count - 1);

        if (input.MenuUp)
            Selected = (Selected + count - 1) % count;
        if (input.MenuDown)
            Selected = (Selected + 1) % count;
        if (!input.MenuSelect)
            return;

        if (Open != null)
        {
            Open.Choose(_universe, Selected);
        }
        else
        {
            var service = Items[Selected];
            if (service.OpensRows)
            {
                _servicesSelected = Selected;
                Open = service;
                Selected = 0;
                return;
            }
            service.Perform(_universe);
        }

        Selected = Math.Max(0, Math.Min(Selected, Lines.Count - 1)); // a trade or a service may have changed the list
    }
}
