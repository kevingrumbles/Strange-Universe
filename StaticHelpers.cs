using Microsoft.Xna.Framework;
using StrangeUniverse.Game.World;
using StrangeUniverse.Rendering.ProceduralGeneration;
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

                if (s is null)
                {
                    string test = "";
                }

                foreach (char c in s ?? string.Empty)
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

        public static readonly string[] StarNames =
        {
            "Sol", "Lumen", "Ignis", "Astra", "Nova",
            "Vega", "Sirius", "Altair", "Rigel", "Polaris",
            "Deneb", "Arcturus", "Betelgeuse", "Proxima Centauri", "Alpha Centauri"
        };

        public static readonly string[] StarSystemNames =
        {
            "Alpha Centauri", "Betelgeuse", "Sirius", "Vega", "Proxima Centauri",
            "Rigel", "Polaris", "Altair", "Deneb", "Arcturus"
        };

        public static readonly Color[] StarColors = new[]
        {
            new Color(255, 240, 180),   // warm yellow (G-type)
            new Color(255, 200, 120),   // orange (K-type)
            new Color(255, 160, 80),    // orange-red (M-type)
            new Color(180, 210, 255),   // blue-white (A-type)
        };

        public static Color PlanetMinimapColor(PlanetType type) => type switch
        {
            PlanetType.Terran => new Color(70, 160, 220),
            PlanetType.Rocky => new Color(160, 130, 100),
            PlanetType.GasGiant => new Color(210, 170, 100),
            PlanetType.Ice => new Color(200, 225, 255),
            PlanetType.Lava => new Color(220, 70, 30),
            PlanetType.Ocean => new Color(30, 100, 200),
            _ => Color.LightGray,
        };

        public enum PlanetType { Terran, Rocky, GasGiant, Ice, Lava, Ocean }
        public static PlanetType[] PlanetTypes = (PlanetType[])Enum.GetValues(typeof(PlanetType));

        /// <summary>Wraps an angle to the range [−π, π] for the shortest-path rotation calc.</summary>
        public static float WrapAngle(float angle)
        {
            angle %= MathHelper.TwoPi;
            if (angle > MathHelper.Pi) angle -= MathHelper.TwoPi;
            if (angle < -MathHelper.Pi) angle += MathHelper.TwoPi;
            return angle;
        }

        // Eight vivid hues spread across the color wheel so any triplet produces
        // clearly distinct, strongly-contrasting color regions.
        public static readonly Color[] NebulaColorPool =
        {
        new Color(215,  40,  45),   // 0  crimson
        new Color(235, 115,  15),   // 1  orange
        new Color( 40,  75, 220),   // 2  cobalt blue
        new Color( 20, 185,  80),   // 3  emerald green
        new Color(200,  20, 190),   // 4  magenta
        new Color( 80,  15, 215),   // 5  deep purple
        new Color( 15, 195, 215),   // 6  cyan / teal
        new Color(220, 195,  20)};  // 7  gold

        // Each triplet is hand-picked so the three colors are well-separated
        // in hue, guaranteeing visible color variety in every nebula.
        public static readonly int[][] NebulaTriplets =
        {
        new[] { 0, 2, 4 },  // Crimson  + Blue    + Magenta
        new[] { 1, 2, 5 },  // Orange   + Blue    + Purple
        new[] { 0, 3, 2 },  // Crimson  + Emerald + Blue
        new[] { 4, 1, 2 },  // Magenta  + Orange  + Blue
        new[] { 5, 1, 6 },  // Purple   + Orange  + Cyan
        new[] { 2, 3, 4 },  // Blue     + Emerald + Magenta
        new[] { 4, 6, 1 },  // Magenta  + Cyan    + Orange
        new[] { 5, 0, 6 }}; // Purple   + Crimson + Cyan

        /// <summary>
        /// Computes one cloud layer's density at (u, v) using a domain-warped FBm
        /// mixed with ridged noise.  Returns [0, 1]: 0 = dark void, 1 = dense core.
        /// Each unique <paramref name="seed"/> produces an entirely different shape.
        /// </summary>
        public static float NebulaLayerDensity(float u, float v, int seed)
        {
            // Domain warp: displace the sample point with FBm so the resulting
            // cloud has organic curves, spirals, and trailing tendrils rather than
            // the repeating blobs that plain FBm produces.
            float q0 = NoiseHelper.Fbm(u * 2.8f, v * 2.8f, seed, 3, 0.50f, 2.0f);
            float q1 = NoiseHelper.Fbm(u * 2.8f + 5.2f, v * 2.8f + 1.3f, seed + 1000, 3, 0.50f, 2.0f);
            float wu = u + q0 * 0.44f;
            float wv = v + q1 * 0.44f;

            // Primary cloud mass
            float densBase = NoiseHelper.Remap01(
                NoiseHelper.Fbm(wu * 3.5f, wv * 3.5f, seed + 2000, 4, 0.50f, 2.05f));

            // Ridged layer: bright filaments and sharpened cloud edges
            float densRidged = NoiseHelper.RidgedFbm(wu * 2.6f, wv * 2.6f, seed + 4000, 3);

            float total = densBase * 0.65f + densRidged * 0.35f;

            // Threshold removes thin uniform haze, leaving distinct cloud masses
            // separated by genuine dark voids.
            const float Threshold = 0.41f;
            float remapped = Math.Max(0f, total - Threshold) / (1f - Threshold);

            // Power curve: widens the contrast gap between thin wisps and dense cores
            return (float)Math.Pow(remapped, 1.6f);
        }
    }
}
