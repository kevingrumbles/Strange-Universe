using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 5: nebula pixel output must stay byte-identical when its colour type changes.</summary>
public class NebulaPixelTests
{
    [Fact]
    public void Nebula_Pixels_MatchGoldenHash()
    {
        var nebula = new Nebula("golden-nebula-pool_0");

        string hash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(nebula.Pixels.AsSpan())));

        
        Assert.Equal("D91BE27860EA2783C06332077479D33BE520A375541DD660D2AC3E1434FDB7D0", hash);
    }
}
