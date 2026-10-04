using System;
using System.Text;

namespace Strange_Universe;

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

/// <summary>Procedural names for stars, systems and planets.</summary>
public static class NameGenerator
{
    public static string GetStarSystemName(string seed, System.Collections.Generic.IReadOnlyList<Strange_Universe.Game.Entities.StarSystemNode> existingNodes = null, OriginFaction faction = OriginFaction.Human)
    {
        Random universeRng = new Random(ProceduralHelpers.SeedHash(seed));
        string name = "Sol";
        if (existingNodes?.Count > 0)
        {
            while (name is null || System.Linq.Enumerable.Any(existingNodes, s => s.Name == name))
            {
                name = GenerateCelestialName(CelestialNameType.System, faction, universeRng);
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
