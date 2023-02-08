using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ShatterToolkit;

public class Player : MonoBehaviour
{
    public enum Gamemode
    {
        Survival, 
        Creative,
        Builder
    }

    public Gamemode Mode = Gamemode.Creative;
    
    public bool isGrounded;
    public bool isFlying;
    public bool isSprinting;
    public bool isZooming;
    public bool isPlaceVoxelVisible;
    public bool commandLineVisible;

    private Transform cam;
    private Camera camObject;
    private World world;

    public float WalkSpeed = 3f;
    public float SprintSpeed = 6f;
    public float FlyingSpeed = 6f;
    public float FlyingSprintSpeed = 18f;
    public float JumpForce = 5f;
    public float Gravity = -9.81f;

    public float playerWidth = 0.4f;
    public float playerHeight = 1.8f;

    public float zoomTransitionDuration = 0.5f;

    private float horizontal;
    private float vertical;

    private float mouseHorizontal;
    private float mouseVertical;

    private Vector3 velocity;

    private float verticalMomentum = 0;
    private bool jumpRequest;
    private bool jumpPressedFirst;
    private float lastJumpRequest;
    private bool resetJump;

    public Transform HighlightVoxel;
    public Transform PlaceVoxel;
    public MeshFilter PlaceVoxelMesh;
    byte PlaceVoxelId = 0;

    public byte BuilderAreaP1Block = 0;
    public byte BuilderAreaP2Block = 0;
    public Vector3 BuilderAreaP1 = new Vector3(float.NaN, float.NaN, float.NaN);
    public Vector3 BuilderAreaP2 = new Vector3(float.NaN, float.NaN, float.NaN);
    public GameObject AreaCube;
    public GameObject AreaCubeMesh;
    public GameObject BlockBreakPrefab;
    public GameObject BlockBreakGroundPrefab;
    
    /// <summary>
    /// Steps for the raycast to get the currently looked block
    /// </summary>
    public float checkIncrement = 0.1f;

    /// <summary>
    /// The distance the player can reach (Maximum ray legnth)
    /// </summary>
    public float reach = 8f;

    public byte SelectedBlockIndex = 1;

    public GameObject CommandLine;
    public CommandLine CommandLineInterface;
    private Image CommandLinePanel;

    private void Start(){
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        // get references
        cam = GameObject.Find("Main Camera").transform;
        camObject = cam.gameObject.GetComponent<Camera>();
        world = GameObject.Find("World").GetComponent<World>();
        CommandLineInterface = CommandLine.GetComponent<CommandLine>();
        CommandLinePanel = CommandLine.GetComponent<Image>();
    }


    private void FixedUpdate()
    {
        CalculateVelocity();
        if (jumpRequest && !this.isFlying) Jump();
        else if (this.isFlying && !this.commandLineVisible)
        {
            if (Input.GetKey(KeyCode.Space))
            {
                velocity.y = .15f;
            } else if (Input.GetMouseButton(2))
            {
                velocity.y = -.15f;
            }
        }

        transform.Translate(velocity, Space.World);
    }


    private void Update(){
        // get current inputs
        GetPlayerInputs();
        PlaceCursorBlocks();

        // rotation
        if (!commandLineVisible)
        {
            transform.Rotate(Vector3.up * mouseHorizontal);
            cam.Rotate(Vector3.right * -mouseVertical);
        }
        // TODO: clamp camera rotation

        if (this.isZooming)
        {
            if(this.camObject.fieldOfView > 20)
            {
                this.camObject.fieldOfView -= this.zoomTransitionDuration * 100 * Time.deltaTime;
            }
        }
        else
        {
            if(this.camObject.fieldOfView < 70)
            {
                this.camObject.fieldOfView += this.zoomTransitionDuration * 100 * Time.deltaTime;
            }
        }
    }

    private void CalculateVelocity()
    {
        // affect vertical momentum with gravity
        if(verticalMomentum > Gravity)
        {
            // exponential vertical momentum decay
            verticalMomentum += Time.fixedDeltaTime * Gravity;
        }

        // sprint multiplier
        if (isSprinting)
        {
            if (isFlying)
            {
                velocity = ((transform.forward * vertical) + (transform.right * horizontal)).normalized * Time.fixedDeltaTime * FlyingSprintSpeed;
            } else
            {
                velocity = ((transform.forward * vertical) + (transform.right * horizontal)).normalized * Time.fixedDeltaTime * SprintSpeed;
            }
        }
        else
        {
            if (isFlying)
            {
                velocity = ((transform.forward * vertical) + (transform.right * horizontal)).normalized * Time.fixedDeltaTime * FlyingSpeed;
            } else
            {
                velocity = ((transform.forward * vertical) + (transform.right * horizontal)).normalized * Time.fixedDeltaTime * WalkSpeed;
            }
        }

        // apply vertical momentum to player (falling / jumping)
        velocity += Vector3.up * verticalMomentum * Time.fixedDeltaTime;
        
        // check if player can move forward or backward
        if((velocity.z > 0 && front) || (velocity.z < 0 && back)){
            velocity.z = 0;
        }

        // check if player can move left or right
        if ((velocity.x > 0 && right) || (velocity.x < 0 && left))
        {
            velocity.x = 0;
        }

        // check if player can move up or down
        if(velocity.y < 0)
        {
            velocity.y = checkDownSpeed(velocity.y);
        } 
        else if(velocity.y > 0)
        {
            velocity.y = checkUpSpeed(velocity.y);
        }

        if (this.isFlying) velocity.y = 0;
    }

    void Jump()
    {
        verticalMomentum = JumpForce;
        isGrounded = false;
        jumpRequest = false;
    }

    private void GetPlayerInputs(){
        horizontal = Input.GetAxisRaw("Horizontal");
        vertical = Input.GetAxisRaw("Vertical");

        if (commandLineVisible)
        {
            horizontal = 0;
            vertical = 0;
        }

        mouseHorizontal = Input.GetAxis("Mouse X");
        mouseVertical = Input.GetAxis("Mouse Y");

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            this.isSprinting = true;
        }

        if (Input.GetKeyUp(KeyCode.LeftShift)){
            this.isSprinting = false;
        }

        if (Input.GetKeyDown(KeyCode.C) && !this.commandLineVisible)
        {
            this.isZooming = true;
        }

        if (Input.GetKeyUp(KeyCode.C) && !this.commandLineVisible)
        {
            this.isZooming = false;
        }

        if (Input.GetKeyDown(KeyCode.T) && !this.commandLineVisible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CommandLinePanel.color = new Color(0, 0, 0, 0.5f);
            CommandLineInterface.InputField.textComponent.alpha = 1f;
            CommandLineInterface.InputField.placeholder.GetComponent<TextMeshProUGUI>().alpha = .5f;
            CommandLineInterface.InputField.interactable = true;
            CommandLineInterface.InputField.Select();
            CommandLineInterface.InputField.ActivateInputField();
            this.commandLineVisible = true;
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            bool valid = CommandLineInterface.ExecuteCommand();
            if (valid)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                CommandLinePanel.color = new Color(0, 0, 0, 0.0f);
                CommandLineInterface.InputField.textComponent.alpha = 0.0f;
                CommandLineInterface.InputField.placeholder.GetComponent<TextMeshProUGUI>().alpha = 0f;
                CommandLineInterface.InputField.interactable = false;
                CommandLineInterface.InputField.DeactivateInputField(true);
                this.commandLineVisible = false;
            }
        }

        if (Input.GetKeyDown(KeyCode.P) && !this.commandLineVisible)
        {
            if (PlaceVoxel.gameObject.activeSelf)
            {
                PlaceVoxel.gameObject.SetActive(false);
                isPlaceVoxelVisible = false;
            } 
            else
            {
                PlaceVoxel.gameObject.SetActive(true);
                isPlaceVoxelVisible = true;
                ChangePlaceBlockTexture(world.blockTypes[SelectedBlockIndex]);
            }
        }

        if (this.isGrounded && Input.GetKeyDown(KeyCode.Space) && !this.commandLineVisible)
        {
            this.jumpRequest = true;
        }

        // only enable voxel placing or breaking if player looks at a voxel currently
        if (HighlightVoxel.gameObject.activeSelf && !commandLineVisible)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if(Mode == Gamemode.Builder)
                {
                    if(BuilderAreaP1 != HighlightVoxel.position)
                    {
                        if (!float.IsNaN(BuilderAreaP1.x)) world.GetChunkFromVector3(BuilderAreaP1).EditVoxel(BuilderAreaP1, BuilderAreaP1Block);

                        BuilderAreaP1 = HighlightVoxel.position;
                        Chunk chunk = world.GetChunkFromVector3(HighlightVoxel.position);
                        BuilderAreaP1Block = chunk.GetVoxelFromGlobalVector3(BuilderAreaP1);
                        chunk.EditVoxel(HighlightVoxel.position, 1);
                        UpdateBuilderArea();
                    }
                    else
                    {
                        BuilderAreaP1 = new Vector3(float.NaN, float.NaN, float.NaN);
                        UpdateBuilderArea();
                        world.GetChunkFromVector3(HighlightVoxel.position).EditVoxel(HighlightVoxel.position, BuilderAreaP1Block);
                    }
                }
                else {
                    byte blockId = world.GetChunkFromVector3(HighlightVoxel.position).GetVoxelFromGlobalVector3(HighlightVoxel.position);
                    world.GetChunkFromVector3(HighlightVoxel.position).EditVoxel(HighlightVoxel.position, 0);

                    // create cube
                    var cube = Instantiate(BlockBreakPrefab);
                    if (world.blockTypes[blockId].isTransparent) cube.GetComponent<Renderer>().material = world.transparentVoxelMaterial;
                    ApplyBreakBlockTexture(world.blockTypes[blockId], cube);
                    cube.transform.position = new Vector3(HighlightVoxel.position.x, HighlightVoxel.position.y, HighlightVoxel.position.z);
                    cube.transform.localScale = new Vector3(1f, 1f, 1f);
                }
            } 
            
            if(Input.GetMouseButtonDown(1)) {
                if(Mode == Gamemode.Builder)
                {
                    if(BuilderAreaP2 != HighlightVoxel.position)
                    {
                        if(!float.IsNaN(BuilderAreaP2.x)) world.GetChunkFromVector3(BuilderAreaP2).EditVoxel(BuilderAreaP2, BuilderAreaP2Block);

                        BuilderAreaP2 = HighlightVoxel.position;
                        Chunk chunk = world.GetChunkFromVector3(HighlightVoxel.position);
                        BuilderAreaP2Block = chunk.GetVoxelFromGlobalVector3(BuilderAreaP2);
                        chunk.EditVoxel(HighlightVoxel.position, 2);
                        UpdateBuilderArea();
                    } else
                    {
                        BuilderAreaP2 = new Vector3(float.NaN, float.NaN, float.NaN);
                        UpdateBuilderArea();
                        world.GetChunkFromVector3(HighlightVoxel.position).EditVoxel(HighlightVoxel.position, BuilderAreaP2Block);
                    }
                }
                else
                {
                    int xCheck = Mathf.FloorToInt(this.transform.position.x);
                    // check the block at the players feet, but also the block above this one because the player is 2 voxels tall
                    int yCheck = Mathf.FloorToInt(this.transform.position.y);
                    int yCheckAbove = Mathf.FloorToInt(this.transform.position.y + 1);
                    int zCheck = Mathf.FloorToInt(this.transform.position.z);

                    int voxelX = Mathf.FloorToInt(PlaceVoxel.position.x);
                    int voxelY = Mathf.FloorToInt(PlaceVoxel.position.y);
                    int voxelZ = Mathf.FloorToInt(PlaceVoxel.position.z);


                    // check if the player is trying to place a block in his own position
                    if (xCheck == voxelX && yCheck == voxelY && zCheck == voxelZ
                     || xCheck == voxelX && yCheckAbove == voxelY && zCheck == voxelZ)
                    {
                        return;
                    }

                    // else place the block
                    world.GetChunkFromVector3(PlaceVoxel.position).EditVoxel(PlaceVoxel.position, SelectedBlockIndex);
                }
            }
        }
    }

    private void UpdateBuilderArea()
    {
        if (float.IsNaN(BuilderAreaP1.x) || float.IsNaN(BuilderAreaP2.x))
        {
            AreaCube.SetActive(false);
            AreaCube.transform.position = new Vector3(0, 0, 0);
            AreaCube.transform.localScale = new Vector3(0, 0, 0);
            return;
        }

        AreaCube.SetActive(true);

        float minX = Mathf.Min(BuilderAreaP1.x, BuilderAreaP2.x);
        float maxX = Mathf.Max(BuilderAreaP1.x, BuilderAreaP2.x);
        float minY = Mathf.Min(BuilderAreaP1.y, BuilderAreaP2.y);
        float maxY = Mathf.Max(BuilderAreaP1.y, BuilderAreaP2.y);
        float minZ = Mathf.Min(BuilderAreaP1.z, BuilderAreaP2.z);
        float maxZ = Mathf.Max(BuilderAreaP1.z, BuilderAreaP2.z);

        int posX = Mathf.FloorToInt((minX + maxX) / 2);
        int posY = Mathf.FloorToInt((minY + maxY)) / 2;
        int posZ = Mathf.FloorToInt((minZ + maxZ) / 2);

        int xDist = Mathf.FloorToInt(Mathf.Abs(BuilderAreaP2.x - BuilderAreaP1.x)) / 2;
        int yDist = Mathf.FloorToInt(Mathf.Abs(BuilderAreaP2.y - BuilderAreaP1.y)) / 2;
        int zDist = Mathf.FloorToInt(Mathf.Abs(BuilderAreaP2.z - BuilderAreaP1.z)) / 2;

        if (BuilderAreaP2.x < BuilderAreaP1.x && Mathf.Abs(BuilderAreaP2.x - BuilderAreaP1.x) > 1) posX -= xDist;
        else if (BuilderAreaP2.x > BuilderAreaP1.x && Mathf.Abs(BuilderAreaP2.x - BuilderAreaP1.x) > 1) posX -= xDist;

        if (BuilderAreaP2.y < BuilderAreaP1.y && Mathf.Abs(BuilderAreaP2.y - BuilderAreaP1.y) > 1) posY -= yDist;
        else if (BuilderAreaP2.y > BuilderAreaP1.y && Mathf.Abs(BuilderAreaP2.y - BuilderAreaP1.y) > 1) posY -= yDist;

        if (BuilderAreaP2.z < BuilderAreaP1.z && Mathf.Abs(BuilderAreaP2.z - BuilderAreaP1.z) > 1) posZ -= zDist;
        else if (BuilderAreaP2.z > BuilderAreaP1.z && Mathf.Abs(BuilderAreaP2.z - BuilderAreaP1.z) > 1) posZ -= zDist;

        Vector3 midpoint = new Vector3(posX, posY, posZ);
        float sizeX = maxX - minX;
        float sizeY = maxY - minY;
        float sizeZ = maxZ - minZ;

        // fix overlapping texture by increaasing scale by something very small
        sizeX += 0.001f;
        sizeY += 0.001f;
        sizeZ += 0.001f;

        AreaCube.transform.position = midpoint;
        AreaCube.transform.localScale = new Vector3(sizeX + 1,sizeY + 1,sizeZ + 1);
        if (AreaCube.transform.localScale.y < 1) AreaCube.transform.localScale = new Vector3(AreaCube.transform.localScale.x, 1, AreaCube.transform.localScale.z);
    }

    public void FillBuildArea(byte blockId)
    {
        if (float.IsNaN(BuilderAreaP1.x) || float.IsNaN(BuilderAreaP2.x))
        {
            return;
        }

        float minX = Mathf.Min(BuilderAreaP1.x, BuilderAreaP2.x);
        float maxX = Mathf.Max(BuilderAreaP1.x, BuilderAreaP2.x);
        float minY = Mathf.Min(BuilderAreaP1.y, BuilderAreaP2.y);
        float maxY = Mathf.Max(BuilderAreaP1.y, BuilderAreaP2.y);
        float minZ = Mathf.Min(BuilderAreaP1.z, BuilderAreaP2.z);
        float maxZ = Mathf.Max(BuilderAreaP1.z, BuilderAreaP2.z);

        Queue<VoxelMod> mods = new Queue<VoxelMod>();

        for (float x = minX; x <= maxX; x++)
        {
            for (float y = minY; y <= maxY; y++)
            {
                for (float z = minZ; z <= maxZ; z++)
                {
                    Vector3 voxelPos = new Vector3(x, y, z);
                    mods.Enqueue(new VoxelMod(voxelPos,blockId));
                }
            }
        }

        world.modifications.Enqueue(mods);

        ClearBuildArea(false);

        world.CheckViewDistance();
    }

    public void ClearBuildArea(bool revertBlocks = true)
    {
        if(revertBlocks && !float.IsNaN(BuilderAreaP1.x)) world.GetChunkFromVector3(BuilderAreaP1).EditVoxel(BuilderAreaP1, BuilderAreaP1Block);
        BuilderAreaP1 = new Vector3(float.NaN, float.NaN, float.NaN);

        if (revertBlocks && !float.IsNaN(BuilderAreaP2.x)) world.GetChunkFromVector3(BuilderAreaP2).EditVoxel(BuilderAreaP2, BuilderAreaP2Block);
        BuilderAreaP2 = new Vector3(float.NaN, float.NaN, float.NaN);

        UpdateBuilderArea();
    }

    /// <summary>
    /// Shoot "fake" ray to check which block the player is currently looking at
    /// </summary>
    private void PlaceCursorBlocks()
    {
        float step = checkIncrement;
        Vector3 lastPos = new Vector3();

        while(step < reach)
        {
            // move forward with fake ray
            Vector3 pos = cam.position + (cam.forward * step);

            // check if there is a voxel
            if (world.CheckForVoxel(pos))
            {
                // position highlight cubes
                HighlightVoxel.position = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
                PlaceVoxel.position = lastPos;

                // activate them
                HighlightVoxel.gameObject.SetActive(true);
                if(isPlaceVoxelVisible) PlaceVoxel.gameObject.SetActive(true);

                return;
            }

            // set last pos and move on
            lastPos = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
            step += checkIncrement;
        }

        HighlightVoxel.gameObject.SetActive(false);
        PlaceVoxel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Replaces the texture of the voxel preview with the texture of the selected voxel
    /// </summary>
    /// <param name="type"></param>
    public void ChangePlaceBlockTexture(BlockType type)
    {
        int vertexIndex = 0;
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int p = 0; p < 6; p++){
            int textureId = type.GetTextureId(p);

            vertices.Add(VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 0]]);
            vertices.Add(VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 1]]);
            vertices.Add(VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 2]]);
            vertices.Add(VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 3]]);

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

            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 3);

            vertexIndex += 4;
        }

        Mesh mesh = new Mesh();

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        PlaceVoxelMesh.mesh = mesh;
    }

    /// <summary>
    /// Replaces the texture of the voxel preview with the texture of the selected voxel
    /// </summary>
    /// <param name="type"></param>
    public void ApplyBreakBlockTexture(BlockType type,GameObject breakBlock)
    {
        int vertexIndex = 0;
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int p = 0; p < 6; p++)
        {
            int textureId = type.GetTextureId(p);

            vertices.Add(VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 0]]);
            vertices.Add(VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 1]]);
            vertices.Add(VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 2]]);
            vertices.Add(VoxelData.VoxelVerts[VoxelData.VoxelTris[p, 3]]);

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

            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 3);

            vertexIndex += 4;
        }

        Mesh mesh = new Mesh();

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        breakBlock.GetComponent<MeshFilter>().mesh = mesh;
    }

    public void ToggleFly()
    {
        this.isFlying = !this.isFlying;
        CommandLineInterface.AddChatMessage($"Toggled Flying ({(this.isFlying ? "On" : "Off")})");
    }

    public void Teleport(int x, int y, int z)
    {
        int halfWorldSizeInVoxels = VoxelData.WorldSizeInVoxels / 2;
        Vector3 pos = new Vector3(x + halfWorldSizeInVoxels, y, z + halfWorldSizeInVoxels);
        if (!world.VoxelInWorld(pos))
        {
            CommandLineInterface.AddChatMessage($"Position is out of bounds of the World.");
            return;
        }
        transform.position = pos;
    }

    public void ChangeWeather(World.Weather weather)
    {
        switch (weather)
        {
            case World.Weather.Clear:
                world.CurrentWeather = weather;
                world.RainSystem.SetActive(false);
                world.SnowSystem.SetActive(false);
                CommandLineInterface.AddChatMessage($"Weather cleared");
                break;
            case World.Weather.Rain:
                world.CurrentWeather = weather;
                world.RainSystem.SetActive(true);
                world.SnowSystem.SetActive(false);
                CommandLineInterface.AddChatMessage($"Weather changed to rain");
                break;
            case World.Weather.Snow:
                world.CurrentWeather = weather;
                world.RainSystem.SetActive(false);
                world.SnowSystem.SetActive(true);
                CommandLineInterface.AddChatMessage($"Weather changed to snow");
                break;
        }
    }
    
    private float checkDownSpeed(float downSpeed)
    {
        if(
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth))
        ){
            this.isGrounded = true;
            verticalMomentum = 0;
            return 0;
        } 
        else
        {
            this.isGrounded = false;
            return downSpeed;
        }
    }

    private float checkUpSpeed(float upSpeed)
    {
        if (
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth)) ||
            world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth))
        )
        {
            verticalMomentum = 0;
            return 0;
        }
        else
        {
            return upSpeed;
        }
    }

    public bool front
    {
        get
        {
            if(
                world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z + playerWidth)) ||
                world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z + playerWidth))
            ){
                return true;
            } 
            else
            {
                return false;
            }
        }
    }

    public bool back
    {
        get
        {
            if (
                world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y, transform.position.z - playerWidth)) ||
                world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z - playerWidth))
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public bool left
    {
        get
        {
            if (
                world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y, transform.position.z)) ||
                world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + 1f, transform.position.z))
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public bool right
    {
        get
        {
            if (
                world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y, transform.position.z)) ||
                world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + 1f, transform.position.z))
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
