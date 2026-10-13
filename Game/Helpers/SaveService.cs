using System.Collections.Generic;
using Strange_Universe.Game.Entities;

namespace Strange_Universe;

/// <summary>Loads, saves and deletes universes. Screens depend on this, not on the file format.</summary>
public interface ISaveService
{
    List<Universe> LoadAll();
    void Save(Universe universe);
    void Delete(Universe universe);
}

/// <summary>Stores all universes in one JSON file via <see cref="Persistence"/>.</summary>
public sealed class FileSaveService : ISaveService
{
    public const string DefaultPath = "Data/universe-settings.json";

    private readonly string _path;

    public FileSaveService(string path = DefaultPath) => _path = path;

    public List<Universe> LoadAll() => Persistence.LoadExisting(_path);
    public void Save(Universe universe) => Persistence.Persist(universe, _path);
    public void Delete(Universe universe) => Persistence.Remove(universe, _path);
}

public static class SaveServiceExtensions
{
    /// <summary>
    /// Creates a universe called <paramref name="name"/> ("New Universe" if blank), adds it to <paramref name="universes"/>
    /// and saves it straight away, so it exists on disk from the moment it is created and survives a session that ends badly.
    /// </summary>
    public static Universe CreateUniverse(this ISaveService saves, List<Universe> universes, string name)
    {
        var created = new Universe(string.IsNullOrWhiteSpace(name) ? "New Universe" : name.Trim());
        universes.Add(created);
        saves.Save(created);
        return created;
    }

    /// <summary>
    /// Replaces the contents of <paramref name="universes"/> with what is saved. This throws away anything that changed
    /// in memory since the last save (and universes that were never saved), so it is how a session is abandoned.
    /// </summary>
    public static void ReloadInto(this ISaveService saves, List<Universe> universes)
    {
        var saved = saves.LoadAll();
        universes.Clear();
        universes.AddRange(saved);
    }
}
