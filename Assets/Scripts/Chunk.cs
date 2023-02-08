using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class Chunk
{
    #region Variables

    public GameObject chunkObject;

    /// <summary>
    /// The position of the chunk in the world
    /// </summary>
    public ChunkCoord coord;

    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;

    /// <summary>
    /// The index of the current vertex to keep track of the vertices
    /// </summary>
    int vertexIndex = 0;

    /// <summary>
    /// The vertices of the chunk mesh
    /// </summary>
    List<Vector3> vertices = new List<Vector3>();

    /// <summary>
    /// The triangles of the chunk mesh
    /// </summary>
    List<int> triangles = new List<int>();

    List<int> transparentTriangles = new List<int>();

    Material[] materials = new Material[2];

    /// <summary>
    /// The UVs of the chunk mesh
    /// </summary>
    List<Vector2> uvs = new List<Vector2>();

    public Vector3 Position;

    /// <summary>
    /// The voxel map of the chunk (block ids)
    /// </summary>
    public byte[,,] voxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];
    public Queue<VoxelMod> modifications = new Queue<VoxelMod>();

    World world;

    private bool _isActive;
    private bool IsVoxelMapPopulated = false;
    private bool threadLocked = false;

    #endregion

    #region Properties

    /// <summary>
    /// Checks if the chunk is active or not (or sets it)
    /// </summary>
    public bool IsActive{
        get { return _isActive; }
        set {
            _isActive = value;
            if (chunkObject != null)
            {
                chunkObject.SetActive(value);
            }
        }
    }

    public bool isEditable
    {
        get
        {
            if (!IsVoxelMapPopulated || threadLocked) return false;
            else return true;
        }
    }

    #endregion

    #region Constructor

    public Chunk(World world, ChunkCoord coord, bool generateOnLoad) { 
        this.world = world;
        this.coord = coord;
        IsActive = true;

        if (generateOnLoad)
        {
            Init();
        }
    }
    
    #endregion

    #region Methods

    public void Init()
    {
        chunkObject = new GameObject();
        // add components to the chunk object
        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();

        materials[0] = world.voxelMaterial;
        materials[1] = world.transparentVoxelMaterial;
        meshRenderer.materials = materials;

        // set parent
        chunkObject.transform.SetParent(world.transform);
        // set position
        chunkObject.transform.position = new Vector3(coord.x * VoxelData.ChunkWidth, 0, coord.z * VoxelData.ChunkWidth);
        // set name
        chunkObject.name = "Chunk" + coord.x + "-" + coord.z;
        Position = chunkObject.transform.position;

        // populate the voxel map in seperate thread
        Thread populateThread = new Thread(new ThreadStart(PopulateVoxelMap));
        populateThread.Start();
    }

    /// <summary>
    /// Populates the voxel map with boolean values wether a voxel is air or not
    /// </summary>
    void PopulateVoxelMap(){
        for(int y = 0; y < VoxelData.ChunkHeight; y++){
            for(int x = 0; x < VoxelData.ChunkWidth; x++){
                for(int z = 0; z < VoxelData.ChunkWidth; z++){
                    voxelMap[x, y, z] = world.GetVoxel(new Vector3(x, y, z) + Position,true);
                }
            }
        }

        _updateMesh();
        this.IsVoxelMapPopulated = true;
    }

    /// <summary>
    /// Updates the mesh of the chunk (in a thread)
    /// </summary>
    public void UpdateMesh()
    {
        Thread thread = new Thread(new ThreadStart(_updateMesh));
        thread.Start();
    }

    /// <summary>
    /// Generates the geometry of the chunk
    /// </summary>
    private void _updateMesh(){
        threadLocked = true;
        
        // process all modifications
        while (modifications.Count > 0)
        {
            VoxelMod v = modifications.Dequeue();
            Vector3 pos = v.position -= Position;
            // if voxel is not in world skip it
            if (!VoxelInWorld(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z))) continue;
            // set voxel in voxelmap
            voxelMap[(int)pos.x, (int)pos.y, (int)pos.z] = v.id;
        }

        ClearMeshData();
        
        for(int y = 0; y < VoxelData.ChunkHeight; y++){
            for(int x = 0; x < VoxelData.ChunkWidth; x++){
                for(int z = 0; z < VoxelData.ChunkWidth; z++){
                    if (world.blockTypes[voxelMap[x, y, z]].isSolid){
                        UpdateMeshData(new Vector3(x, y, z));
                    }
                }
            }
        }

        lock (world.chunksToDraw)
        {
            world.chunksToDraw.Enqueue(this);
        }

        threadLocked = false;
    }

    /// <summary>
    /// Edit voxel at the given vector3 to the given block id
    /// </summary>
    /// <param name="pos"> voxel position </param>
    /// <param name="newID"> block id to change the given voxel to </param>
    public void EditVoxel(Vector3 pos, byte newID)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

        voxelMap[xCheck, yCheck, zCheck] = newID;

        // update surrounding chunks
        UpdateSurroundingVoxels(xCheck, yCheck, zCheck);

        // update this chunks mesh
        _updateMesh();
    }

    /// <summary>
    /// update voxels around the given voxel
    /// </summary>
    /// <param name="x"> x position of the voxel </param>
    /// <param name="y"> y position of the voxel </param>
    /// <param name="z"> z position of the voxel </param>
    void UpdateSurroundingVoxels(int x, int y, int z)
    {
        Vector3 thisVoxel = new Vector3(x, y, z);

        for (int p = 0; p < 6; p++)
        {
            Vector3 currentVoxel = thisVoxel + VoxelData.FaceChecks[p];
            // check if voxel is in other chunk and if so update the other chunk
            Chunk c = world.GetChunkFromVector3(currentVoxel + Position);
            Debug.Log($"Chunk Update? {c.coord.x - 25} {c.coord.z - 25}, In Current Chunk: {VoxelInChunk((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z)}, {currentVoxel.x} {currentVoxel.y} {currentVoxel.z}");
            if (!VoxelInChunk((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z))
            {
                c.UpdateMesh();
                Debug.Log($"Updated Chunk {c.coord.x - 25} {c.coord.z - 25}");
            }
        }
    }

    /// <summary>
    /// check if voxel is in world
    /// </summary>
    /// <param name="x"> x position of the voxel </param>
    /// <param name="y"> y position of the voxel </param>
    /// <param name="z"> z position of the voxel </param>
    /// <returns> bool indicating whether the voxel is in the world or not </returns>
    bool VoxelInWorld(int x, int y, int z)
    {
        if (x < 0 || x > VoxelData.WorldSizeInVoxels - 1 || y < 0 || y > VoxelData.ChunkHeight - 1 || z < 0 || z > VoxelData.WorldSizeInVoxels - 1)   // if voxel outside of world boundaries
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    /// <summary>
    /// Checks if a given voxel position is inside the chunk
    /// </summary>
    /// <param name="x">The x position of the voxel</param>
    /// <param name="y">The y position of the voxel</param>
    /// <param name="z">The z position of the voxel</param>
    bool VoxelInChunk(int x, int y, int z){
        if(x < 0 || x > VoxelData.ChunkWidth - 1 || y < 0 || y > VoxelData.ChunkHeight - 1 || z < 0 || z > VoxelData.ChunkWidth - 1){
            return false;
        } else {
            return true;
        }
    }

    /// <summary>
    /// Checks if a voxel is air or not
    /// (if the voxel is outside the chunk, it will return false)
    /// </summary>
    /// <param name="pos">The position of the voxel (relative to the chunk)</param>
    /// <returns>True if the voxel is air, false if it is a solid block</returns>
    bool CheckVoxel(Vector3 pos){
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);
        
        // check if the voxel is outside the chunk
        if(!VoxelInChunk(x, y, z)){
            return world.CheckVoxelTransparency(pos + Position);
        }

        return world.blockTypes[voxelMap[x, y, z]].isTransparent;
    }

    /// <summary>
    /// Get voxel from global vector3
    /// </summary>
    /// <param name="pos">global coordinates of a voxel</param>
    /// <returns>the block id</returns>
    public byte GetVoxelFromGlobalVector3(Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(Position.x);
        zCheck -= Mathf.FloorToInt(Position.z);

        // check if voxel is in current chunk else return 0 (air)
        if (xCheck < 0 || xCheck > 15 || zCheck < 0 || zCheck > 15) return 0;

        try
        {
            return voxelMap[xCheck, yCheck, zCheck];
        }
        catch
        {
            Debug.Log(xCheck + " " + yCheck + " " + zCheck);
            return 0;
        }
    }

    /// <summary>
    /// Adds geometry of a voxel to the mesh at the given position
    /// </summary>
    /// <param name="pos">The position of the voxel (relative to the chunk)</param>
    void UpdateMeshData(Vector3 pos){
        byte blockId = voxelMap[(int)(pos.x), (int)(pos.y), (int)(pos.z)];
        bool isTransparent = world.blockTypes[blockId].isTransparent;
        // build the triangles
        for (int p = 0; p < 6; p++){
            // check if the face should be drawn (if there is a face next to it it, is not visible to the player and should not be drawn)
            if (CheckVoxel(pos + VoxelData.FaceChecks[p])){
                // only four vertices per face so the edges don't share the same vertices shared vertices would cause
                // the edges of the mesh to be a little bit rounded instead of sharp
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 0]]);
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 1]]);
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 2]]);
                vertices.Add(pos + VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 3]]);
                // add uvs 
                AddTexture(world.blockTypes[blockId].GetTextureId(p));

                // add triangles
                if (isTransparent)
                {
                    transparentTriangles.Add(vertexIndex);
                    transparentTriangles.Add(vertexIndex + 1);
                    transparentTriangles.Add(vertexIndex + 2);
                    transparentTriangles.Add(vertexIndex + 2);
                    transparentTriangles.Add(vertexIndex + 1);
                    transparentTriangles.Add(vertexIndex + 3);
                }
                else
                {
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 3);
                }

                // +4 because we only have 4 vertices per face
                vertexIndex += 4;
            }
        }
    }

    /// <summary>
    /// Applies the vertices, triangles and uvs to the mesh
    /// </summary>
    public void CreateMesh(){
        // build the mesh
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.subMeshCount = 2;
        // transparent and solid triangles
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetTriangles(transparentTriangles.ToArray(), 1);
        mesh.uv = uvs.ToArray();

        // recalculate normals
        mesh.RecalculateNormals();

        // assign the mesh to the mesh filter
        meshFilter.mesh = mesh;
    }

    /// <summary>
    /// Clear the meshes vertices, traingles and uvs
    /// </summary>
    void ClearMeshData()
    {
        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        transparentTriangles.Clear();
        uvs.Clear();
    }

    /// <summary>
    /// Adds a texture to a specific face of a voxel
    /// </summary>
    /// <param name="textureId">The id of the texture in the texture atlas</param>
    void AddTexture(int textureId){
        // for a 4x4 texture atlas, the texture id 0 would be at (0, 0) and the texture id 15 would be at (3, 3)
        float y = textureId / VoxelData.TextureAtlasSize;
        float x = textureId - (y * VoxelData.TextureAtlasSize);

        // normalize the texture coordinates
        x *= VoxelData.NormalizedBlockTextureSize;
        y *= VoxelData.NormalizedBlockTextureSize;

        // reverse the y coordinate because the texture coordinates start at the bottom left corner
        y = 1 - y - VoxelData.NormalizedBlockTextureSize;

        // add the texture coordinates to the UVs
        // same order as in VoxelData.VoxelUVs
        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x, y + VoxelData.NormalizedBlockTextureSize));
        uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y));
        uvs.Add(new Vector2(x + VoxelData.NormalizedBlockTextureSize, y + VoxelData.NormalizedBlockTextureSize));
    }

    #endregion
}

public class ChunkCoord{
    public int x;
    public int z;

    public ChunkCoord()
    {
        this.x = 0;
        this.z = 0;
    }

    public ChunkCoord(int x, int z){
        this.x = x;
        this.z = z;
    }

    public ChunkCoord(Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int zCheck = Mathf.FloorToInt(pos.z);

        this.x = xCheck / VoxelData.ChunkWidth;
        this.z = zCheck / VoxelData.ChunkWidth;
    }

    /// <summary>
    /// Checks if two ChunkCoord objects are equal (in terms of x and z)
    /// </summary>
    public bool Equals(ChunkCoord other){
        if(other == null){
            return false;
        }

        if(other.x == x && other.z == z){
            return true;
        } else {
            return false;
        }
    }
}