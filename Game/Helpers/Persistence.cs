using Strange_Universe.Game.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strange_Universe;

/// <summary>Save/load of universe data and data-path resolution.</summary>
public static class Persistence
{
    /// <summary>
    /// Resolves <paramref name="path"/> to a rooted, normalized path.
    /// Relative paths are anchored to the executable directory rather than the
    /// process working directory, which is not guaranteed to be the same.
    /// </summary>
    public static string ResolveDataPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    public static void Persist(Universe activeUniverse, string path)
    {
        if (activeUniverse == null) return;
        Update(path, "save", all =>
        {
            int idx = all.FindIndex(u => u.Id == activeUniverse.Id);
            if (idx >= 0) all[idx] = activeUniverse;
            else all.Add(activeUniverse);
        });
    }

    public static void Remove(Universe activeUniverse, string path)
    {
        if (activeUniverse == null) return;
        Update(path, "remove", all => all.RemoveAll(u => u.Id == activeUniverse.Id));
    }

    /// <summary>
    /// Loads all universes. On a parse failure the bad file is copied to a timestamped
    /// <c>.corrupt-*.bak</c> next to it, the error is logged, and an empty list is returned.
    /// </summary>
    public static List<Universe> LoadExisting(string path)
    {
        try
        {
            return TryLoad(ResolveDataPath(path), out var list) ? list : new List<Universe>();
        }
        catch (Exception e)
        {
            Log($"Failed to load universe settings from '{path}'", e);
            return new List<Universe>();
        }
    }

    /// <summary>Read-modify-write. Aborts (without touching the file) if the existing file can't be parsed.</summary>
    private static void Update(string path, string operation, Action<List<Universe>> mutate)
    {
        try
        {
            string fullPath = ResolveDataPath(path);
            if (!TryLoad(fullPath, out var all))
            {
                Log($"Skipped {operation}: '{fullPath}' could not be parsed and was not overwritten.", null);
                return;
            }

            mutate(all);
            WriteAtomic(fullPath, JsonSerializer.Serialize(all, _writeOptions));
        }
        catch (Exception e)
        {
            Log($"Failed to {operation} universe settings at '{path}'", e);
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
            list = JsonSerializer.Deserialize<List<Universe>>(json, _readOptions) ?? new List<Universe>();
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

    public static readonly JsonSerializerOptions _readOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    public static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
}
