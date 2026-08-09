using Microsoft.Xna.Framework;
using Strange_Universe;
using Strange_Universe.Game.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        public static void Remove(Universe activeUniverse, string path)
        {
            var all = StaticHelpers.LoadExisting(path);
            int idx = all.FindIndex(u => u.Id == activeUniverse.Id);
            if (idx >= 0)
                all.RemoveAt(idx);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(all, StaticHelpers._writeOptions));
        }

        /// <summary>Deterministic 32-bit FNV-1a hash of a string.</summary>
        public static int SeedHash(string s)
        {
            unchecked
            {
                uint hash = 2166136261u;

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
        public static T LoadFile<T>(string path) where T : class
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
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
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
        /// Remaps UV coordinates to a periodic domain using sine/cosine so noise functions
        /// wrap seamlessly at u=0/1 and v=0/1 boundaries. Returns (x, y) in noise space.
        /// </summary>
        private static (float x, float y) MakePeriodicUV(float u, float v)
        {
            // Map [0,1] to angle [0, 2π], then to circle coordinates
            // This creates a seamless wrap because the circle has no edges
            float angleU = u * MathHelper.TwoPi;
            float angleV = v * MathHelper.TwoPi;

            // Project to 2D using two circles offset in 4D space
            // This avoids the singularity at the poles of a single circle
            float x = (float)Math.Cos(angleU) + (float)Math.Cos(angleV);
            float y = (float)Math.Sin(angleU) + (float)Math.Sin(angleV);

            return (x, y);
        }

        /// <summary>
        /// Tileable version of FBm that wraps seamlessly at u=0/1 and v=0/1.
        /// </summary>
        private static float TileableFbm(float u, float v, int seed,
                                          int octaves = 5, float persistence = 0.5f, float lacunarity = 2f)
        {
            var (x, y) = MakePeriodicUV(u, v);
            return ProceduralHelpers.Fbm(x, y, seed, octaves, persistence, lacunarity);
        }

        /// <summary>
        /// Tileable version of RidgedFbm that wraps seamlessly at u=0/1 and v=0/1.
        /// </summary>
        private static float TileableRidgedFbm(float u, float v, int seed,
                                                int octaves = 4, float persistence = 0.5f, float lacunarity = 2f)
        {
            var (x, y) = MakePeriodicUV(u, v);
            return ProceduralHelpers.RidgedFbm(x, y, seed, octaves, persistence, lacunarity);
        }

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
            float q0 = ProceduralHelpers.Fbm(u * 2.8f, v * 2.8f, seed, 3, 0.50f, 2.0f);
            float q1 = ProceduralHelpers.Fbm(u * 2.8f + 5.2f, v * 2.8f + 1.3f, seed + 1000, 3, 0.50f, 2.0f);
            float wu = u + q0 * 0.44f;
            float wv = v + q1 * 0.44f;

            // Primary cloud mass
            float densBase = ProceduralHelpers.Remap01(
                ProceduralHelpers.Fbm(wu * 3.5f, wv * 3.5f, seed + 2000, 4, 0.50f, 2.05f));

            // Ridged layer: bright filaments and sharpened cloud edges
            float densRidged = ProceduralHelpers.RidgedFbm(wu * 2.6f, wv * 2.6f, seed + 4000, 3);

            float total = densBase * 0.65f + densRidged * 0.35f;

            // Threshold removes thin uniform haze, leaving distinct cloud masses
            // separated by genuine dark voids.
            const float Threshold = 0.41f;
            float remapped = Math.Max(0f, total - Threshold) / (1f - Threshold);

            // Power curve: widens the contrast gap between thin wisps and dense cores
            return (float)Math.Pow(remapped, 1.6f);
        }

        /// <summary>
        /// Tileable version of NebulaLayerDensity that wraps seamlessly at u=0/1 and v=0/1.
        /// Used for infinite scrolling nebula backgrounds.
        /// </summary>
        public static float TileableNebulaLayerDensity(float u, float v, int seed)
        {
            // Domain warp using tileable FBm
            float q0 = TileableFbm(u, v, seed, 3, 0.50f, 2.0f);
            float q1 = TileableFbm(u, v, seed + 1000, 3, 0.50f, 2.0f);

            // Wrap the warped coordinates to stay in [0,1]
            float wu = (u + q0 * 0.44f);
            float wv = (v + q1 * 0.44f);
            wu = wu - (float)Math.Floor(wu); // Wrap to [0,1]
            wv = wv - (float)Math.Floor(wv);

            // Primary cloud mass using tileable FBm
            float densBase = ProceduralHelpers.Remap01(
                TileableFbm(wu, wv, seed + 2000, 4, 0.50f, 2.05f));

            // Ridged layer using tileable ridged FBm
            float densRidged = TileableRidgedFbm(wu, wv, seed + 4000, 3);

            float total = densBase * 0.65f + densRidged * 0.35f;

            // Threshold removes thin uniform haze
            const float Threshold = 0.41f;
            float remapped = Math.Max(0f, total - Threshold) / (1f - Threshold);

            // Power curve for contrast
            return (float)Math.Pow(remapped, 1.6f);
        }

        /// <summary>
        /// Smoothstep-interpolates the radial profile between the nearest two control angles.
        /// C¹ continuity avoids the hard corners produced by linear interpolation.
        /// </summary>
        public static float AsteroidInterpolatedRadius(float angle, float[] angles, float[] radii)
        {
            int n = angles.Length;
            float a = (angle + MathHelper.TwoPi) % MathHelper.TwoPi;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                float a0 = angles[i];
                float a1 = angles[next];
                if (next == 0) a1 += MathHelper.TwoPi;

                if (a >= a0 && a < a1)
                {
                    float t = (a - a0) / (a1 - a0);
                    t = t * t * (3f - 2f * t);   // smoothstep
                    return MathHelper.Lerp(radii[i], radii[next], t);
                }
            }
            return radii[0];
        }
        public static float StarDeltaAngle(float a, float b)
        {
            float d = (a - b + MathHelper.TwoPi) % MathHelper.TwoPi;
            if (d > MathHelper.Pi) d -= MathHelper.TwoPi;
            return d;
        }


        public enum OriginFaction
        {
            Human,
            Corporate,
            Republic,
            Pirate,
            Ancient,
            Alien
        }

        public enum CelestialNameType
        {
            Star,
            System,
            Planet
        }

        public enum Direction
        {
            Up,
            Down,
            Left,
            Right
        }

        public static string GetStarSystemName(string seed, OriginFaction faction = OriginFaction.Human)
        {
            Random universeRng = new Random(StaticHelpers.SeedHash(seed));
            string name = "Sol";
            var nodes = Launcher.ActiveUniverse?.StarSystemNodes;   
            if (nodes?.Count > 0)
            {
                while (name is null || nodes.Contains(nodes.Find(s => s.Name == name)))
                {
                    name = StaticHelpers.GenerateCelestialName(StaticHelpers.CelestialNameType.System, faction, universeRng);
                }
            }
            return name;
        }

        public static string GenerateCelestialName(CelestialNameType type, OriginFaction faction = OriginFaction.Human, Random random = null)
        {
            random ??= Random.Shared;

            string[] starts;
            string[] middles;
            string[] ends;

            switch (faction)
            {
                case OriginFaction.Human:
                    starts = ["Al", "Ar", "Bel", "Cal", "Dal", "El", "Far", "Gal", "Hel", "Kel", "Nor", "Tal", "Val", "West", "East", "New"];
                    middles = ["a", "e", "i", "o", "u", "ae", "ia", "or", "an", "el", "er", "is"];
                    ends = ["on", "ar", "us", "ia", " Prime", " Reach", " Point", " Haven", " Gate"];
                    break;

                case OriginFaction.Corporate:
                    starts = ["VX", "TR", "AX", "NT", "PR", "Sigma", "Nova", "Omni", "Core", "Helix"];
                    middles = ["-", "-", "-", "-", ""];
                    ends = [
                        random.Next(10,999).ToString(),
                $"{random.Next(1,99)}A",
                $"{random.Next(1,99)}X",
                "Station",
                "Hub"
                    ];
                    break;

                case OriginFaction.Republic:
                    starts = ["Aure", "Celes", "Imper", "Prae", "Victo", "Roma", "Solar", "Nova"];
                    middles = ["a", "e", "i", "o", "or", "an", "ae"];
                    ends = ["ius", "ium", "is", "a", "or", " Prime", " Secundus", " Tertius"];
                    break;

                case OriginFaction.Pirate:
                    starts = ["Black", "Dead", "Red", "Skull", "Broken", "Rag", "Scar", "Rust"];
                    middles = [" "];
                    ends = ["Rock", "Reach", "Cove", "Drift", "Haven", "Hole", "Nest"];
                    break;

                case OriginFaction.Ancient:
                    starts = ["Xa", "Qa", "Ul", "Vor", "Tha", "Esh", "Yth", "Zor"];
                    middles = ["ae", "io", "ua", "yth", "esh", "or", "il", "an"];
                    ends = ["os", "eth", "uun", "aar", "is", "yx", "oth"];
                    break;

                default: // Alien
                    starts = ["Zh", "Kr", "Vr", "Xe", "Qo", "Ss", "Th", "Ch"];
                    middles = ["aa", "ii", "uu", "ae", "oa", "yx", "ith", "orr"];
                    ends = ["q", "th", "k", "x", "ss", "rr", "n", "m"];
                    break;
            }

            string name;

            if (faction == OriginFaction.Corporate)
            {
                name = $"{starts[random.Next(starts.Length)]}{middles[random.Next(middles.Length)]}{ends[random.Next(ends.Length)]}";
            }
            else
            {
                var sb = new StringBuilder();

                sb.Append(starts[random.Next(starts.Length)]);

                int syllables = random.Next(1, 3);

                for (int i = 0; i < syllables; i++)
                    sb.Append(middles[random.Next(middles.Length)]);

                sb.Append(ends[random.Next(ends.Length)]);

                name = sb.ToString();
            }

            switch (type)
            {
                case CelestialNameType.Star:
                    if (random.NextDouble() < 0.25)
                        name += " " + (char)('A' + random.Next(26));
                    break;

                case CelestialNameType.System:
                    // Systems use the generated name directly.
                    break;

                case CelestialNameType.Planet:
                    if (random.NextDouble() < 0.60)
                    {
                        string[] suffixes =
                        [
                            " I"," II"," III"," IV"," V"," VI",
                    " Alpha"," Beta"," Gamma"," Delta"
                        ];

                        name += suffixes[random.Next(suffixes.Length)];
                    }
                    break;
            }

            return name;
        }
    }
}
