using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.UI;
using TMPro;
using ShatterToolkit;

public class Player : MonoBehaviour
{
    /* VR Control related stuff */
    [SerializeField]
    private XRNode xrNode = XRNode.LeftHand;
    private List<InputDevice> devices = new List<InputDevice>();
    private InputDevice rightHandDevice;
    private InputDevice leftHandDevice;
    
    // Available gamemodes
    public enum Gamemode
    {
        Survival, 
        Creative,
        Builder
    }

    /// <summary>
    /// The Players current gamemode
    /// </summary>
    public Gamemode Mode = Gamemode.Creative;

    /// <summary>
    /// Is the Player currently in vr?
    /// </summary>
    public bool InVR = false;
    private bool RayOverUI = false;

    // gameobjects for the left and right hand
    public GameObject LeftHand;
    public GameObject RightHand;

    /// <summary>
    /// Is the player currently on the ground?
    /// </summary>
    public bool isGrounded;
    /// <summary>
    /// is the player currently flying?
    /// </summary>
    public bool isFlying;
    /// <summary>
    /// is the player currently sprinting?
    /// </summary>
    public bool isSprinting;
    /// <summary>
    /// is the player currently zooming?
    /// </summary>
    public bool isZooming;
    /// <summary>
    /// Is the block preview visible?
    public bool isPlaceVoxelVisible;
    /// <summary>
    /// Is the command line visible?
    /// </summary>
    public bool commandLineVisible;

    private Transform cam;
    private Camera camObject;
    private World world;

    // player stats (speed, jump force, gravity, ...)
    public float WalkSpeed = 3f;
    public float SprintSpeed = 6f;
    public float FlyingSpeed = 6f;
    public float FlyingSprintSpeed = 18f;
    public float JumpForce = 5f;
    public float Gravity = -9.81f;

    // player size
    public float playerWidth = 0.4f;
    public float playerHeight = 1.8f;

    // cooldowns for specific actions in vr
    private float breakCooldown = 0f;
    private float placeCooldown = 0f;
    private float scrollCooldown = 0f;

    public float zoomTransitionDuration = 0.5f;

    // movement related stuff
    private float horizontal;
    private float vertical;

    private float mouseHorizontal;
    private float mouseVertical;

    private Vector3 velocity;

    private float verticalMomentum = 0;
    private bool jumpRequest;

    // raycast related stuff (to show the block preview and the highlighted block)
    public Transform HighlightVoxel;
    public Transform PlaceVoxel;
    public MeshFilter PlaceVoxelMesh;
    byte PlaceVoxelId = 0;

    /// <summary>
    /// The original block of the first point of the builder area (to reset the area later)
    /// </summary>
    public byte BuilderAreaP1Block = 0;
    /// <summary>
    /// The original block of the second point of the builder area (to reset the area later)
    /// </summary>
    public byte BuilderAreaP2Block = 0;
    /// <summary>
    /// The first point of the builder area
    /// </summary>
    public Vector3 BuilderAreaP1 = new Vector3(float.NaN, float.NaN, float.NaN);
    /// <summary>
    /// The second point of the builder area
    /// </summary>
    public Vector3 BuilderAreaP2 = new Vector3(float.NaN, float.NaN, float.NaN);
    /// <summary>
    /// The area cube that is used to show the builder area
    /// </summary>
    public GameObject AreaCube;
    public GameObject AreaCubeMesh;

    /// <summary>
    /// Prefab of the block break effect
    /// </summary>
    public GameObject BlockBreakPrefab;
    
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
    public Toolbar Toolbar;

    private void Start(){
        // hide and lock cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        // get references
        cam = GameObject.Find("Main Camera").transform;
        camObject = cam.gameObject.GetComponent<Camera>();
        world = GameObject.Find("World").GetComponent<World>();
        CommandLineInterface = CommandLine.GetComponent<CommandLine>();
        CommandLinePanel = CommandLine.GetComponent<Image>();
    }

    private void OnEnable()
    {
        // if the player is in vr, get the devices
        if ((!rightHandDevice.isValid || !leftHandDevice.isValid) && InVR)
        {
            GetDevice();
        }
    }


    private void FixedUpdate()
    {
        // update velocity
        CalculateVelocity();
        // if the player pressed jump and he isn't flying yet, jump
        if (jumpRequest && !this.isFlying) Jump();
        // else if he is flying check for flying movements
        else if (this.isFlying && !this.commandLineVisible)
        {
            // VR Inputs
            bool primaryButtonActive = false;
            if (InVR) rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButtonActive);
            bool secondaryButtonActive = false;
            if (InVR) rightHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryButtonActive);

            // check for flying inputs
            if (Input.GetKey(KeyCode.Space) || primaryButtonActive)
            {
                velocity.y = .15f;
            } else if (Input.GetMouseButton(2) || secondaryButtonActive)
            {
                velocity.y = -.15f;
            }
        }

        // move the player (apply velocity)
        transform.Translate(velocity, Space.World);
    }


    private void Update(){
        // if the player is in vr, get the devices (if they are not valid yet)
        if ((!rightHandDevice.isValid || !leftHandDevice.isValid) && InVR)
        {
            GetDevice();
        }

        // get current inputs
        GetPlayerInputs();
        // place the block preview and highlight the currently looked block
        PlaceCursorBlocks();

        // rotation (only if not in VR, handled differently there)
        if (!commandLineVisible && !InVR)
        {
            transform.Rotate(Vector3.up * mouseHorizontal);
            cam.Rotate(Vector3.right * -mouseVertical);
        }

        // VR rotation (with joystick, because you can look around with your head in VR)
        if (InVR)
        {
            transform.Rotate(Vector3.up * mouseHorizontal * 2);
        }

        // TODO: clamp camera rotation

        // transition between zoomed and unzoomed
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

        // if the player is in vr, update the cooldowns
        if (InVR)
        {
            if (placeCooldown > 0) placeCooldown -= Time.deltaTime;
            if (breakCooldown > 0) breakCooldown -= Time.deltaTime;
            if (scrollCooldown > 0) scrollCooldown -= Time.deltaTime;
        }
        // else just reset them
        else
        {
            placeCooldown = 0;
            breakCooldown = 0;
            scrollCooldown = 0;
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

        // sprint multiplier (for flying and walking)
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

        // reset the velocity on the y axis if the player is flying
        if (this.isFlying) velocity.y = 0;
    }

    /// <summary>
    /// Execute a jump
    /// </summary>
    void Jump()
    {
        // set the vertical momentum to the jump force
        verticalMomentum = JumpForce;
        // set isgrounded to false (so he can't jump again)
        isGrounded = false;
        // reset the jump request
        jumpRequest = false;
    }

    /// <summary>
    /// Fetch the current player inputs
    /// </summary>
    private void GetPlayerInputs(){
        // wasd inputs
        horizontal = Input.GetAxisRaw("Horizontal");
        vertical = Input.GetAxisRaw("Vertical");

        // reset the velocity if the command line is visible
        if (commandLineVisible)
        {
            horizontal = 0;
            vertical = 0;
        }

        // mouse axis inputs
        mouseHorizontal = Input.GetAxis("Mouse X");
        mouseVertical = Input.GetAxis("Mouse Y");

        // if in vr mode, get the input from the right hand controller joystick
        if (InVR)
        {
            Vector2 vrMovement = new Vector2(0, 0);
            rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out vrMovement);
            mouseHorizontal = vrMovement.x;
            mouseVertical = vrMovement.y;

            // get vr inputs for scrolling in the toolbar
            bool primaryLeftButtonActive = false;
            if(InVR) leftHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out primaryLeftButtonActive);
            bool secondaryLeftButtonActive = false;
            if (InVR) leftHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryLeftButtonActive);

            if (primaryLeftButtonActive && scrollCooldown <= 0)
            {
                Toolbar.SetSlotIndex(Toolbar.slotIndex - 1);
                scrollCooldown = .2f;
            }
            if (secondaryLeftButtonActive && scrollCooldown <= 0)
            {
                Toolbar.SetSlotIndex(Toolbar.slotIndex + 1);
                scrollCooldown = .2f;
            }
        }
        
        
        bool leftGripActive = false;
        if(InVR) leftHandDevice.TryGetFeatureValue(CommonUsages.gripButton, out leftGripActive);
        // enable sprinting (shift or left grip in vr)
        if (Input.GetKeyDown(KeyCode.LeftShift) || (leftGripActive && InVR))
        {
            this.isSprinting = true;
        }

        // disable sprinting (shift or left grip in vr)
        if (Input.GetKeyUp(KeyCode.LeftShift) || (!leftGripActive && InVR)){
            this.isSprinting = false;
        }

        // enable zooming (c)
        if (Input.GetKeyDown(KeyCode.C) && !this.commandLineVisible)
        {
            this.isZooming = true;
        }

        // disable zooming (c)
        if (Input.GetKeyUp(KeyCode.C) && !this.commandLineVisible)
        {
            this.isZooming = false;
        }

        // show the command line (t)
        if (Input.GetKeyDown(KeyCode.T) && !this.commandLineVisible)
        {
            // unlock cursor and show it
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // set the command line panel to visible
            CommandLinePanel.color = new Color(0, 0, 0, 0.5f);
            CommandLineInterface.InputField.textComponent.alpha = 1f;
            CommandLineInterface.InputField.placeholder.GetComponent<TextMeshProUGUI>().alpha = .5f;
            CommandLineInterface.InputField.interactable = true;
            // focus on input
            CommandLineInterface.InputField.Select();
            CommandLineInterface.InputField.ActivateInputField();
            this.commandLineVisible = true;
        }

        // hide the command line (enter) and execute the command
        if (Input.GetKeyDown(KeyCode.Return))
        {
            // check if the command that was sent is valid
            bool valid = CommandLineInterface.ExecuteCommand();
            // if the command is valid, hide the command line
            if (valid)
            {
                // lock cursor and hide it
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                // hide the command line panel
                CommandLinePanel.color = new Color(0, 0, 0, 0.0f);
                CommandLineInterface.InputField.textComponent.alpha = 0.0f;
                CommandLineInterface.InputField.placeholder.GetComponent<TextMeshProUGUI>().alpha = 0f;
                // deactivate the input field
                CommandLineInterface.InputField.interactable = false;
                CommandLineInterface.InputField.DeactivateInputField(true);
                this.commandLineVisible = false;
            }
        }

        // toggle the visibility of the block previews (p)
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

        
        bool primaryButtonActive = false;
        if (InVR) rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButtonActive);
        // jump (space or primary button in vr)
        if (this.isGrounded && (Input.GetKeyDown(KeyCode.Space) || primaryButtonActive) && !this.commandLineVisible)
        {
            this.jumpRequest = true;
        }

        // only enable voxel placing or breaking if player looks at a voxel currently
        if (HighlightVoxel.gameObject.activeSelf && !commandLineVisible)
        {
            bool rightTriggerActive = false;
            if (InVR)
            {
                rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out rightTriggerActive);
                // if the right trigger is not active reset cooldown
                if (!rightTriggerActive) breakCooldown = 0f;
            }
            // left click or right trigger in vr to break a block
            if ((Input.GetMouseButtonDown(0) || rightTriggerActive) && breakCooldown <= 0)
            {
                // set the cooldown
                breakCooldown = .5f;
                if(Mode == Gamemode.Builder)
                {
                    // if the clicked voxel is not already the first point of the builder area, set it
                    if (BuilderAreaP1 != HighlightVoxel.position)
                    {
                        // if there was a previous first point, reset it to its original block
                        if (!float.IsNaN(BuilderAreaP1.x)) world.GetChunkFromVector3(BuilderAreaP1).EditVoxel(BuilderAreaP1, BuilderAreaP1Block);

                        // set the first point
                        BuilderAreaP1 = HighlightVoxel.position;
                        
                        // get the chunk the voxel is in and edit it to be bedrock (for now, later a kind of barrier block)
                        Chunk chunk = world.GetChunkFromVector3(HighlightVoxel.position);
                        BuilderAreaP1Block = chunk.GetVoxelFromGlobalVector3(BuilderAreaP1);
                        chunk.EditVoxel(HighlightVoxel.position, 1);
                        // update the builder area
                        UpdateBuilderArea();
                    }
                    else
                    {
                        // reset the first point
                        BuilderAreaP1 = new Vector3(float.NaN, float.NaN, float.NaN);
                        UpdateBuilderArea();
                        world.GetChunkFromVector3(HighlightVoxel.position).EditVoxel(HighlightVoxel.position, BuilderAreaP1Block);
                    }
                }
                else {
                    // get the block id of the voxel that is being broken
                    byte blockId = world.GetChunkFromVector3(HighlightVoxel.position).GetVoxelFromGlobalVector3(HighlightVoxel.position);
                    // set the voxel to air
                    world.GetChunkFromVector3(HighlightVoxel.position).EditVoxel(HighlightVoxel.position, 0);

                    // create block break effect
                    var cube = Instantiate(BlockBreakPrefab);
                    // if the block is transparent, use the transparent material instead of the opaque one
                    if (world.blockTypes[blockId].isTransparent) cube.GetComponent<Renderer>().material = world.transparentVoxelMaterial;
                    // set the texture of the block break effect to the texture of the block that is being broken
                    ApplyBreakBlockTexture(world.blockTypes[blockId], cube);
                    // set the position and scale of the block break effect
                    cube.transform.position = new Vector3(HighlightVoxel.position.x, HighlightVoxel.position.y, HighlightVoxel.position.z);
                    cube.transform.localScale = new Vector3(1f, 1f, 1f);
                }
            }

            bool leftTriggerActive = false;
            if (InVR)
            {
                leftHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out leftTriggerActive);
                // if the left trigger is not active reset cooldown
                if (!leftTriggerActive) placeCooldown = 0f;
            }
            // right click or left trigger in vr to place a block
            if ((Input.GetMouseButtonDown(1) || leftTriggerActive) && placeCooldown <= 0) {
                placeCooldown = .5f;
                if(Mode == Gamemode.Builder)
                {
                    // if the clicked voxel is not already the second point of the builder area, set it
                    if (BuilderAreaP2 != HighlightVoxel.position)
                    {
                        // if there was a previous second point, reset it to its original block
                        if (!float.IsNaN(BuilderAreaP2.x)) world.GetChunkFromVector3(BuilderAreaP2).EditVoxel(BuilderAreaP2, BuilderAreaP2Block);

                        // set the second point
                        BuilderAreaP2 = HighlightVoxel.position;
                        // get the chunk the voxel is in and edit it to be stone (for now, later a kind of barrier block)
                        Chunk chunk = world.GetChunkFromVector3(HighlightVoxel.position);
                        BuilderAreaP2Block = chunk.GetVoxelFromGlobalVector3(BuilderAreaP2);
                        chunk.EditVoxel(HighlightVoxel.position, 2);
                        // update the builder area
                        UpdateBuilderArea();
                    } else
                    {
                        // reset the second point
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

    /// <summary>
    /// Update the builder area cube's position and scale
    /// </summary>
    private void UpdateBuilderArea()
    {
        // check if both points are set
        if (float.IsNaN(BuilderAreaP1.x) || float.IsNaN(BuilderAreaP2.x))
        {
            // if not set, hide the cube
            AreaCube.SetActive(false);
            AreaCube.transform.position = new Vector3(0, 0, 0);
            AreaCube.transform.localScale = new Vector3(0, 0, 0);
            return;
        }

        // else the cube is visible
        AreaCube.SetActive(true);

        // calculate min and max values for each axis to find the starting point to calculate the middle correctly
        float minX = Mathf.Min(BuilderAreaP1.x, BuilderAreaP2.x);
        float maxX = Mathf.Max(BuilderAreaP1.x, BuilderAreaP2.x);
        float minY = Mathf.Min(BuilderAreaP1.y, BuilderAreaP2.y);
        float maxY = Mathf.Max(BuilderAreaP1.y, BuilderAreaP2.y);
        float minZ = Mathf.Min(BuilderAreaP1.z, BuilderAreaP2.z);
        float maxZ = Mathf.Max(BuilderAreaP1.z, BuilderAreaP2.z);

        // calculate the middle of the cube
        int posX = Mathf.FloorToInt((minX + maxX) / 2);
        int posY = Mathf.FloorToInt((minY + maxY)) / 2;
        int posZ = Mathf.FloorToInt((minZ + maxZ) / 2);

        // calculate the distance of each axis to correct the position later
        int xDist = Mathf.FloorToInt(Mathf.Abs(BuilderAreaP2.x - BuilderAreaP1.x)) / 2;
        int yDist = Mathf.FloorToInt(Mathf.Abs(BuilderAreaP2.y - BuilderAreaP1.y)) / 2;
        int zDist = Mathf.FloorToInt(Mathf.Abs(BuilderAreaP2.z - BuilderAreaP1.z)) / 2;

        // correct the position of the cube (because the cube is offset by 0.5 in each direction)
        if (BuilderAreaP2.x < BuilderAreaP1.x && Mathf.Abs(BuilderAreaP2.x - BuilderAreaP1.x) > 1) posX -= xDist;
        else if (BuilderAreaP2.x > BuilderAreaP1.x && Mathf.Abs(BuilderAreaP2.x - BuilderAreaP1.x) > 1) posX -= xDist;

        if (BuilderAreaP2.y < BuilderAreaP1.y && Mathf.Abs(BuilderAreaP2.y - BuilderAreaP1.y) > 1) posY -= yDist;
        else if (BuilderAreaP2.y > BuilderAreaP1.y && Mathf.Abs(BuilderAreaP2.y - BuilderAreaP1.y) > 1) posY -= yDist;

        if (BuilderAreaP2.z < BuilderAreaP1.z && Mathf.Abs(BuilderAreaP2.z - BuilderAreaP1.z) > 1) posZ -= zDist;
        else if (BuilderAreaP2.z > BuilderAreaP1.z && Mathf.Abs(BuilderAreaP2.z - BuilderAreaP1.z) > 1) posZ -= zDist;

        Vector3 midpoint = new Vector3(posX, posY, posZ);
        // calculate its size
        float sizeX = maxX - minX;
        float sizeY = maxY - minY;
        float sizeZ = maxZ - minZ;

        // fix overlapping texture by increaasing scale by something very small
        sizeX += 0.001f;
        sizeY += 0.001f;
        sizeZ += 0.001f;

        // set the position and scale of the cube
        AreaCube.transform.position = midpoint;
        AreaCube.transform.localScale = new Vector3(sizeX + 1,sizeY + 1,sizeZ + 1);
        // if the scale is smaller than 1, set it to 1 (because if the cubes are on the same level the scale would be 0)
        if (AreaCube.transform.localScale.y < 1) AreaCube.transform.localScale = new Vector3(AreaCube.transform.localScale.x, 1, AreaCube.transform.localScale.z);
    }

    /// <summary>
    /// Fill in the set build area with the given block id
    /// </summary>
    /// <param name="blockId"></param>
    public void FillBuildArea(byte blockId)
    {
        // check if the build area is completed
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

        // iterate over all voxels in the build area and add them to the queue
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

        // add queue to the world's modifications (NOT WORKING CORRECTLY, NOT ALL CHUNKS GETTING UPDATED)
        world.modifications.Enqueue(mods);

        // clear the build area
        ClearBuildArea(false);

        // update the view distance to update the chunks (NOT WORKING CORRECTLY)
        world.CheckViewDistance();
    }

    /// <summary>
    /// Clear the build area
    /// </summary>
    /// <param name="revertBlocks"> reset the blocks to their original blocks? </param>
    public void ClearBuildArea(bool revertBlocks = true)
    {
        if(revertBlocks && !float.IsNaN(BuilderAreaP1.x)) world.GetChunkFromVector3(BuilderAreaP1).EditVoxel(BuilderAreaP1, BuilderAreaP1Block);
        BuilderAreaP1 = new Vector3(float.NaN, float.NaN, float.NaN);

        if (revertBlocks && !float.IsNaN(BuilderAreaP2.x)) world.GetChunkFromVector3(BuilderAreaP2).EditVoxel(BuilderAreaP2, BuilderAreaP2Block);
        BuilderAreaP2 = new Vector3(float.NaN, float.NaN, float.NaN);

        // update the build area (hide it)
        UpdateBuilderArea();
    }

    /// <summary>
    /// Shoot "fake" ray to check which block the player is currently looking at
    /// </summary>
    private void PlaceCursorBlocks()
    {
        float step = checkIncrement;
        Vector3 lastPos = new Vector3();

        while(step < reach && !RayOverUI)
        {
            // move forward with fake ray (if in vr the origin is the right hand instead of the cam)
            Vector3 pos = (!InVR) ? cam.position + (cam.forward * step) : RightHand.transform.position + (RightHand.transform.forward * step);

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
    /// Replaces the texture of the cube with the block break effect with the texture of the selected voxel
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

    /// <summary>
    /// Toggle flying
    /// </summary>
    public void ToggleFly()
    {
        this.isFlying = !this.isFlying;
        CommandLineInterface.AddChatMessage($"Toggled Flying ({(this.isFlying ? "On" : "Off")})");
    }

    /// <summary>
    /// teleport player to the given position (if it is valid)
    /// </summary>
    /// <param name="x"> x coordinate to teleport to </param>
    /// <param name="y"> y coordinate to teleport to</param>
    /// <param name="z"> z coordinate to teleport to</param>
    public void Teleport(int x, int y, int z)
    {
        // add half world size in voxel so it is accurate to the coordinates in the debug screen (0,0,0) is the center of the world
        int halfWorldSizeInVoxels = VoxelData.WorldSizeInVoxels / 2;
        Vector3 pos = new Vector3(x + halfWorldSizeInVoxels, y, z + halfWorldSizeInVoxels);
        // check if voxel is in the world before teleporting
        if (!world.VoxelInWorld(pos))
        {
            CommandLineInterface.AddChatMessage($"Position is out of bounds of the World.");
            return;
        }
        transform.position = pos;
    }

    /// <summary>
    /// Update the weather to the given weather
    /// </summary>
    /// <param name="weather"> the weather to change to </param>
    public void ChangeWeather(World.Weather weather)
    {
        switch (weather)
        {
            case World.Weather.Clear:
                // on clear just disable the rain and snow systems
                world.CurrentWeather = weather;
                world.RainSystem.SetActive(false);
                world.SnowSystem.SetActive(false);
                CommandLineInterface.AddChatMessage($"Weather cleared");
                break;
            case World.Weather.Rain:
                // on rain just enable the rain system and disable the snow system
                world.CurrentWeather = weather;
                world.RainSystem.SetActive(true);
                world.SnowSystem.SetActive(false);
                CommandLineInterface.AddChatMessage($"Weather changed to rain");
                break;
            case World.Weather.Snow:
                // on snow just enable the snow system and disable the rain system
                world.CurrentWeather = weather;
                world.RainSystem.SetActive(false);
                world.SnowSystem.SetActive(true);
                CommandLineInterface.AddChatMessage($"Weather changed to snow");
                break;
        }
    }

    /// <summary>
    /// fetch the device for the right and left hand
    /// </summary>
    private void GetDevice()
    {
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right, devices);
        rightHandDevice = devices.FirstOrDefault();

        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left, devices);
        leftHandDevice = devices.FirstOrDefault();
    }

    public void RayHoverUI(HoverEnterEventArgs e)
    {
        Debug.Log("Ray Hover UI");
        RayOverUI = true;
    }

    public void RayExitUI(HoverExitEventArgs e)
    {
        Debug.Log("Ray Left UI");
        RayOverUI = false;
    }

    /// <summary>
    /// check if the player can move downwards
    /// </summary>
    /// <param name="downSpeed"> current down speed </param>
    /// <returns> the resulting down speed </returns>
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

    /// <summary>
    /// check if the player can move upwards
    /// </summary>
    /// <param name="upSpeed"> current up speed </param>
    /// <returns> the resulting up speed </returns>
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

    /// <summary>
    /// Check if the player can move forward
    /// </summary>
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

    /// <summary>
    /// check if the player can move backward
    /// </summary>
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

    /// <summary>
    /// check if the player can move to the left
    /// </summary>
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

    /// <summary>
    /// check if the player can move to the right
    /// </summary>
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
