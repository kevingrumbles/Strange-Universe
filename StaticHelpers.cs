using StrangeUniverse.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StrangeUniverse
{
    public static class StaticHelpers
    {
        public static void Persist(Universe activeUniverse, string path)
        {
            var all = StaticHelpers.LoadExisting(path);
            int idx = all.FindIndex(u => u.Id == activeUniverse.Id);
            if (idx >= 0)
                all[idx] = activeUniverse;
            else
                all.Add(activeUniverse);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(all, StaticHelpers._writeOptions));
        }

        /// <summary>Deterministic 32-bit FNV-1a hash of a string.</summary>
        public static int SeedHash(string s)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in s)
                    hash = (hash ^ c) * 16777619u;
                return (int)hash;
            }
        }
        public static float NextWeightedFloat(
            this Random random,
            float? min,
            float? max,
            int samples = 3)
        {
            float sum = 0;

            for (int i = 0; i < samples; i++)
                sum += (float)random.NextDouble();

            float normalized = sum / samples;

            return (min ?? 0) + normalized * ((max ?? 1) - (min ?? 0));
        }
        public static T? LoadFile<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                var deserialized = JsonSerializer.Deserialize<T>(json, _readOptions);
                return deserialized;
            }
            catch
            {
                return null;
            }
        }

        public static List<Universe> LoadExisting(string path)
        {
            if (!File.Exists(path)) return new List<Universe>();
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<Universe>>(json, _readOptions) ?? new List<Universe>();
            }
            catch (Exception e)
            {
                System.Console.WriteLine($"[Strange Universe] Failed to load universe settings: {e.Message}");
                return new List<Universe>();
            }
        }

        public static readonly JsonSerializerOptions _readOptions = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
        };

        public static readonly JsonSerializerOptions _writeOptions = new()
        {
            WriteIndented = true,
            IncludeFields = true,
        };

        public static readonly string[] PlanetNames =
        {
        "Aether", "Boras", "Calyss", "Drevon", "Eston",
        "Fyrath", "Gavorn", "Helix", "Iridia", "Joras"
        };
    }
}
