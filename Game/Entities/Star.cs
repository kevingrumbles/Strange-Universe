using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StrangeUniverse.Game.Components;
using System;

namespace StrangeUniverse.Game.Entities;

public class Star
{
    public string    Id { get; }
    public Transform Transform    { get; } = new();
    public float     Radius       { get; set; }
    public string    Name         { get; set; } = "Sol";
    public float     OrbitRadius  { get; set; }
    public float     OrbitAngle   { get; set; }
    public float     OrbitSpeed   { get; set; }
    public Color     MinimapColor { get; set; }
    public GravityWell GravityWell { get; private set; }

    private const int Size = 512;

    public Star(string id, float radius, string name, Color minimapColor, float orbitRadius, float orbitAngle, float orbitSpeed)
    {
        Id = id;
        Radius = radius;
        Name = name;
        OrbitRadius = orbitRadius;
        OrbitAngle = orbitAngle;
        OrbitSpeed = orbitSpeed;
        Random starRng = new Random(StaticHelpers.SeedHash($"{Id}_{Name}"));


        // Create gravity well based on star size
        float wellRadius = Radius * MathHelper.Lerp(4f, 8f, (float)starRng.NextDouble());
        // Stars use 80-100% of max gravity force at their center
        float wellStrength = MathHelper.Lerp(0.8f, 1.0f, (float)starRng.NextDouble());
        GravityWell = new GravityWell(Transform.Position, wellRadius, wellStrength);

        var tex = Generate(minimapColor, starRng);
        Launcher.TextureCache.Register(Id, tex);
    }

    /// <summary>
    /// Generates a glowing star texture: bright core, coloured corona, soft halo, and ray spikes.
    /// </summary>
    public static Texture2D Generate(Color starColor, Random rng)
    {
        var colors = new Color[Size * Size];
        float cx = Size * 0.5f;
        float cy = Size * 0.5f;
        float maxR = cx;

        // Generate 8 ray angles
        float[] rayAngles = new float[8];
        for (int i = 0; i < 8; i++)
            rayAngles[i] = MathHelper.TwoPi * i / 8f + (float)rng.NextDouble() * 0.2f;

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float normDist = dist / maxR;   // 0 = centre, 1 = edge

                if (normDist > 1f) continue;

                // Core glow — inverse square falloff
                float glow = Math.Max(0f, 1f - normDist);
                glow = glow * glow * glow;

                // Ray contribution
                float angle = (float)Math.Atan2(dy, dx);
                float rayContrib = 0f;
                foreach (float ra in rayAngles)
                {
                    float angleDiff = Math.Abs(StaticHelpers.StarDeltaAngle(angle, ra));
                    float rayWidth = 0.04f + normDist * 0.03f;
                    if (angleDiff < rayWidth)
                    {
                        float rayFall = 1f - angleDiff / rayWidth;
                        rayContrib = Math.Max(rayContrib, rayFall * (1f - normDist) * 0.6f);
                    }
                }

                float totalLight = Math.Min(1f, glow + rayContrib);
                if (totalLight <= 0f) continue;

                // Colour: white at core, starColor mid, transparent at edge
                Color pixel;
                if (normDist < 0.08f)
                    pixel = Color.Lerp(Color.White, starColor, normDist / 0.08f);
                else
                    pixel = Color.Lerp(starColor, Color.Transparent, (normDist - 0.08f) / 0.92f);

                byte alpha = (byte)Math.Min(255, (int)(totalLight * 255f));
                colors[py * Size + px] = new Color(pixel.R, pixel.G, pixel.B, alpha);
            }
        }

        var tex = new Texture2D(Launcher.GD, Size, Size);
        tex.SetData(colors);
        return tex;
    }
}
