using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Tests.Fakes;

/// <summary>Keeps universes in memory so screens and services can be tested without files.</summary>
public sealed class InMemorySaveService : SaveService
{
    public List<Universe> Stored { get; } = new();

    public override List<Universe> LoadAll() => Stored.ToList();

    public override void Save(Universe universe)
    {
        int i = Stored.FindIndex(u => u.Id == universe.Id);
        if (i >= 0) Stored[i] = universe; else Stored.Add(universe);
    }

    public override void Delete(Universe universe) => Stored.RemoveAll(u => u.Id == universe.Id);
}
