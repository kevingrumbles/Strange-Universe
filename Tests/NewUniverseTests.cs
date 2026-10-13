using System;
using System.IO;
using System.Linq;
using Strange_Universe.Game.Entities;
using Strange_Universe.Tests.Fakes;
using Xunit;

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
    public void ANewUniverse_RecordsThatItsPlayerStillNeedsToSpawn_InTheSaveFile()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            var created = saves.CreateUniverse(saves.LoadAll(), "Unspawned");
            Assert.True(created.Player.NeedsSpawn);

            Assert.Contains("NeedsSpawn", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
            Assert.True(new FileSaveService(path).LoadAll().Single().Player.NeedsSpawn);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ASavedNewUniverse_SpawnsThePlayerWhenItIsLoaded_NotInsideTheStar()
    {
        string path = TempPath();
        try
        {
            new FileSaveService(path).CreateUniverse(new System.Collections.Generic.List<Universe>(), "Unspawned");

            var loaded = LoadAndEnter(path);
            Assert.Equal(Microsoft.Xna.Framework.Vector2.Zero, loaded.Player.Position); // before the first frame

            StepPlayer(loaded);

            float distance = loaded.Player.Position.Length();
            Assert.Equal(loaded.ActiveStarSystem.MandevilleRadius * 0.9f, distance, 1); // the safe entry point
            Assert.False(loaded.Player.NeedsSpawn);
            loaded.Dispose();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void OnceSpawned_TheSpawnPointIsPersisted_AndNotRepeated()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            saves.CreateUniverse(new System.Collections.Generic.List<Universe>(), "Spawned");
            var first = LoadAndEnter(path);
            StepPlayer(first);
            var spawnPoint = first.Player.Position;
            saves.Save(first);
            first.Dispose();

            var second = LoadAndEnter(path);
            Assert.False(second.Player.NeedsSpawn);
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
    public void ASaveFromBeforeNeedsSpawnExisted_KeepsThePlayerWhereItWas()
    {
        // An old save has no NeedsSpawn (it reads as false): its player is already somewhere, and must not be moved.
        string path = TempPath();
        try
        {
            var universe = new Universe("Old", "old-seed");
            universe.Player.NeedsSpawn = false;
            universe.Player.Position = new Microsoft.Xna.Framework.Vector2(1234, -567);
            new FileSaveService(path).Save(universe);
            string withoutProperty = System.Text.RegularExpressions.Regex.Replace(
                File.ReadAllText(path), @"""needsSpawn""\s*:\s*(true|false),?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            File.WriteAllText(path, withoutProperty);
            Assert.DoesNotContain("needsspawn", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);

            var loaded = LoadAndEnter(path);
            StepPlayer(loaded);

            Assert.False(loaded.Player.NeedsSpawn);
            Assert.Equal(new Microsoft.Xna.Framework.Vector2(1234, -567), loaded.Player.Position);
            loaded.Dispose();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
