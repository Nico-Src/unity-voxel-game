using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DebugScreen : MonoBehaviour
{
    /// <summary>
    /// The debug screen game object
    /// </summary>
    public GameObject DebugInfo;
    
    // Text meshes of all the categories
    TextMeshProUGUI PerformanceInfo;
    TextMeshProUGUI PlayerInfo;
    TextMeshProUGUI ChunkInfo;
    
    // References to the world and the player
    World World;
    Player Player;

    // framerate counter
    float frameRate;
    float timer;

    int halfWorldSizeInVoxels;
    int halfWorldSizeInChunks;

    private void Start()
    {
        World = GameObject.Find("World").GetComponent<World>();
        Player = GameObject.Find("PlayerObject").GetComponent<Player>();
        PerformanceInfo = GameObject.Find("PerformanceInfoText").GetComponentInChildren<TextMeshProUGUI>();
        PlayerInfo = GameObject.Find("PlayerInfoText").GetComponentInChildren<TextMeshProUGUI>();
        ChunkInfo = GameObject.Find("ChunkInfoText").GetComponentInChildren<TextMeshProUGUI>();

        // calculate some metrics
        halfWorldSizeInChunks = VoxelData.WorldSizeInChunks / 2;
        halfWorldSizeInVoxels = VoxelData.WorldSizeInVoxels / 2;
    }
    
    private void Update()
    {
        // update info
        PerformanceInfo.text = $"FPS: {frameRate}";
        PlayerInfo.text = $"X: {Mathf.FloorToInt(World.player.transform.position.x) - halfWorldSizeInVoxels}, Y: {Mathf.FloorToInt(World.player.transform.position.y)}, Z: {Mathf.FloorToInt(World.player.transform.position.z) - halfWorldSizeInVoxels}";
        PlayerInfo.text += $"\nSpawn - X: {Mathf.FloorToInt(World.spawnPosition.x) - halfWorldSizeInVoxels}, Y: {Mathf.FloorToInt(World.spawnPosition.y)}, Z: {Mathf.FloorToInt(World.spawnPosition.z) - halfWorldSizeInVoxels}";
        PlayerInfo.text += $"\nLooking At: {Player.HighlightVoxel.position.x} {Player.HighlightVoxel.position.y} {Player.HighlightVoxel.position.z}";
        ChunkInfo.text = $"Chunk - X: {World.playerChunkCoord.x - halfWorldSizeInChunks}, Y: {World.playerChunkCoord.z - halfWorldSizeInChunks}";

        // update framerate only every half second
        if (timer > .5f)
        {
            frameRate = (int)(1f / Time.unscaledDeltaTime);
            timer = 0;
        }
        else
        {
            timer += Time.deltaTime;
        }
    }
}
