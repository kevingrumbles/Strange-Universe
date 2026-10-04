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

        try
        {
            string fullPath = ResolveDataPath(path);

            var all = LoadExisting(fullPath);
            int idx = all.FindIndex(u => u.Id == activeUniverse.Id);
            if (idx >= 0)
                all[idx] = activeUniverse;
            else
                all.Add(activeUniverse);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            string json = JsonSerializer.Serialize(all, _writeOptions);
            File.WriteAllText(fullPath, json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Strange Universe] Failed to save universe settings: {e}");
        }
    }

    public static void Remove(Universe activeUniverse, string path)
    {
        try
        {
            string fullPath = ResolveDataPath(path);

            var all = LoadExisting(fullPath);
            int idx = all.FindIndex(u => u.Id == activeUniverse.Id);
            if (idx >= 0)
                all.RemoveAt(idx);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            File.WriteAllText(fullPath, JsonSerializer.Serialize(all, _writeOptions));
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Strange Universe] Failed to remove universe settings: {e}");
        }
    }

    public static List<Universe> LoadExisting(string path)
    {
        try
        {
            string fullPath = ResolveDataPath(path);
            if (!File.Exists(fullPath)) return new List<Universe>();

            string json = File.ReadAllText(fullPath);
            return JsonSerializer.Deserialize<List<Universe>>(json, _readOptions) ?? new List<Universe>();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Strange Universe] Failed to load universe settings: {e.Message}");
            return new List<Universe>();
        }
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
