using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Biome", menuName = "VoxelEngine/Biome")]
public class BiomeAttributes : ScriptableObject
{
    public string biomeName;

    /// <summary>
    /// Below this height the voxels are solid
    /// </summary>
    public int solidGroundHeight;

    /// <summary>
    /// The height of the terrain (from the solid ground height)
    /// </summary>
    public int terrainHeight;
    public float terrainScale;

    [Header("Trees")]
    public float treeZoneScale = 1.3f;
    [Range(0.1f,1.0f)]
    public float treeZoneThreshold = 0.6f;
    public float treePlacementScale = 15f;
    [Range(0.1f, 1.0f)]
    public float treePlacementThreshold = 0.8f;

    public int maxTreeHeight = 12;
    public int minTreeHeight = 4;

    public Lode[] lodes;
}

[System.Serializable]
public class Lode{
    public string nodeName;
    public byte blockID;
    public int minHeight;
    public int maxHeight;
    public float scale;
    public float threshold;
    public float noiseOffset;
}