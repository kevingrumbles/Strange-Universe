using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

public class PersistenceRoundTripTests
{
    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Data", "sample-universe-settings.json");

    [Fact]
    public void Sample_Deserializes()
    {
        var universes = JsonSerializer.Deserialize<List<Universe>>(
            File.ReadAllText(SamplePath), FileSaveService.ReadOptions);

        Assert.NotNull(universes);
        Assert.Single(universes);
        Assert.Equal("Strife", universes[0].Name);
        Assert.NotNull(universes[0].Player);
        Assert.Equal(6, universes[0].StarSystemNodes.Count);
    }

    [Fact]
    public void Sample_RoundTrip_PreservesValues()
    {
        string original = File.ReadAllText(SamplePath);

        var universes = JsonSerializer.Deserialize<List<Universe>>(original, FileSaveService.ReadOptions);
        string reserialized = JsonSerializer.Serialize(universes, FileSaveService.WriteOptions);

        var expected = JsonNode.Parse(original);
        var actual = JsonNode.Parse(reserialized);

        Assert.True(JsonNode.DeepEquals(expected, actual),
            $"Round-trip mismatch.\nExpected:\n{expected}\nActual:\n{actual}");
    }

    [Fact]
    public void Persist_Then_LoadExisting_RoundTrips()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"su-test-{Guid.NewGuid():N}.json");
        try
        {
            var universes = JsonSerializer.Deserialize<List<Universe>>(
                File.ReadAllText(SamplePath), FileSaveService.ReadOptions);

            new FileSaveService(tmp).Save(universes[0]);
            var loaded = new FileSaveService(tmp).LoadAll();

            Assert.Single(loaded);
            Assert.Equal(universes[0].Id, loaded[0].Id);
            Assert.Equal(universes[0].Player.Position, loaded[0].Player.Position);
            Assert.Equal(universes[0].Player.Velocity, loaded[0].Player.Velocity);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void CorruptFile_IsBackedUp_AndNotOverwrittenBySave()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"su-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "universe-settings.json");
        try
        {
            const string garbage = "[{ \"Name\": broken";
            File.WriteAllText(file, garbage);

            Assert.Empty(new FileSaveService(file).LoadAll());
            Assert.Single(Directory.GetFiles(dir, "*.corrupt-*.bak"));

            new FileSaveService(file).Save(new Universe("X"));
            Assert.Equal(garbage, File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
