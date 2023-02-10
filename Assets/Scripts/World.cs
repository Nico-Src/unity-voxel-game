using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public enum Weather
    {
        Clear,
        Rain,
        Snow
    }

    public Weather CurrentWeather = Weather.Clear;

    public GameObject RainSystem;
    public GameObject SnowSystem;
    
    public int seed;
    public BiomeAttributes biome;

    /// <summary>
    /// The player transform
    /// </summary>
    public Transform player;

    /// <summary>
    /// The position of the spawn
    /// </summary>
    public Vector3 spawnPosition;

    /// <summary>
    /// The material used to texture the voxels
    /// </summary>
    public Material voxelMaterial;

    /// <summary>
    /// The material used to texture the transparent voxels
    /// </summary>
    public Material transparentVoxelMaterial;

    /// <summary>
    /// the debug screen game object
    /// </summary>
    public GameObject DebugScreen;

    /// <summary>
    /// Array of all blocktypes
    /// </summary>
    public BlockType[] blockTypes;

    /// <summary>
    /// Chunk array
    /// </summary>
    Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

    /// <summary>
    /// List of currently active chunks
    /// </summary>
    List<ChunkCoord> activeChunks = new List<ChunkCoord>();

    /// <summary>
    /// The last chunk the player was in
    /// </summary>
    ChunkCoord playerLastChunkCoord;

    /// <summary>
    /// The current chunk the player is in
    /// </summary>
    public ChunkCoord playerChunkCoord;

    /// <summary>
    /// List of chunks that are waiting to be created
    /// </summary>
    List<ChunkCoord> ChunksToCreate = new List<ChunkCoord>();

    /// <summary>
    /// List of chunks that are waiting to be drawn
    /// </summary>
    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();

    /// <summary>
    /// bool indicating if we are currently applying modifications
    /// </summary>
    bool applyingModifications = false;

    /// <summary>
    /// Queue of modifications to apply
    /// </summary>
    public Queue<Queue<VoxelMod>> modifications = new Queue<Queue<VoxelMod>>();

    /// <summary>
    /// List of chunks that are waiting to be updated
    /// </summary>
    public List<Chunk> chunksToUpdate = new List<Chunk>();

    public void Start(){
        // same seed for the same world
        Random.InitState(seed);
        GenerateWorld();
    }

    public void Update(){
        // check if the player has moved to a new chunk
        playerChunkCoord = GetChunkCoordFromVector3(player.position);
        // if the player has moved to a new chunk update the world
        if (!playerLastChunkCoord.Equals(playerChunkCoord))
        {
            CheckViewDistance();
        }

        // apply modifictions if there are any and if we are not already applying them
        if (!applyingModifications)
        {
            ApplyModifications();
        }

        // if there are chunks to create, create the next one in the list
        if(ChunksToCreate.Count > 0)
        {
            CreateChunk();
        }

        // if there are chunks to update, update the next one in the list
        if(chunksToUpdate.Count > 0)
        {
            UpdateChunks();
        }

        // if there are chunks to draw, draw the next one in the list
        if (chunksToDraw.Count > 0)
        {
            // lock the list so that it can't be modified while we are drawing
            lock (chunksToDraw)
            {
                // check if the chunk is editable (if it is, it is ready to be drawn)
                if (chunksToDraw.Peek().isEditable)
                {
                    // create mesh
                    chunksToDraw.Dequeue().CreateMesh();
                }
            }
        }

        // toggle debug screen on f3 key press
        if (Input.GetKeyDown(KeyCode.F3))
        {
            DebugScreen.SetActive(!DebugScreen.activeSelf);
        }
    }

    /// <summary>
    /// Generates the world
    /// </summary>
    void GenerateWorld(){
        int centerX = VoxelData.WorldSizeInChunks / 2;
        int centerZ = VoxelData.WorldSizeInChunks / 2;

        for(int x = centerX - VoxelData.ViewDistanceInChunks; x < centerX + VoxelData.ViewDistanceInChunks; x++){
            for(int z = centerZ - VoxelData.ViewDistanceInChunks; z < centerZ + VoxelData.ViewDistanceInChunks; z++){
                chunks[x, z] = new Chunk(this, new ChunkCoord(x,z),true);
                activeChunks.Add(new ChunkCoord(x, z));
            }
        }

        // get spawn position
        for(int i = 0; i < VoxelData.ChunkHeight; i+=4)
        {
            if(!blockTypes[chunks[centerX, centerZ].voxelMap[7, i, 7]].isSolid)
            {
                spawnPosition = new Vector3((VoxelData.WorldSizeInChunks / 2) * VoxelData.ChunkWidth, (float)i + 50f, (VoxelData.WorldSizeInChunks / 2) * VoxelData.ChunkWidth);
                break;
            }
        }
        
        player.position = spawnPosition;
        // update last chunk coord
        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);

        while(chunksToUpdate.Count > 0)
        {
            chunksToUpdate[0].UpdateMesh();
            chunksToUpdate.RemoveAt(0);
        }

        // rename the game object
        // int totalVoxels = VoxelData.WorldSizeInVoxels * VoxelData.WorldSizeInVoxels * VoxelData.ChunkHeight;
        // gameObject.name = "World (" + totalVoxels + " Voxels)";
    }

    /// <summary>
    /// Create chunk
    /// </summary>
    void CreateChunk()
    {
        // get first chunk in chunkstocreate list
        ChunkCoord c = ChunksToCreate[0];
        ChunksToCreate.RemoveAt(0);
        activeChunks.Add(c);
        // init the chunk
        chunks[c.x, c.z].Init();
    }

    /// <summary>
    /// Update chunks
    /// </summary>
    void UpdateChunks()
    {
        bool updated = false;
        int index = 0;

        // update all chunks that are in the list
        while (!updated && index < chunksToUpdate.Count - 1)
        {
            // only update the chunk if it is editable (leave it for now if it isn't and resume later)
            if (chunksToUpdate[index].isEditable)
            {
                // update mesh
                chunksToUpdate[index].UpdateMesh();
                // remove it from list
                chunksToUpdate.RemoveAt(index);
                updated = true;
            } else
            {
                index++;
            }
        }
    }

    /// <summary>
    /// Apply all modifications that are currently in the queue
    /// </summary>
    void ApplyModifications()
    {
        // set bool so that this method isn't called twice at the same time so they don't interfere
        applyingModifications = true;

        // run this while loop till all modifications are processed
        while(modifications.Count > 0)
        {
            Queue<VoxelMod> queue = modifications.Dequeue();
            try
            {
                while (queue.Count > 0)
                {
                    VoxelMod v = queue.Dequeue();
                    // get responsible chunk (in which chunk the mod is applied)
                    ChunkCoord c = GetChunkCoordFromVector3(v.position);

                    // check if chunk exists
                    if (chunks[c.x, c.z] == null)
                    {
                        chunks[c.x, c.z] = new Chunk(this, c, true);
                        activeChunks.Add(c);
                    }

                    // pass modification to queue of responsible chunk
                    chunks[c.x, c.z].modifications.Enqueue(v);

                    if (!chunksToUpdate.Contains(chunks[c.x, c.z]))
                    {
                        chunksToUpdate.Add(chunks[c.x, c.z]);
                    }
                }
            }
            catch
            {
                
            }
        }

        applyingModifications = false;
    }

    /// <summary>
    /// Returns the chunk coordinate from a vector3 position
    /// </summary>
    /// <param name="pos"> the position to get the chunk coordinates from </param>
    /// <returns> the coordinates of the chunk at the given position </returns>
    ChunkCoord GetChunkCoordFromVector3(Vector3 pos){
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);

        return new ChunkCoord(x, z);
    }

    /// <summary>
    /// Returns the chunk at the given vector3
    /// </summary>
    /// <param name="pos"> the position to get the chunk from </param>
    /// <returns> the chunk at the given position </returns>
    public Chunk GetChunkFromVector3(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);

        return chunks[x, z];
    }

    /// <summary>
    /// Manage the chunks that are visible to the player
    /// </summary>
    public void CheckViewDistance(){
        // get current chunk coord
        ChunkCoord coord = GetChunkCoordFromVector3(player.position);
        // update last chunk coord
        playerLastChunkCoord = playerChunkCoord;

        // chunks that were active before updating
        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(activeChunks);

        for(int x = coord.x - VoxelData.ViewDistanceInChunks; x < coord.x + VoxelData.ViewDistanceInChunks; x++){
            for(int z = coord.z - VoxelData.ViewDistanceInChunks; z < coord.z + VoxelData.ViewDistanceInChunks; z++){
                // check if chunk is in world
                if(ChunkInWorld(new ChunkCoord(x, z))){
                    // if chunk has not been created yet create it (don't initialize immediatly) and add it to the list of chunks to create
                    if(chunks[x, z] == null){
                        chunks[x, z] = new Chunk(this, new ChunkCoord(x, z), false);
                        ChunksToCreate.Add(new ChunkCoord(x, z));
                    // else just enable the chunk
                    } else if(!chunks[x, z].IsActive){
                        chunks[x, z].IsActive = true;
                    }
                    // add the chunk to active chunks
                    activeChunks.Add(new ChunkCoord(x, z));
                }

                // check which chunks were active last chunk that aren't anymore
                List<ChunkCoord> chunksToRemoveTmp = new List<ChunkCoord>();
                for (int i = 0; i < previouslyActiveChunks.Count; i++)
                {
                    if (previouslyActiveChunks[i].Equals(new ChunkCoord(x, z)))
                    {
                        chunksToRemoveTmp.Add(previouslyActiveChunks[i]);
                    }
                }
                
                // remove all chunks that aren't active anymore
                while(chunksToRemoveTmp.Count > 0)
                {
                    previouslyActiveChunks.Remove(chunksToRemoveTmp[0]);
                    chunksToRemoveTmp.RemoveAt(0);
                }
            }
        }

        foreach(ChunkCoord c in previouslyActiveChunks){
            chunks[c.x, c.z].IsActive = false;
            activeChunks.Remove(c);
        }
    }

    /// <summary>
    /// Gets the voxel type at the specified position
    /// </summary>
    /// <param name="pos"> the position to get the voxel from </param>
    /// <param name="generate"> wether to generate any new trees </param>
    public byte GetVoxel(Vector3 pos, bool generate){
        int yPos = Mathf.FloorToInt(pos.y);

        // every voxel outside the world is air
        if(!VoxelInWorld(pos)){
            return 0;
        }

        // bottom layer is bedrock (ID 1)
        if(yPos == 0){
            return 1;
        }

        // Basic Terrain

        int terrainHeight = Mathf.FloorToInt(biome.terrainHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 500f, biome.terrainScale)) + biome.solidGroundHeight;
        byte voxelValue = 0;

        if(yPos == terrainHeight){
            voxelValue = 4; // grass
        } else if(yPos < terrainHeight && yPos > terrainHeight - 4){
            voxelValue = 3; // dirt
        } else if (yPos > terrainHeight) {
            return 0; // air
        } else {
            voxelValue = 2; // stone
        }

        // Second Pass (Ores, Other Blocks...)

        if(voxelValue == 2){
            foreach(Lode lode in biome.lodes){
                if(yPos > lode.minHeight && yPos < lode.maxHeight){
                    if(Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold)){
                        voxelValue = lode.blockID;
                    }
                }
            }
        }

        // Third Pass (Trees)

        if(yPos == terrainHeight)
        {
            if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treeZoneScale) > biome.treeZoneThreshold)
            {
                if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > biome.treePlacementThreshold)
                {
                    if(generate) modifications.Enqueue(Structure.MakeTree(pos, biome.minTreeHeight, biome.maxTreeHeight));
                }
            }
        }

        return voxelValue;
    }

    /// <summary>
    /// Check if a chunk is in the world
    /// </summary>
    /// <param name="coord">The coordinate of the chunk</param>
    public bool ChunkInWorld(ChunkCoord coord){
        if(coord.x > 0 && coord.x < VoxelData.WorldSizeInChunks - 1 && coord.z > 0 && coord.z < VoxelData.WorldSizeInChunks - 1){
            return true;
        }else{
            return false;
        }
    }

    /// <summary>
    /// Check if a voxel is in the world
    public bool VoxelInWorld(Vector3 pos){
        if(pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels && pos.y >= 0 && pos.y < VoxelData.ChunkHeight && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels){
            return true;
        }else{
            return false;
        }
    }

    /// <summary>
    /// Check if a voxel is solid (globally)
    /// </summary>
    public bool CheckForVoxel(Vector3 pos){
        // floor the coordinates
        ChunkCoord thisChunk = new ChunkCoord(pos);

        // if voxel is outside of world return false
        if (!ChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight - 1) return false;

        // if the chunk is set and editable check if the voxel is solid
        if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].isEditable)
        {
            return blockTypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].isSolid;
        }

        return blockTypes[GetVoxel(pos,false)].isSolid;
    }

    /// <summary>
    /// check if the voxel at the given position is transparent or not
    /// </summary>
    /// <param name="pos"> the position where to check at </param>
    /// <returns> bool indicating whether voxel is transparent or not </returns>
    public bool CheckVoxelTransparency(Vector3 pos)
    {
        // floor the coordinates
        ChunkCoord thisChunk = new ChunkCoord(pos);

        // if voxel is outside of world return false
        if (!ChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight - 1) return false;

        // if the chunk is set and editable check if the voxel is solid
        if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].isEditable)
        {
            return blockTypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].isTransparent;
        }

        return blockTypes[GetVoxel(pos,false)].isTransparent;
    }
}

[System.Serializable]
public class BlockType{
    /// <summary>
    /// The name of the block
    /// </summary>
    public string Name;

    /// <summary>
    /// Bool that determines if the block is solid
    /// </summary>
    public bool isSolid;

    public bool isTransparent;
    public Sprite icon;

    [Header("Face Textures")]
    [Tooltip("The texture of the block on the back face")]
    public int BackFaceTextureId;
    [Tooltip("The texture of the block on the front face")]
    public int FrontFaceTextureId;
    [Tooltip("The texture of the block on the top face")]
    public int TopFaceTextureId;
    [Tooltip("The texture of the block on the bottom face")]
    public int BottomFaceTextureId;
    [Tooltip("The texture of the block on the left face")]
    public int LeftFaceTextureId;
    [Tooltip("The texture of the block on the right face")]
    public int RightFaceTextureId;

    /// <summary>
    /// The texture of the block on a specific face
    /// </summary>
    /// <param name="faceIndex">The index of the face</param>
    public int GetTextureId(int faceIndex){
        switch(faceIndex){
            case 0:
                return BackFaceTextureId;
            case 1:
                return FrontFaceTextureId;
            case 2:
                return TopFaceTextureId;
            case 3:
                return BottomFaceTextureId;
            case 4:
                return LeftFaceTextureId;
            case 5:
                return RightFaceTextureId;
            default:
                Debug.Log("Invalid face index: " + faceIndex);
                return 0;
        }
    }
}

public class VoxelMod
{
    public Vector3 position;
    public byte id;

    public VoxelMod()
    {
        this.position = new Vector3();
        this.id = 0;
    }

    public VoxelMod(Vector3 pos, byte id)
    {
        this.position = pos;
        this.id = id;
    }
}