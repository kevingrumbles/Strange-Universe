using Strange_Universe.Game.Components;
using System;
using System.IO;
using System.Linq;
using Strange_Universe.Game.Entities;
using Strange_Universe.Tests.Fakes;
using Xunit;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities.ShipParts;

namespace Strange_Universe.Tests;

/// <summary>A new universe is saved the moment it is created.</summary>
public class NewUniverseTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"su-new-{Guid.NewGuid():N}.json");

    [Fact]
    public void CreateUniverse_IsOnDisk_Immediately()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            var list = saves.LoadAll();

            var created = saves.CreateUniverse(list, "  My Universe  ");

            Assert.Contains(created, list);
            var onDisk = new FileSaveService(path).LoadAll().Single(); // a fresh read of the file, as after a restart
            Assert.Equal(created.Id, onDisk.Id);
            Assert.Equal("My Universe", onDisk.Name);
            Assert.Equal(created.Seed, onDisk.Seed);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateUniverse_WithABlankName_IsCalledNewUniverse(string name)
    {
        var saves = new InMemorySaveService();
        var created = saves.CreateUniverse(new System.Collections.Generic.List<Universe>(), name);
        Assert.Equal("New Universe", created.Name);
        Assert.Single(saves.Stored);
    }

    [Fact]
    public void ANewUniverse_SurvivesLosingTheSession()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            var list = saves.LoadAll();
            var created = saves.CreateUniverse(list, "Doomed");

            // The player is destroyed in the very first session: nothing more is saved, and the menu reloads from disk.
            created.Player.CurrentHullStrength = 0;
            saves.ReloadInto(list);

            var reloaded = list.Single();
            Assert.Equal("Doomed", reloaded.Name);
            Assert.False(reloaded.Player.IsDestroyed); // as it was when created
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ASavedNewUniverse_CanBeLoadedAndPlayed()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            saves.CreateUniverse(saves.LoadAll(), "Fresh");

            var loaded = new FileSaveService(path).LoadAll().Single();
            loaded.NebulaFactory = Nebula.CreateWithoutPixels;
            loaded.Generate(new RecordingAssetRequests()); // the same thing entering it from the menu does

            Assert.Equal("Sol", loaded.ActiveStarSystem.Name);
            Assert.NotNull(loaded.Player.StarSystem);
            loaded.Dispose();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void SeveralNewUniverses_AreAllKept()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            var list = saves.LoadAll();
            saves.CreateUniverse(list, "One");
            saves.CreateUniverse(list, "Two");

            Assert.Equal(new[] { "One", "Two" }, new FileSaveService(path).LoadAll().Select(u => u.Name));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    // -- the spawn point ----------------------------------------------------

    private static void StepPlayer(Universe universe, int frames = 5)
    {
        for (int i = 0; i < frames; i++)
            universe.Player.Update(0.016f, new Strange_Universe.Game.Components.InputState());
    }

    private static Universe LoadAndEnter(string path)
    {
        var universe = new FileSaveService(path).LoadAll().Single();
        universe.NebulaFactory = Nebula.CreateWithoutPixels;
        universe.Generate(new RecordingAssetRequests());
        return universe;
    }

    [Fact]
    public void ANewUniverse_CreatesItsPlayerWithNoLocation_AndSavesThat()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            var created = saves.CreateUniverse(saves.LoadAll(), "Unplaced");
            Assert.Null(created.Player.Location);

            // In the file the location is the player's "Position", saved as null.
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
            var player = json[0]["Player"].AsObject();
            Assert.True(player.ContainsKey("Position"));
            Assert.Null(player["Position"]);
            Assert.DoesNotContain(player, p => p.Key.Equals("NeedsSpawn", StringComparison.OrdinalIgnoreCase)); // no extra flag

            Assert.Null(new FileSaveService(path).LoadAll().Single().Player.Location);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void APlayerWithNoLocation_SpawnsWhenTheUniverseIsEntered_NotInsideTheStar()
    {
        string path = TempPath();
        try
        {
            new FileSaveService(path).CreateUniverse(new System.Collections.Generic.List<Universe>(), "Unplaced");

            var loaded = LoadAndEnter(path);
            Assert.Null(loaded.Player.Location);               // not placed yet
            Assert.True(loaded.Player.Navigator.Has<Strange_Universe.Game.NavSystem.SpawnTask>());

            StepPlayer(loaded);

            Assert.NotNull(loaded.Player.Location);
            Assert.Equal(loaded.ActiveStarSystem.MandevilleRadius * 0.9f, loaded.Player.Location.Value.Length(), 1); // the safe entry point
            loaded.Dispose();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void OncePlaced_TheLocationIsSaved_AndTheSpawnIsNotRepeated()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            saves.CreateUniverse(new System.Collections.Generic.List<Universe>(), "Placed");
            var first = LoadAndEnter(path);
            StepPlayer(first);
            var spawnPoint = first.Player.Position;
            saves.Save(first);
            first.Dispose();

            var second = LoadAndEnter(path);
            Assert.Equal(spawnPoint, second.Player.Location);
            Assert.False(second.Player.Navigator.Has<Strange_Universe.Game.NavSystem.SpawnTask>());
            StepPlayer(second);

            Assert.Equal(spawnPoint, second.Player.Position); // exactly where the first session left it
            second.Dispose();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void EnteringTheSameUniverseTwiceBeforeItSpawned_QueuesOnlyOneSpawn()
    {
        var universe = new Universe("Twice", "twice-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
        universe.Generate(new RecordingAssetRequests());
        universe.Dispose();
        universe.Generate(new RecordingAssetRequests()); // left and re-entered before the first frame ran

        Assert.Equal(1, universe.Player.Navigator.QueuedCount + (universe.Player.Navigator.HasActiveTask ? 1 : 0));
        universe.Dispose();
    }

    [Fact]
    public void ASaveWithAPosition_KeepsThePlayerWhereItWas_AndDoesNotSpawn()
    {
        // Every save from before the location could be null has a position: the player is already somewhere.
        string path = TempPath();
        try
        {
            var universe = new Universe("Old", "old-seed");
            universe.Player.Position = new Microsoft.Xna.Framework.Vector2(1234, -567);
            new FileSaveService(path).Save(universe);

            var loaded = LoadAndEnter(path);
            Assert.Equal(new Microsoft.Xna.Framework.Vector2(1234, -567), loaded.Player.Location);
            Assert.False(loaded.Player.Navigator.Has<Strange_Universe.Game.NavSystem.SpawnTask>());
            StepPlayer(loaded);

            Assert.Equal(new Microsoft.Xna.Framework.Vector2(1234, -567), loaded.Player.Position);
            loaded.Dispose();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void APlayerAtTheOrigin_IsPlaced_NotUnplaced()
    {
        // (0, 0) is a perfectly good saved position; only null means "not placed".
        var player = new Player("p", new Shuttle());
        Assert.Null(player.Location);

        player.Position = Microsoft.Xna.Framework.Vector2.Zero;

        Assert.Equal(Microsoft.Xna.Framework.Vector2.Zero, player.Location);
    }

    [Fact]
    public void SettingTheLocationToNull_MarksThePlayerUnplaced_AndAValueMarksItPlaced()
    {
        var player = new Player("p", new Shuttle()) { Location = new Microsoft.Xna.Framework.Vector2(5, 6) };
        Assert.Equal(new Microsoft.Xna.Framework.Vector2(5, 6), player.Position);

        player.Location = null;
        Assert.Null(player.Location);
    }
}
