using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VoxelData
{
    /// <summary>
    /// The width of a chunk in voxels.
    /// </summary>
    public static readonly int ChunkWidth = 16;

    /// <summary>
    /// The height of a chunk in voxels.
    /// </summary>
    public static readonly int ChunkHeight = 128;

    /// <summary>
    /// The size of the world in chunks.
    /// </summary>
    public static readonly int WorldSizeInChunks = 50;
    public static readonly int ViewDistanceInChunks = 5;

    /// <summary>
    /// The size of the world in voxels.
    /// </summary>
    public static int WorldSizeInVoxels {
        get { return WorldSizeInChunks * ChunkWidth; }
    }

    /// <summary>
    /// The size of the texture atlas in voxels.
    /// </summary>
    public static readonly int TextureAtlasSize = 16;

    /// <summary>
    /// The normalized size of a block texture.
    /// </summary>
    public static float NormalizedBlockTextureSize
    {
        get { return 1f / (float)TextureAtlasSize; }
    }

    /// <summary>
    /// The vertices of a voxel (clockwise)
    /// </summary>
    public static readonly Vector3[] VoxelVerts = new Vector3[8] {
        new Vector3(0f, 0f, 0f),
        new Vector3(1f, 0f, 0f),
        new Vector3(1f, 1f, 0f),
        new Vector3(0f, 1f, 0f),
        new Vector3(0f, 0f, 1f),
        new Vector3(1f, 0f, 1f),
        new Vector3(1f, 1f, 1f),
        new Vector3(0f, 1f, 1f)
    };

    /// <summary>
    /// The triangles of the voxel faces (back, front, ...) (clockwise)
    /// </summary>
    public static readonly int[,] VoxelTris = new int[6, 4] {
        {0, 3, 1, 2}, // Back Face
        {5, 6, 4, 7}, // Front Face
        {3, 7, 2, 6}, // Top Face
        {1, 5, 0, 4}, // Bottom Face
        {4, 7, 0, 3}, // Left Face
        {1, 2, 5, 6}  // Right Face
    };

    /// <summary>
    /// The UVs of the voxel faces (back, front, ...) (clockwise)
    /// </summary>
    public static readonly Vector2[] VoxelUvs = new Vector2[4] {
        new Vector2(0f, 0f),
        new Vector2(0f, 1f),
        new Vector2(1f, 0f),
        new Vector2(1f, 1f)
    };

    /// <summary>
    /// The vectors to check for adjacent voxels (back, front, ...)
    /// </summary>
    public static readonly Vector3[] FaceChecks = new Vector3[6] {
        new Vector3(0f, 0f, -1f), // Back Face
        new Vector3(0f, 0f, 1f), // Front Face
        new Vector3(0f, 1f, 0f), // Top Face
        new Vector3(0f, -1f, 0f), // Bottom Face
        new Vector3(-1f, 0f, 0f), // Left Face
        new Vector3(1f, 0f, 0f)  // Right Face
    };
}
