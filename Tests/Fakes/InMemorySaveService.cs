using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Tests.Fakes;

/// <summary>Keeps universes in memory so screens and services can be tested without files.</summary>
public sealed class InMemorySaveService : ISaveService
{
    public List<Universe> Stored { get; } = new();

    public List<Universe> LoadAll() => Stored.ToList();

    public void Save(Universe universe)
    {
        int i = Stored.FindIndex(u => u.Id == universe.Id);
        if (i >= 0) Stored[i] = universe; else Stored.Add(universe);
    }

    public void Delete(Universe universe) => Stored.RemoveAll(u => u.Id == universe.Id);
}
