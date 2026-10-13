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
}
