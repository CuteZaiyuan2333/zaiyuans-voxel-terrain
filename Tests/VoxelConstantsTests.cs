using Godot;
using NUnit.Framework;
using ZaiyuansVoxelWorld.Core;

namespace ZaiyuansVoxelWorld.Tests;

[TestFixture]
public class VoxelConstantsTests
{
    [Test]
    public void ChunkSize_constants()
    {
        Assert.That(VoxelConstants.ChunkSize, Is.EqualTo(32));
        Assert.That(VoxelConstants.ChunkSizeShift, Is.EqualTo(5));
        Assert.That(VoxelConstants.ChunkSizeMask, Is.EqualTo(31));
        Assert.That(VoxelConstants.ChunkSizeSq, Is.EqualTo(1024));
        Assert.That(VoxelConstants.ChunkVolume, Is.EqualTo(32768));
    }

    [Test]
    [TestCase(0, 0)]
    [TestCase(31, 0)]
    [TestCase(32, 1)]
    [TestCase(63, 1)]
    [TestCase(64, 2)]
    [TestCase(-1, -1)]
    [TestCase(-32, -1)]
    [TestCase(-33, -2)]
    public void WorldToChunkCoord(int worldCoord, int expectedChunk)
    {
        Assert.That(VoxelConstants.WorldToChunkCoord(worldCoord), Is.EqualTo(expectedChunk));
    }

    [Test]
    [TestCase(0, 0)]
    [TestCase(31, 31)]
    [TestCase(32, 0)]
    [TestCase(-1, 31)]
    [TestCase(-32, 0)]
    [TestCase(-33, 31)]
    public void WorldToLocalCoord(int worldCoord, int expectedLocal)
    {
        Assert.That(VoxelConstants.WorldToLocalCoord(worldCoord), Is.EqualTo(expectedLocal));
    }

    [Test]
    public void ChunkToWorldOrigin()
    {
        var p = new Vector3I(0, 0, 0);
        Assert.That(VoxelConstants.ChunkToWorldOrigin(p), Is.EqualTo(new Vector3I(0, 0, 0)));
        p = new Vector3I(1, 0, -1);
        Assert.That(VoxelConstants.ChunkToWorldOrigin(p), Is.EqualTo(new Vector3I(32, 0, -32)));
    }

    [Test]
    [TestCase(0, 0, 0, 0)]
    [TestCase(31, 0, 0, 31)]
    [TestCase(0, 1, 0, 32)]
    [TestCase(0, 0, 1, 1024)]
    [TestCase(31, 31, 31, 32767)]
    public void LocalToIndex(int lx, int ly, int lz, int expectedIndex)
    {
        Assert.That(VoxelConstants.LocalToIndex(lx, ly, lz), Is.EqualTo(expectedIndex));
    }

    [Test]
    public void GetNeighborChunkPositions()
    {
        var six = new Vector3I[6];
        VoxelConstants.GetNeighborChunkPositions(new Vector3I(0, 0, 0), six);
        Assert.That(six[0], Is.EqualTo(new Vector3I(-1, 0, 0)));
        Assert.That(six[1], Is.EqualTo(new Vector3I(1, 0, 0)));
        Assert.That(six[2], Is.EqualTo(new Vector3I(0, -1, 0)));
        Assert.That(six[3], Is.EqualTo(new Vector3I(0, 1, 0)));
        Assert.That(six[4], Is.EqualTo(new Vector3I(0, 0, -1)));
        Assert.That(six[5], Is.EqualTo(new Vector3I(0, 0, 1)));
    }
}
