using System.IO;
using System.Linq;
using Strange_Universe.Game.Entities;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 6: saving goes through <see cref="ISaveService"/>.</summary>
public class SaveServiceTests
{
    private static void ExerciseContract(ISaveService saves)
    {
        var a = new Universe("A", "seed-a");
        var b = new Universe("B", "seed-b");

        saves.Save(a);
        saves.Save(b);
        saves.Save(a); // saving again replaces, not duplicates

        Assert.Equal(new[] { "A", "B" }, saves.LoadAll().Select(u => u.Name).OrderBy(n => n));

        saves.Delete(a);

        Assert.Equal(new[] { "B" }, saves.LoadAll().Select(u => u.Name));
    }

    [Fact]
    public void InMemory_FollowsContract() => ExerciseContract(new InMemorySaveService());

    [Fact]
    public void File_FollowsContract()
    {
        string path = Path.Combine(Path.GetTempPath(), $"su-save-{System.Guid.NewGuid():N}.json");
        try { ExerciseContract(new FileSaveService(path)); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
