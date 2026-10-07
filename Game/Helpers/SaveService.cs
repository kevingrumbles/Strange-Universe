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
