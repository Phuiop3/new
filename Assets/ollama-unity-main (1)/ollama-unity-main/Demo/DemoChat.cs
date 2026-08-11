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
    private string demoModel = "qwen3:4b";


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


    private bool bold;
    private bool italic;


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
        if (!isStreaming)
            return;

        if (llmOutput == null)
            return;


        while (
            buffer.TryDequeue(
                out string text
            )
        )
        {
            text =
                text.Replace(
                    "\n\n",
                    "\n"
                );


            // -------------------------------------------------
            // BOLD
            // -------------------------------------------------

            if (text.Contains("**"))
            {
                bold =
                    !bold;

                text =
                    text.Replace(
                        "**",
                        bold
                            ? "<b>"
                            : "</b>"
                    );
            }


            // -------------------------------------------------
            // ITALIC
            // -------------------------------------------------

            if (text.Contains("*"))
            {
                italic =
                    !italic;

                text =
                    text.Replace(
                        "*",
                        italic
                            ? "<i>"
                            : "</i>"
                    );
            }


            llmOutput.text +=
                text;
        }
    }


    // =========================================================
    // ASK
    // =========================================================

    public async Task Ask(
        string prompt
    )
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


        // -----------------------------------------------------
        // RESET OUTPUT
        // -----------------------------------------------------

        if (llmOutput != null)
        {
            llmOutput.text = "";
        }


        bold = false;
        italic = false;


        completeResponse =
            "";


        isStreaming =
            true;


        // -----------------------------------------------------
        // AI INSTRUCTIONS
        // -----------------------------------------------------

        string instructions = @"
You are an AI assistant controlling a Unity XR environment.

You can control Unity objects.

IMPORTANT:
When the user asks you to CREATE something in Unity,
return ONLY valid JSON.

Do NOT use markdown.
Do NOT use code fences.
Do NOT write explanations before or after the JSON.

============================================================
CREATE PRIMITIVE
============================================================

If the user asks to create a primitive object, use:

{
    ""tool"": ""create_primitive"",
    ""objectType"": ""cube"",
    ""color"": ""#FF0000"",
    ""position"": {
        ""x"": 0,
        ""y"": 0,
        ""z"": 0
    },
    ""rotation"": {
        ""x"": 0,
        ""y"": 0,
        ""z"": 0
    },
    ""scale"": {
        ""x"": 1,
        ""y"": 1,
        ""z"": 1
    }
}

Allowed objectType values:

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
    ""color"": ""#FF0000"",
    ""position"": {
        ""x"": 0,
        ""y"": 0,
        ""z"": 0
    },
    ""rotation"": {
        ""x"": 0,
        ""y"": 0,
        ""z"": 0
    },
    ""scale"": {
        ""x"": 1,
        ""y"": 1,
        ""z"": 1
    }
}

============================================================
CREATE WALL
============================================================

If the user asks to create a wall, use:

{
    ""tool"": ""create_wall"",
    ""startX"": 0,
    ""startZ"": 0,
    ""endX"": 5,
    ""endZ"": 0,
    ""height"": 2.7,
    ""thickness"": 0.15
}

The wall coordinates use:

X = left/right
Z = forward/back
Y = vertical

Wall dimensions are in metres.

============================================================
CLEAR HOUSE
============================================================

If the user asks to remove or clear the generated house,
use:

{
    ""tool"": ""clear_house""
}

============================================================
NORMAL QUESTIONS
============================================================

If the user is NOT asking you to create or modify something
in Unity, answer normally.

When creating Unity objects, return ONLY the JSON object.

";


        // -----------------------------------------------------
        // SEND TO OLLAMA
        // -----------------------------------------------------

        try
        {
            await Ollama.ChatStream(
                text =>
                {
                    completeResponse +=
                        text;

                    buffer.Enqueue(
                        text
                    );
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

            isStreaming =
                false;

            return;
        }


        // -----------------------------------------------------
        // STREAM FINISHED
        // -----------------------------------------------------

        isStreaming =
            false;


        Debug.Log(
            "[DemoChat] Complete response:\n" +
            completeResponse
        );


        // -----------------------------------------------------
        // TRY UNITY TOOL
        // -----------------------------------------------------

        TryExecuteUnityTool(
            completeResponse
        );
    }


    // =========================================================
    // TRY EXECUTE UNITY TOOL
    // =========================================================

    private void TryExecuteUnityTool(
        string response
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                response
            )
        )
        {
            return;
        }


        string json =
            ExtractJson(
                response
            );


        if (
            string.IsNullOrWhiteSpace(
                json
            )
        )
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
            // -------------------------------------------------
            // Read the tool name first.
            // -------------------------------------------------

            ToolCommand command =
                JsonUtility.FromJson<ToolCommand>(
                    json
                );


            if (command == null)
            {
                Debug.LogWarning(
                    "[DemoChat] Could not create ToolCommand."
                );

                return;
            }


            if (
                string.IsNullOrEmpty(
                    command.tool
                )
            )
            {
                Debug.Log(
                    "[DemoChat] JSON does not contain a tool."
                );

                return;
            }


            // -------------------------------------------------
            // Create SHARED ToolArguments.
            //
            // This is the important part.
            //
            // ToolArguments comes from:
            //
            // AIToolData.cs
            //
            // NOT UnityToolManager.ToolArguments.
            // -------------------------------------------------

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


            // -------------------------------------------------
            // EXECUTE TOOL
            // -------------------------------------------------

            unityToolManager.ExecuteTool(
                command.tool,
                args
            );


            // -------------------------------------------------
            // OUTPUT
            // -------------------------------------------------

            if (llmOutput != null)
            {
                switch (command.tool)
                {
                    case "create_primitive":

                        llmOutput.text =
                            "Created " +
                            command.objectType;

                        break;


                    case "create_wall":

                        llmOutput.text =
                            "Created wall.";

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
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "[DemoChat] Could not parse Unity tool command: " +
                e.Message
            );
        }
    }


    // =========================================================
    // EXTRACT JSON
    // =========================================================

    private string ExtractJson(
        string response
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                response
            )
        )
        {
            return null;
        }


        string json =
            response.Trim();


        // -----------------------------------------------------
        // Remove markdown code fences
        // -----------------------------------------------------

        if (
            json.StartsWith(
                "```"
            )
        )
        {
            int firstNewLine =
                json.IndexOf(
                    '\n'
                );


            if (firstNewLine >= 0)
            {
                json =
                    json.Substring(
                        firstNewLine + 1
                    );
            }


            int fence =
                json.LastIndexOf(
                    "```"
                );


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
        // Find JSON object
        // -----------------------------------------------------

        int jsonStart =
            json.IndexOf(
                '{'
            );


        int jsonEnd =
            json.LastIndexOf(
                '}'
            );


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

        public string color;

        public Vector3 position;

        public Vector3 rotation;

        public Vector3 scale =
            Vector3.one;

        // Wall

        public float startX;

        public float startZ;

        public float endX;

        public float endZ;

        public float height;

        public float thickness;
    }
}