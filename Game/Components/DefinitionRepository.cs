using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Components;

/// <summary>A weapon as written in <c>Definitions/weapons.json</c>.</summary>
public sealed class WeaponDefinition
{
    public string Name { get; set; }
    public EquipmentType Type { get; set; }
    public int? Damage { get; set; }
    public float? Speed { get; set; }
    public float? Range { get; set; }
    public float? FireRate { get; set; }
    public float? EnergyCost { get; set; }
    public float? Mass { get; set; }
    public float? Accuracy { get; set; }

    /// <summary>Seconds a spent projectile stays in the world after impact.</summary>
    public float ImpactLingerSeconds { get; set; } = 0.18f;

    /// <summary>Key into <see cref="DefinitionRepository.VisualStyles"/>; the renderer decides what it looks like.</summary>
    public string VisualStyle { get; set; } = DefinitionRepository.DefaultStyleName;

    public Equipment ToEquipment() => new()
    {
        EquipmentName = Name,
        EquipmentType = Type,
        Damage = Damage,
        Speed = Speed,
        Range = Range,
        FireRate = FireRate,
        EnergyCost = EnergyCost,
        Mass = Mass,
        Accuracy = Accuracy,
        ImpactLingerSeconds = ImpactLingerSeconds,
    };
}

/// <summary>
/// Projectile appearance as written in JSON. Plain numbers only (colours are [r, g, b]), so this
/// layer stays graphics-free; the renderer turns it into a <c>ProjectileVisual</c>.
/// </summary>
public sealed class ProjectileStyleDefinition
{
    public string Style { get; set; } = "laser";
    public int[] CoreColor { get; set; } = { 255, 255, 255 };
    public int CoreLength { get; set; }
    public int CoreWidth { get; set; }
    public int[] GlowColor { get; set; } = { 255, 255, 255 };
    public int GlowRadius { get; set; }
    public float GlowIntensity { get; set; }
    public int TrailLength { get; set; }
    public int TrailWidth { get; set; }
    public float TrailAlpha { get; set; }
    public float PulseSpeed { get; set; }
    public float PulseAmount { get; set; }
    public int ParticleCount { get; set; }
    public float BurstDuration { get; set; }
    public float BurstRadius { get; set; }
    public int BurstParticleCount { get; set; }
    public float BurstParticleSpeed { get; set; }
}

/// <summary>
/// Loads game definitions (currently weapons and their projectile styles) from JSON.
/// <see cref="Default"/> reads <c>Definitions/weapons.json</c> next to the executable when present,
/// otherwise the copy embedded in the assembly, so adding a weapon needs no code change.
/// </summary>
public sealed class DefinitionRepository
{
    public const string DefaultStyleName = "default";
    private const string EmbeddedName = "Strange_Universe.Definitions.weapons.json";
    private const string FilePath = "Definitions/weapons.json";

    private sealed class Document
    {
        public List<WeaponDefinition> Weapons { get; set; } = new();
        public Dictionary<string, ProjectileStyleDefinition> VisualStyles { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static DefinitionRepository _default;

    /// <summary>The repository the game uses. Tests may replace it.</summary>
    public static DefinitionRepository Default
    {
        get => _default ??= LoadDefault();
        set => _default = value;
    }

    public IReadOnlyList<WeaponDefinition> Weapons { get; }
    public IReadOnlyDictionary<string, ProjectileStyleDefinition> VisualStyles { get; }

    private DefinitionRepository(Document doc)
    {
        Weapons = doc.Weapons;
        VisualStyles = new Dictionary<string, ProjectileStyleDefinition>(doc.VisualStyles, StringComparer.OrdinalIgnoreCase);
    }

    public static DefinitionRepository Load(Stream json)
    {
        var doc = JsonSerializer.Deserialize<Document>(json, Options)
                  ?? throw new InvalidDataException("Definitions file is empty.");
        return new DefinitionRepository(doc);
    }

    public static DefinitionRepository Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    private static DefinitionRepository LoadDefault()
    {
        string file = Path.Combine(AppContext.BaseDirectory, FilePath);
        if (File.Exists(file))
            return Load(file);

        using var embedded = typeof(DefinitionRepository).Assembly.GetManifestResourceStream(EmbeddedName)
                             ?? throw new FileNotFoundException($"Embedded definitions '{EmbeddedName}' not found.");
        return Load(embedded);
    }

    public WeaponDefinition FindWeapon(string name) =>
        name == null ? null : Weapons.FirstOrDefault(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Equipment for every weapon definition, in file order.</summary>
    public List<Equipment> CreateWeapons() => Weapons.Select(w => w.ToEquipment()).ToList();

    /// <summary>The style used by <paramref name="weaponName"/>, or the default style when the weapon or style is unknown.</summary>
    public ProjectileStyleDefinition StyleForWeapon(string weaponName)
    {
        string key = FindWeapon(weaponName)?.VisualStyle ?? DefaultStyleName;
        return VisualStyles.TryGetValue(key, out var style) ? style
             : VisualStyles.TryGetValue(DefaultStyleName, out var fallback) ? fallback
             : null;
    }
}
