using System;
using System.Collections.Generic;
using System.IO;
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
            File.ReadAllText(SamplePath), Persistence._readOptions);

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

        var universes = JsonSerializer.Deserialize<List<Universe>>(original, Persistence._readOptions);
        string reserialized = JsonSerializer.Serialize(universes, Persistence._writeOptions);

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
                File.ReadAllText(SamplePath), Persistence._readOptions);

            Persistence.Persist(universes[0], tmp);
            var loaded = Persistence.LoadExisting(tmp);

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

            Assert.Empty(Persistence.LoadExisting(file));
            Assert.Single(Directory.GetFiles(dir, "*.corrupt-*.bak"));

            Persistence.Persist(new Universe("X"), file);
            Assert.Equal(garbage, File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
