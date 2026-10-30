using Strange_Universe.Game.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strange_Universe;

/// <summary>Loads, saves and deletes universes. Screens depend on this, not on the file format.</summary>
public abstract class SaveService
{
    public abstract List<Universe> LoadAll();
    public abstract void Save(Universe universe);
    public abstract void Delete(Universe universe);

    /// <summary>
    /// Creates a universe called <paramref name="name"/> ("New Universe" if blank), adds it to <paramref name="universes"/>
    /// and saves it straight away, so it exists on disk from the moment it is created and survives a session that ends badly.
    /// </summary>
    public Universe CreateUniverse(List<Universe> universes, string name)
    {
        var created = new Universe(string.IsNullOrWhiteSpace(name) ? "New Universe" : name.Trim());
        universes.Add(created);
        Save(created);
        return created;
    }

    /// <summary>
    /// Replaces the contents of <paramref name="universes"/> with what is saved. This throws away anything that changed
    /// in memory since the last save (and universes that were never saved), so it is how a session is abandoned.
    /// </summary>
    public void ReloadInto(List<Universe> universes)
    {
        var saved = LoadAll();
        universes.Clear();
        universes.AddRange(saved);
    }
}

/// <summary>Stores all universes in one JSON file.</summary>
public sealed class FileSaveService : SaveService
{
    public const string DefaultPath = "Data/universe-settings.json";

    private readonly string _path;

    public FileSaveService(string path = DefaultPath) => _path = path;

    /// <summary>
    /// Loads all universes. On a parse failure the bad file is copied to a timestamped
    /// <c>.corrupt-*.bak</c> next to it, the error is logged, and an empty list is returned.
    /// </summary>
    public override List<Universe> LoadAll()
    {
        try
        {
            return TryLoad(ResolvePath(), out var list) ? list : new List<Universe>();
        }
        catch (Exception e)
        {
            Log($"Failed to load universe settings from '{_path}'", e);
            return new List<Universe>();
        }
    }

    public override void Save(Universe universe)
    {
        if (universe == null) return;
        Update("save", all =>
        {
            int idx = all.FindIndex(u => u.Id == universe.Id);
            if (idx >= 0) all[idx] = universe;
            else all.Add(universe);
        });
    }

    public override void Delete(Universe universe)
    {
        if (universe == null) return;
        Update("remove", all => all.RemoveAll(u => u.Id == universe.Id));
    }

    /// <summary>
    /// The save path as a rooted, normalized path. A relative path is anchored to the executable directory rather than the
    /// process working directory, which is not guaranteed to be the same.
    /// </summary>
    private string ResolvePath()
    {
        if (string.IsNullOrWhiteSpace(_path))
            throw new ArgumentException("Path must not be empty.");

        return Path.IsPathRooted(_path)
            ? Path.GetFullPath(_path)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _path));
    }

    /// <summary>Read-modify-write. Aborts (without touching the file) if the existing file can't be parsed.</summary>
    private void Update(string operation, Action<List<Universe>> mutate)
    {
        try
        {
            string fullPath = ResolvePath();
            if (!TryLoad(fullPath, out var all))
            {
                Log($"Skipped {operation}: '{fullPath}' could not be parsed and was not overwritten.", null);
                return;
            }

            mutate(all);
            WriteAtomic(fullPath, JsonSerializer.Serialize(all, WriteOptions));
        }
        catch (Exception e)
        {
            Log($"Failed to {operation} universe settings at '{_path}'", e);
        }
    }

    /// <summary>Returns false only when the file exists but is not valid JSON for the format (after backing it up).</summary>
    private static bool TryLoad(string fullPath, out List<Universe> list)
    {
        list = new List<Universe>();
        if (!File.Exists(fullPath)) return true;

        string json = File.ReadAllText(fullPath);
        if (string.IsNullOrWhiteSpace(json)) return true;

        try
        {
            list = JsonSerializer.Deserialize<List<Universe>>(json, ReadOptions) ?? new List<Universe>();
            return true;
        }
        catch (JsonException e)
        {
            string backup = $"{fullPath}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            try { File.Copy(fullPath, backup, overwrite: true); }
            catch (Exception copyError) { Log($"Could not back up corrupt file to '{backup}'", copyError); backup = null; }
            Log($"Corrupt universe settings '{fullPath}'" + (backup != null ? $"; backed up to '{backup}'" : string.Empty), e);
            return false;
        }
    }

    /// <summary>Writes to a temp file then swaps it in, so a crash mid-write can't truncate the save.</summary>
    private static void WriteAtomic(string fullPath, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string tmp = fullPath + ".tmp";
        File.WriteAllText(tmp, contents);
        File.Move(tmp, fullPath, overwrite: true);
    }

    private static void Log(string message, Exception e)
    {
        string line = $"[Strange Universe] {message}{(e != null ? $": {e}" : string.Empty)}";
        Console.Error.WriteLine(line);
        System.Diagnostics.Trace.TraceError(line);
    }

    public static readonly JsonSerializerOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    public static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
}
