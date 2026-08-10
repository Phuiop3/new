using ollama;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class DemoChat : MonoBehaviour
{
    [Header("Model")]
    [SerializeField]
    private string demoModel = "qwen3:4b";

    [Header("Output")]
    [SerializeField]
    private TMP_Text llmOutput;

    [Header("Unity Tools")]
    [SerializeField]
    private UnityToolManager unityToolManager;

    private Queue<string> buffer;
    private bool isStreaming;

    private bool bold;
    private bool italic;

    private string completeResponse = "";

    private void Awake()
    {
        Ollama.Launch();
    }

    private void Start()
    {
        buffer = new Queue<string>();
        Ollama.InitChat();
    }

    private void OnEnable()
    {
        Ollama.OnStreamFinished += StreamFinished;
    }

    private void OnDisable()
    {
        Ollama.OnStreamFinished -= StreamFinished;
    }

    private void StreamFinished()
    {
        isStreaming = false;
    }

    private void LateUpdate()
    {
        if (!isStreaming)
            return;

        while (buffer.TryDequeue(out string text))
        {
            text = text.Replace("\n\n", "\n");

            if (text.Contains("**"))
            {
                bold = !bold;
                text = text.Replace(
                    "**",
                    bold ? "<b>" : "</b>"
                );
            }

            if (text.Contains("*"))
            {
                italic = !italic;
                text = text.Replace(
                    "*",
                    italic ? "<i>" : "</i>"
                );
            }

            llmOutput.text += text;
        }
    }

    public async Task Ask(string prompt)
    {
        if (isStreaming)
            return;

        if (unityToolManager == null)
        {
            Debug.LogError(
                "UnityToolManager is not assigned!"
            );
            return;
        }

        llmOutput.text = "";

        bold = false;
        italic = false;

        completeResponse = "";
        isStreaming = true;

        string instructions = @"
You are an AI assistant controlling a Unity XR environment.

If the user asks you to create a Unity primitive,
return ONLY valid JSON.

Allowed objectType:
cube
sphere
cylinder
capsule
plane
quad

JSON format:

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

Examples:

Create a red cube:
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

Create a blue sphere:
{
  ""tool"": ""create_primitive"",
  ""objectType"": ""sphere"",
  ""color"": ""#0000FF"",
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

If the user is not asking to create an object,
answer normally.

Do not use markdown around JSON.
";

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

        isStreaming = false;

        TryExecuteUnityTool(completeResponse);
    }

    private void TryExecuteUnityTool(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return;

        string json = response.Trim();

        if (json.StartsWith("```"))
        {
            int firstNewLine = json.IndexOf('\n');

            if (firstNewLine >= 0)
                json = json.Substring(firstNewLine);

            json = json.Replace("```json", "");
            json = json.Replace("```", "");
            json = json.Trim();
        }

        int jsonStart = json.IndexOf('{');
        int jsonEnd = json.LastIndexOf('}');

        if (jsonStart < 0 || jsonEnd < jsonStart)
            return;

        json = json.Substring(
            jsonStart,
            jsonEnd - jsonStart + 1
        );

        try
        {
            ToolCommand command =
                JsonUtility.FromJson<ToolCommand>(json);

            if (command == null)
                return;

            if (string.IsNullOrEmpty(command.tool))
                return;

            ToolArguments args = new ToolArguments();

            args.objectType = command.objectType;
            args.color = command.color;
            args.position = command.position;
            args.rotation = command.rotation;
            args.scale = command.scale;

            unityToolManager.ExecuteTool(
                command.tool,
                args
            );

            llmOutput.text =
                "Created " +
                command.objectType;
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "Could not parse Unity tool command: " +
                e.Message
            );
        }
    }

    [Serializable]
    private class ToolCommand
    {
        public string tool;
        public string objectType;
        public string color;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
    }
}