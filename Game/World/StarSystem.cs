using System.Collections.Generic;
using StrangeUniverse.Game.Entities;
using StrangeUniverse.Game.Systems;
using StrangeUniverse.Input;

namespace StrangeUniverse.Game.World;

/// <summary>Owns all game entities and drives the frame update.</summary>
public class StarSystem
{
    public Star                   Star            { get; set; } = new();
    public PlayerShip             Player          { get; set; } = null!;
    public List<Planet>           Planets         { get; }      = new();
    public List<Asteroid>         Asteroids       { get; }      = new();
    public List<BackgroundStar>   BackgroundStars { get; }      = new();
    public string                 NebulaTextureId { get; set; } = string.Empty;

    private readonly PhysicsSystem   _physics   = new();
    private readonly CollisionSystem _collision = new();

    public void Update(float deltaTime, InputState input)
    {
        Player.Update(deltaTime, input);
        _physics.Update(Asteroids, deltaTime);
        _collision.Resolve(Player, Planets, Asteroids);
    }
}
