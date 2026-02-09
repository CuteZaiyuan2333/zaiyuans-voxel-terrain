using System;
using Godot;
using NUnit.Framework;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.Data;

namespace ZaiyuansVoxelWorld.Tests;

[TestFixture]
public class ChunkStorageTests
{
	private string _tempDir;

	[SetUp]
	public void SetUp()
	{
		_tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VoxelChunkStorageTests_" + Guid.NewGuid().ToString("N")[..8]);
	}

	[TearDown]
	public void TearDown()
	{
		if (!string.IsNullOrEmpty(_tempDir) && System.IO.Directory.Exists(_tempDir))
		{
			try { System.IO.Directory.Delete(_tempDir, true); } catch { /* ignore */ }
		}
	}

	[Test]
	public void GetChunkPath_empty_dir_returns_empty()
	{
		Assert.That(ChunkStorage.GetChunkPath("", 0, 0, 0), Is.EqualTo(string.Empty));
		Assert.That(ChunkStorage.GetChunkPath(null!, 0, 0, 0), Is.EqualTo(string.Empty));
	}

	[Test]
	public void GetChunkPath_returns_expected_subpath()
	{
		string p = ChunkStorage.GetChunkPath(_tempDir, 1, -2, 3);
		Assert.That(p, Does.Contain("chunks"));
		Assert.That(p, Does.EndWith("1_-2_3.chunk"));
	}

	[Test]
	public void Save_empty_dir_returns_false()
	{
		var data = new byte[VoxelConstants.ChunkVolume];
		Assert.That(ChunkStorage.Save("", new Vector3I(0, 0, 0), data), Is.False);
		Assert.That(ChunkStorage.Save(null!, new Vector3I(0, 0, 0), data), Is.False);
	}

	[Test]
	public void Save_too_small_data_returns_false()
	{
		var data = new byte[100];
		Assert.That(ChunkStorage.Save(_tempDir, new Vector3I(0, 0, 0), data), Is.False);
	}

	[Test]
	public void Save_and_TryLoad_roundtrip()
	{
		var data = new byte[VoxelConstants.ChunkVolume];
		for (int i = 0; i < data.Length; i++)
			data[i] = (byte)(i % 256);
		var chunkPos = new Vector3I(2, 3, -1);
		Assert.That(ChunkStorage.Save(_tempDir, chunkPos, data), Is.True);

		var buffer = new byte[VoxelConstants.ChunkVolume];
		Assert.That(ChunkStorage.TryLoad(_tempDir, chunkPos, buffer), Is.True);
		for (int i = 0; i < data.Length; i++)
			Assert.That(buffer[i], Is.EqualTo(data[i]), $"index {i}");
	}

	[Test]
	public void TryLoad_nonexistent_returns_false()
	{
		var buffer = new byte[VoxelConstants.ChunkVolume];
		Assert.That(ChunkStorage.TryLoad(_tempDir, new Vector3I(99, 99, 99), buffer), Is.False);
	}

	[Test]
	public void TryLoad_null_or_small_buffer_returns_false()
	{
		Assert.That(ChunkStorage.TryLoad(_tempDir, new Vector3I(0, 0, 0), null!), Is.False);
		var small = new byte[100];
		Assert.That(ChunkStorage.TryLoad(_tempDir, new Vector3I(0, 0, 0), small), Is.False);
	}
}
