using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StrangeUniverse.StaticHelpers;

namespace StrangeUniverse.Rendering.ProceduralGeneration;

public static class PlanetTextureGenerator
{
    private const int Size = 256;

    private static readonly Color[] TerranLow  = { new(30,  80,  160), new(25,  75,  155) }; // ocean
    private static readonly Color[] TerranMid  = { new(60,  130, 50),  new(80,  110, 40)  }; // land
    private static readonly Color[] TerranHigh = { new(110, 90,  60),  new(140, 120, 80)  }; // peaks

    public static Texture2D Generate(GraphicsDevice gd, PlanetType type, int seed)
    {
        var colors = new Color[Size * Size];
        float cx = Size * 0.5f;
        float cy = Size * 0.5f;
        float r  = cx - 2;

        // Light direction (slightly above left)
        var light = Vector3.Normalize(new Vector3(-0.4f, -0.6f, 0.8f));

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float d2 = dx * dx + dy * dy;
                if (d2 > r * r) continue;

                float dist = (float)Math.Sqrt(d2);
                float normDist = dist / r;

                // Sphere normal (Lambert)
                float nz = (float)Math.Sqrt(Math.Max(0f, 1f - normDist * normDist));
                var normal = Vector3.Normalize(new Vector3(dx / r, dy / r, nz));
                float diffuse = Math.Max(0f, Vector3.Dot(normal, light));
                float ambient = 0.25f;
                float lighting = ambient + (1f - ambient) * diffuse;

                // Sample noise for surface detail
                float noiseScale = 3.5f;
                float nx = (dx / r + 1f) * noiseScale;
                float ny = (dy / r + 1f) * noiseScale;
                float n = NoiseHelper.Remap01(NoiseHelper.Fbm(nx, ny, seed, 6, 0.5f, 2f));

                // Atmosphere edge glow
                float edgeFactor = 1f - normDist;

                Color surfaceColor = GetSurfaceColor(type, n, py, seed);

                // Darken surface by lighting
                surfaceColor = new Color(
                    (int)(surfaceColor.R * lighting),
                    (int)(surfaceColor.G * lighting),
                    (int)(surfaceColor.B * lighting));

                // Atmospheric rim
                Color atmColor = GetAtmosphereColor(type);
                float rimStrength = (float)Math.Pow(1f - edgeFactor, 4f);
                surfaceColor = Color.Lerp(surfaceColor, atmColor, rimStrength * 0.7f);

                // Planet body is fully opaque; only the outermost 5% fades for a soft edge
                byte alpha = normDist < 0.95f
                    ? (byte)255
                    : (byte)(Math.Max(0f, 1f - (normDist - 0.95f) / 0.05f) * 255f);
                colors[py * Size + px] = new Color(surfaceColor.R, surfaceColor.G, surfaceColor.B, alpha);
            }
        }

        var tex = new Texture2D(gd, Size, Size);
        tex.SetData(colors);
        return tex;
    }

    private static Color GetSurfaceColor(PlanetType type, float n, int py, int seed)
    {
        return type switch
        {
            PlanetType.Terran    => GetTerranColor(n),
            PlanetType.Rocky     => GetRockyColor(n),
            PlanetType.GasGiant  => GetGasGiantColor(n, py, seed),
            PlanetType.Ice       => GetIceColor(n),
            PlanetType.Lava      => GetLavaColor(n),
            PlanetType.Ocean     => GetOceanColor(n),
            _                    => Color.Gray,
        };
    }

    private static Color GetTerranColor(float n)
    {
        if (n < 0.42f) return Color.Lerp(new Color(30, 80, 160), new Color(45, 100, 190), n / 0.42f);
        if (n < 0.55f) return Color.Lerp(new Color(200, 175, 110), new Color(60, 130, 50), (n - 0.42f) / 0.13f);
        if (n < 0.78f) return Color.Lerp(new Color(60, 130, 50), new Color(110, 90, 60), (n - 0.55f) / 0.23f);
        return Color.Lerp(new Color(110, 90, 60), new Color(220, 215, 210), (n - 0.78f) / 0.22f);
    }

    private static Color GetRockyColor(float n)
    {
        var dark  = new Color(90,  70,  55);
        var mid   = new Color(140, 110, 85);
        var light = new Color(175, 155, 130);
        if (n < 0.5f) return Color.Lerp(dark, mid, n * 2f);
        return Color.Lerp(mid, light, (n - 0.5f) * 2f);
    }

    private static Color GetGasGiantColor(float n, int py, int seed)
    {
        // Horizontal banding
        float band = NoiseHelper.Remap01(NoiseHelper.Noise(py * 0.05f, 0f, seed + 9999)) * 0.3f;
        float t = (n + band) % 1f;
        if (t < 0.33f) return Color.Lerp(new Color(200, 150, 80), new Color(230, 180, 100), t * 3f);
        if (t < 0.66f) return Color.Lerp(new Color(190, 120, 60), new Color(210, 160, 90), (t - 0.33f) * 3f);
        return Color.Lerp(new Color(220, 170, 95), new Color(200, 130, 70), (t - 0.66f) * 3f);
    }

    private static Color GetIceColor(float n)
    {
        var deep  = new Color(180, 210, 240);
        var mid   = new Color(215, 230, 245);
        var white = new Color(240, 245, 255);
        if (n < 0.5f) return Color.Lerp(deep, mid, n * 2f);
        return Color.Lerp(mid, white, (n - 0.5f) * 2f);
    }

    private static Color GetLavaColor(float n)
    {
        if (n < 0.35f) return Color.Lerp(new Color(25, 15, 10), new Color(80, 20, 5), n / 0.35f);
        if (n < 0.5f)  return Color.Lerp(new Color(80, 20, 5), new Color(200, 60, 0), (n - 0.35f) / 0.15f);
        return Color.Lerp(new Color(200, 60, 0), new Color(255, 200, 20), (n - 0.5f) * 2f);
    }

    private static Color GetOceanColor(float n)
    {
        if (n < 0.6f) return Color.Lerp(new Color(20, 60, 140), new Color(30, 90, 180), n / 0.6f);
        return Color.Lerp(new Color(30, 90, 180), new Color(60, 130, 200), (n - 0.6f) * 2.5f);
    }

    private static Color GetAtmosphereColor(PlanetType type) => type switch
    {
        PlanetType.Terran   => new Color(100, 160, 255),
        PlanetType.Ice      => new Color(200, 230, 255),
        PlanetType.Ocean    => new Color(80,  140, 220),
        PlanetType.GasGiant => new Color(220, 170, 100),
        PlanetType.Lava     => new Color(200, 80,  20),
        _                   => new Color(160, 140, 120),
    };
}
