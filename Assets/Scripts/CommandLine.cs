using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;

public class CommandLine : MonoBehaviour
{
    /// <summary>
    /// Input Field of the chat box
    /// </summary>
    public TMP_InputField InputField;

    public Player Player;

    /// <summary>
    /// the chat box panel image
    /// </summary>
    public Image ChatBox;

    /// <summary>
    /// The chat box game object
    /// </summary>
    public GameObject Chat;

    /// <summary>
    /// Prefab for chat messages
    /// </summary>
    public GameObject ChatMessagePrefab;

    /// <summary>
    /// Prefab for command recommendation
    /// </summary>
    public GameObject CommandPrefab;

    /// <summary>
    /// the command list panel
    /// </summary>
    public GameObject CommandList;
    
    /// <summary>
    /// List of all message components
    /// </summary>
    public List<TextMeshProUGUI> Messages;
    
    /// <summary>
    /// List of all commands (in the recommendation list)
    /// </summary>
    public List<GameObject> Commands;

    /// <summary>
    /// Timer for chat messages (to fade in and out)
    /// </summary>
    private float chatTimer = 0f;

    /// <summary>
    /// bool to indicate whether the chat is visible or not
    /// </summary>
    private bool showChat = false;

    /// <summary>
    /// current chat opacity
    /// </summary>
    private float alpha = 0f;

    public void Start()
    {
        // command list for the recommendation list
        List<string> commands = new List<string>
        {
            ("/fly"),
            ("/tp x y z"),
            ("/gamemode mode <color=#ffff00ff>survival"),
            ("/clearbuildarea"),
            ("/fillbuildarea id <color=#ffff00ff>1 | stone"),
            ("/weather weather <color=#ffff00ff>clear | rain")
        };

        // initialize and add commands to the recommandation list
        foreach(string cmd in commands)
        {
            // instantiate the command prefab
            var command = Instantiate(CommandPrefab, CommandList.transform);
            var tmPro = command.GetComponent<TextMeshProUGUI>();
            tmPro.text = cmd;
            Commands.Add(command.gameObject);
        }
        
        Chat = ChatBox.gameObject;
        // add event listener for the input field
        InputField.onValueChanged.AddListener(HandleUpdate);
    }

    public void Update()
    {
        // fade transition for the chat box and messages
        if (showChat && alpha < .5f)
        {
            alpha += Time.deltaTime * 2f;
            var newColor = new Color(ChatBox.color.r, ChatBox.color.g, ChatBox.color.b, alpha);
            ChatBox.color = newColor;
            // also fade the messages
            StartCoroutine(FadeMessages(alpha * 2));
        }
        else if (!showChat && ChatBox.color.a > 0f)
        {
            alpha -= Time.deltaTime * 2f;
            var newColor = new Color(ChatBox.color.r, ChatBox.color.g, ChatBox.color.b, alpha);
            ChatBox.color = newColor;
            // also fade the messages
            StartCoroutine(FadeMessages(alpha));
        }

        // increment timer
        if(showChat) chatTimer += Time.deltaTime;

        // if timer is over 5 seconds, hide the chat box and reset the timer
        if (chatTimer > 5f && showChat)
        {
            showChat = false;
            chatTimer = 0f;
        }
    }

    /// <summary>
    /// Handle input field update (update command list)
    /// </summary>
    /// <param name="str"> current input </param>

    public void HandleUpdate(string str)
    {
        // hide / show command list depending on whether the input field contains a slash
        CommandList.SetActive(str.Contains('/'));

        // if the input field contains a slash, show only commands that contain the input
        if (str.Contains('/'))
        {
            foreach (var cmd in Commands)
            {
                var text = cmd.GetComponent<TextMeshProUGUI>().text;
                var keyword = str.Split(' ')[0].Replace("/", String.Empty);
                if (text.Contains(keyword))
                {
                    cmd.SetActive(true);
                } else
                {
                    cmd.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Add message to chat box
    /// </summary>
    /// <param name="msg"> message to add </param>
    public void AddChatMessage(string msg)
    {
        // instantiate message prefab and set text
        var msgObj = Instantiate(ChatMessagePrefab, ChatBox.transform);
        TextMeshProUGUI message = msgObj.GetComponent<TextMeshProUGUI>();
        message.text = $"\n{msg}";
        // show chat and reset timer
        showChat = true;
        chatTimer = 0f;
        Messages.Add(message);
    }

    /// <summary>
    /// fade in all message with a delay between them
    /// </summary>
    /// <param name="alpha"> the alpha to set them to </param>
    /// <returns></returns>
    IEnumerator FadeMessages(float alpha)
    {
        foreach(var msg in Messages)
        {
            msg.alpha = alpha;
            yield return new WaitForSeconds(0.15f);
        }
    }

    /// <summary>
    /// Execute command / send message that is currently in the input field
    /// </summary>
    /// <returns> bool that indicates if a command was successful / valid (for normal messages true)</returns>
    public bool ExecuteCommand()
    {
        // if message doesnt contain a slash it is a normal message
        if (!InputField.text.Contains('/'))
        {
            Debug.Log("Normal message");
            InputField.text = "";
            return true;
        }

        // split command
        string[] parts = InputField.text.Split(' ');
        // get the keyword
        string keyword = parts[0].Replace("/", string.Empty);

        // check which command is being executed
        switch (keyword)
        {
            case "fly":
                Player.ToggleFly();
                break;
            case "tp":
                // check if there are the amount of parameters required by the command
                if(parts.Length != 4)
                {
                    Debug.Log("Wrong amount of parameters");
                    InputField.text = "";
                    InputField.text = "";
                    return true;
                }
                
                // parse coordinates
                int x = int.Parse(parts[1]);
                int y = int.Parse(parts[2]);
                int z = int.Parse(parts[3]);
                
                Player.Teleport(x,y,z);
                break;
            case "gamemode":
                // check if there are the amount of parameters required by the command
                if (parts.Length != 2)
                {
                    Debug.Log("Wrong amount of parameters");
                    InputField.text = "";
                    InputField.text = "";
                    return true;
                }

                string mode = parts[1];
                // try to parse given mode to enum
                if(Enum.TryParse(typeof(Player.Gamemode), mode, true, out object res))
                {
                    // set current mode
                    Player.Mode = (Player.Gamemode)res;
                    AddChatMessage($"Switched to {Player.Mode.ToString()}-Mode");
                } else
                {
                    AddChatMessage($"Unknown Gamemode");
                }
                break;
            case "clearbuildarea":
                Player.ClearBuildArea();
                break;
            case "weather":
                // check if there are the amount of parameters required by the command
                if (parts.Length != 2)
                {
                    Debug.Log("Wrong amount of parameters");
                    InputField.text = "";
                    InputField.text = "";
                    return true;
                }

                string weather = parts[1];
                // try to parse given mode to enum
                if (Enum.TryParse(typeof(World.Weather), weather, true, out object weatherObj))
                {
                    // set current mode
                    Player.ChangeWeather((World.Weather)weatherObj);
                }
                else
                {
                    AddChatMessage($"Unknown Weather");
                }
                break;
            case "fillbuildarea":
                // check if there are the amount of parameters required by the command
                if (parts.Length != 2)
                {
                    Debug.Log("Wrong amount of parameters");
                    InputField.text = "";
                    InputField.text = "";
                    return true;
                }

                byte blockId = byte.Parse(parts[1]);
                Player.FillBuildArea(blockId);
                break;
            // command not in list
            default:
                Debug.Log("Command not found");
                InputField.text = "";
                InputField.text = "";
                return true;
        }

        InputField.text = "";
        InputField.text = "";
        return true;
    }
}