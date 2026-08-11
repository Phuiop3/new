using ollama;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class DemoChat : MonoBehaviour
{
    // =========================================================
    // MODEL
    // =========================================================

    [Header("Model")]
    [SerializeField]
    private string demoModel = "qwen3-vl";


    // =========================================================
    // OUTPUT
    // =========================================================

    [Header("Output")]
    [SerializeField]
    private TMP_Text llmOutput;


    // =========================================================
    // UNITY TOOLS
    // =========================================================

    [Header("Unity Tools")]
    [SerializeField]
    private UnityToolManager unityToolManager;


    // =========================================================
    // STREAMING
    // =========================================================

    private Queue<string> buffer;

    private bool isStreaming;

    private string completeResponse = "";


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        Ollama.Launch();
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        buffer =
            new Queue<string>();

        Ollama.InitChat();
    }


    // =========================================================
    // ENABLE
    // =========================================================

    private void OnEnable()
    {
        Ollama.OnStreamFinished +=
            StreamFinished;
    }


    // =========================================================
    // DISABLE
    // =========================================================

    private void OnDisable()
    {
        Ollama.OnStreamFinished -=
            StreamFinished;
    }


    // =========================================================
    // STREAM FINISHED
    // =========================================================

    private void StreamFinished()
    {
        isStreaming = false;
    }


    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (llmOutput == null)
            return;

        while (
            buffer != null &&
            buffer.TryDequeue(
                out string text
            )
        )
        {
            llmOutput.text += text;
        }
    }


    // =========================================================
    // ASK
    // =========================================================

    public async Task Ask(
        string prompt)
    {
        if (isStreaming)
        {
            Debug.LogWarning(
                "[DemoChat] Already processing a request."
            );

            return;
        }


        if (unityToolManager == null)
        {
            Debug.LogError(
                "[DemoChat] UnityToolManager is not assigned!"
            );

            return;
        }


        if (string.IsNullOrWhiteSpace(prompt))
        {
            Debug.LogWarning(
                "[DemoChat] Prompt is empty."
            );

            return;
        }


        if (llmOutput != null)
        {
            llmOutput.text = "";
        }


        completeResponse = "";

        isStreaming = true;


        // =====================================================
        // AI INSTRUCTIONS
        // =====================================================

        string instructions = @"
You are an AI assistant controlling a Unity XR environment.

Your purpose is to help the user design a house in VR.

The user is the designer.

You help the user create, modify and remove objects.

IMPORTANT:

When the user asks you to CREATE something in Unity,
return ONLY valid JSON.

When the user asks you to MOVE something,
return ONLY valid JSON.

When the user asks you to RESIZE something,
return ONLY valid JSON.

When the user asks you to DELETE something,
return ONLY valid JSON.

When the user asks to CLEAR the generated house,
return ONLY valid JSON.

For normal questions, answer normally.

Do not use markdown.

Do not use code fences.

Do not write explanations around a Unity JSON command.


=========================================================
TOOL 1: CREATE PRIMITIVE
=========================================================

Use this for simple objects such as:

table
chair
bed
desk
sofa
cabinet
plant
lamp
box

Allowed primitive types:

cube
sphere
cylinder
capsule
plane
quad

Example:

{
  ""tool"": ""create_primitive"",
  ""objectType"": ""cube"",
  ""name"": ""DiningTable"",
  ""color"": ""#FFFFFF"",
  ""position"": {
    ""x"": 0,
    ""y"": 0.5,
    ""z"": 0
  },
  ""rotation"": {
    ""x"": 0,
    ""y"": 0,
    ""z"": 0
  },
  ""scale"": {
    ""x"": 2,
    ""y"": 1,
    ""z"": 1
  }
}


=========================================================
TOOL 2: CREATE WALL
=========================================================

Use this to create architectural walls.

Example:

{
  ""tool"": ""create_wall"",
  ""name"": ""LivingRoom_Wall_01"",
  ""startX"": 0,
  ""startZ"": 0,
  ""endX"": 5,
  ""endZ"": 0,
  ""height"": 2.7,
  ""thickness"": 0.15
}

Coordinates:

X = left/right
Z = forward/back
Y = vertical

All dimensions are metres.


=========================================================
TOOL 3: MOVE OBJECT
=========================================================

Use this when the user wants to move an existing object.

Example:

{
  ""tool"": ""move_object"",
  ""targetName"": ""DiningTable"",
  ""moveX"": 1,
  ""moveY"": 0,
  ""moveZ"": 0
}


=========================================================
TOOL 4: RESIZE OBJECT
=========================================================

Use this when the user wants to make an object bigger or smaller.

Example:

{
  ""tool"": ""resize_object"",
  ""targetName"": ""DiningTable"",
  ""scaleX"": 3,
  ""scaleY"": 1,
  ""scaleZ"": 1.5
}


=========================================================
TOOL 5: DELETE OBJECT
=========================================================

Use this when the user wants to remove an object.

Example:

{
  ""tool"": ""delete_object"",
  ""targetName"": ""DiningTable""
}


=========================================================
TOOL 6: CLEAR HOUSE
=========================================================

Use this when the user asks to remove everything.

Example:

{
  ""tool"": ""clear_house""
}


=========================================================
IMPORTANT NAMING RULE
=========================================================

Every created object must have a meaningful unique name.

Examples:

LivingRoom
Kitchen
Bedroom_1
Bedroom_2
DiningTable
Sofa
Bed_1
Wall_Living_01
Wall_Living_02

When modifying or deleting an object,
use its exact name as targetName.


=========================================================
DESIGN RULE
=========================================================

Do not automatically make large design decisions unless
the user asks you to.

The user should remain in control of the design.

";


        // =====================================================
        // SEND TO OLLAMA
        // =====================================================

        try
        {
            await Ollama.ChatStream(
                text =>
                {
                    completeResponse += text;

                    buffer.Enqueue(text);
                },

                demoModel,

                instructions +
                "\nUSER:\n" +
                prompt +
                "\n\n/no_think"
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[DemoChat] Ollama error: " +
                e.Message
            );

            isStreaming = false;

            return;
        }


        // =====================================================
        // FINISHED
        // =====================================================

        isStreaming = false;


        Debug.Log(
            "[DemoChat] Complete response:\n" +
            completeResponse
        );


        // =====================================================
        // EXECUTE UNITY TOOL
        // =====================================================

        TryExecuteUnityTool(
            completeResponse
        );
    }


    // =========================================================
    // EXECUTE UNITY TOOL
    // =========================================================

    private void TryExecuteUnityTool(
        string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return;
        }


        string json =
            ExtractJson(response);


        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.Log(
                "[DemoChat] No Unity JSON command found."
            );

            return;
        }


        Debug.Log(
            "[DemoChat] Unity JSON:\n" +
            json
        );


        try
        {
            ToolCommand command =
                JsonUtility.FromJson<ToolCommand>(
                    json
                );


            if (command == null)
            {
                Debug.LogWarning(
                    "[DemoChat] Could not parse ToolCommand."
                );

                return;
            }


            if (string.IsNullOrWhiteSpace(command.tool))
            {
                Debug.LogWarning(
                    "[DemoChat] JSON contains no tool."
                );

                return;
            }


            // Convert JSON into shared ToolArguments.
            ToolArguments args =
                JsonUtility.FromJson<ToolArguments>(
                    json
                );


            if (args == null)
            {
                Debug.LogWarning(
                    "[DemoChat] Could not parse ToolArguments."
                );

                return;
            }


            // Execute inside Unity.
            unityToolManager.ExecuteTool(
                command.tool,
                args
            );


            // Display simple result.
            ShowToolResult(
                command,
                args
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[DemoChat] Tool execution error: " +
                e.Message
            );
        }
    }


    // =========================================================
    // SHOW RESULT
    // =========================================================

    private void ShowToolResult(
        ToolCommand command,
        ToolArguments args)
    {
        if (llmOutput == null)
            return;


        switch (command.tool)
        {
            case "create_primitive":

                llmOutput.text =
                    "Created " +
                    args.name;

                break;


            case "create_wall":

                llmOutput.text =
                    "Created wall " +
                    args.name;

                break;


            case "move_object":

                llmOutput.text =
                    "Moved " +
                    args.targetName;

                break;


            case "resize_object":

                llmOutput.text =
                    "Resized " +
                    args.targetName;

                break;


            case "delete_object":

                llmOutput.text =
                    "Deleted " +
                    args.targetName;

                break;


            case "clear_house":

                llmOutput.text =
                    "House cleared.";

                break;


            default:

                llmOutput.text =
                    "Executed: " +
                    command.tool;

                break;
        }
    }


    // =========================================================
    // EXTRACT JSON
    // =========================================================

    private string ExtractJson(
        string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }


        string json =
            response.Trim();


        // -----------------------------------------------------
        // Remove markdown code fences if Qwen adds them.
        // -----------------------------------------------------

        if (json.StartsWith("```"))
        {
            int firstNewLine =
                json.IndexOf('\n');


            if (firstNewLine >= 0)
            {
                json =
                    json.Substring(
                        firstNewLine + 1
                    );
            }


            int fence =
                json.LastIndexOf("```");


            if (fence >= 0)
            {
                json =
                    json.Substring(
                        0,
                        fence
                    );
            }


            json =
                json.Trim();
        }


        // -----------------------------------------------------
        // Find first JSON object.
        // -----------------------------------------------------

        int jsonStart =
            json.IndexOf('{');


        int jsonEnd =
            json.LastIndexOf('}');


        if (
            jsonStart < 0 ||
            jsonEnd < jsonStart
        )
        {
            return null;
        }


        return json.Substring(
            jsonStart,
            jsonEnd - jsonStart + 1
        );
    }


    // =========================================================
    // TOOL COMMAND
    // =========================================================

    [Serializable]
    private class ToolCommand
    {
        public string tool;

        public string objectType;

        public string name;

        public string targetName;

        public string color;

        public string material;

        public Vector3 position;

        public Vector3 rotation;

        public Vector3 scale =
            Vector3.one;

        public float startX;
        public float startZ;

        public float endX;
        public float endZ;

        public float height;
        public float thickness;

        public float width;
        public float depth;

        public float moveX;
        public float moveY;
        public float moveZ;

        public float scaleX;
        public float scaleY;
        public float scaleZ;
    }
}