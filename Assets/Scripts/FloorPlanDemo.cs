using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sends a 2D architectural floor plan to Ollama/Qwen3-VL
/// and converts the returned wall coordinates into Unity walls.
///
/// Editor:
///     http://localhost:11434
///
/// Meta Quest:
///     http://YOUR_PC_LAN_IP:11434
/// </summary>
public class FloorPlanDemo : MonoBehaviour
{
    [Header("Floor Plan")]
    [Tooltip("Drag your floor-plan PNG/JPG here.")]
    [SerializeField]
    private Texture2D floorPlan;


    [Header("Ollama")]
    [SerializeField]
    private string ollamaUrl =
        "http://localhost:11434/api/chat";

    [SerializeField]
    private string model =
        "qwen3-vl:8b";


    [Header("Unity")]
    [SerializeField]
    private UnityToolManager unityToolManager;


    [Header("Optional UI")]
    [SerializeField]
    private TMP_Text outputText;

    [SerializeField]
    private Button generateButton;


    [Header("House Settings")]
    [Tooltip("Default wall height in metres.")]
    [SerializeField]
    private float defaultWallHeight = 2.7f;

    [Tooltip("Default wall thickness in metres.")]
    [SerializeField]
    private float defaultWallThickness = 0.15f;


    [Tooltip(
        "Generated walls will be placed under this Transform."
    )]
    [SerializeField]
    private Transform houseParent;


    private bool isGenerating;

    public bool IsGenerating
    {
        get { return isGenerating; }
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (generateButton != null)
        {
            generateButton.onClick.AddListener(
                GenerateHouse
            );
        }
    }


    // =========================================================
    // DESTROY
    // =========================================================

    private void OnDestroy()
    {
        if (generateButton != null)
        {
            generateButton.onClick.RemoveListener(
                GenerateHouse
            );
        }
    }


    // =========================================================
    // GENERATE
    // =========================================================

    public void GenerateHouse()
    {
        if (isGenerating)
        {
            Debug.LogWarning(
                "House generation is already running."
            );

            return;
        }


        if (floorPlan == null)
        {
            Debug.LogError(
                "Floor plan image is not assigned."
            );

            SetOutput(
                "ERROR: Floor plan image is not assigned."
            );

            return;
        }


        if (unityToolManager == null)
        {
            Debug.LogError(
                "UnityToolManager is not assigned."
            );

            SetOutput(
                "ERROR: UnityToolManager is not assigned."
            );

            return;
        }


        StartCoroutine(
            AnalyzeFloorPlan()
        );
    }


    // =========================================================
    // ANALYZE FLOOR PLAN
    // =========================================================

    private IEnumerator AnalyzeFloorPlan()
    {
        isGenerating = true;


        SetOutput(
            "Analyzing floor plan with Qwen3-VL..."
        );


        byte[] imageBytes;


        try
        {
            imageBytes =
                floorPlan.EncodeToJPG(90);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "Could not encode floor plan: " +
                e.Message
            );

            SetOutput(
                "ERROR: Could not encode floor plan."
            );

            isGenerating = false;

            yield break;
        }


        string base64Image =
            Convert.ToBase64String(
                imageBytes
            );


        string prompt =
            BuildFloorPlanPrompt();


        OllamaChatRequest request =
            new OllamaChatRequest();


        request.model =
            model;


        request.stream =
            false;


        request.messages =
            new List<OllamaMessage>();


        OllamaMessage message =
            new OllamaMessage();


        message.role =
            "user";


        message.content =
            prompt;


        message.images =
            new List<string>();


        message.images.Add(
            base64Image
        );


        request.messages.Add(
            message
        );


        string jsonRequest;


        try
        {
            jsonRequest =
                JsonUtility.ToJson(
                    request
                );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "Could not create Ollama request: " +
                e.Message
            );

            SetOutput(
                "ERROR: Could not create Ollama request."
            );

            isGenerating = false;

            yield break;
        }


        using (
            UnityWebRequest requestWeb =
            new UnityWebRequest(
                ollamaUrl,
                "POST"
            )
        )
        {
            byte[] bodyRaw =
                Encoding.UTF8.GetBytes(
                    jsonRequest
                );


            requestWeb.uploadHandler =
                new UploadHandlerRaw(
                    bodyRaw
                );


            requestWeb.downloadHandler =
                new DownloadHandlerBuffer();


            requestWeb.SetRequestHeader(
                "Content-Type",
                "application/json"
            );


            Debug.Log(
                "Sending floor plan to Ollama..."
            );


            Debug.Log(
                "Model: " +
                model
            );


            Debug.Log(
                "URL: " +
                ollamaUrl
            );


            yield return requestWeb.SendWebRequest();


            if (
                requestWeb.result !=
                UnityWebRequest.Result.Success
            )
            {
                Debug.LogError(
                    "Ollama request failed: " +
                    requestWeb.error
                );


                Debug.LogError(
                    "Response: " +
                    requestWeb.downloadHandler.text
                );


                SetOutput(
                    "Ollama connection failed:\n" +
                    requestWeb.error
                );


                isGenerating = false;

                yield break;
            }


            string responseText =
                requestWeb.downloadHandler.text;


            Debug.Log(
                "Ollama response:\n" +
                responseText
            );


            OllamaChatResponse response;


            try
            {
                response =
                    JsonUtility.FromJson<OllamaChatResponse>(
                        responseText
                    );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "Could not parse Ollama response: " +
                    e.Message
                );


                SetOutput(
                    "ERROR: Could not parse Ollama response."
                );


                isGenerating = false;

                yield break;
            }


            if (
                response == null ||
                response.message == null
            )
            {
                Debug.LogError(
                    "Ollama returned an empty response."
                );


                SetOutput(
                    "ERROR: Ollama returned no message."
                );


                isGenerating = false;

                yield break;
            }


            string modelResponse =
                response.message.content;


            Debug.Log(
                "Qwen3-VL result:\n" +
                modelResponse
            );


            SetOutput(
                "Qwen3-VL analyzed the floor plan.\n" +
                "Generating walls..."
            );


            GenerateWallsFromResponse(
                modelResponse
            );
        }


        isGenerating = false;
    }


    // =========================================================
    // PROMPT
    // =========================================================

    private string BuildFloorPlanPrompt()
    {
        return @"
You are analyzing an architectural house floor plan.

Your task is to identify the WALLS in the floor plan.

Do NOT create a 3D mesh.

Return wall coordinates so Unity can construct the walls.

IMPORTANT:
Return ONLY valid JSON.
Do NOT use markdown.
Do NOT write explanations.
Do NOT write code fences.

Coordinate system:

X = left/right direction.
Z = front/back direction.
Y is vertical and must not be used.

The bottom-left corner of the house footprint
is approximately X=0, Z=0.

Measurements should be interpreted as feet/inches
and converted to metres.

1 foot = 0.3048 metres.

Default wall height = 2.7 metres.
Default wall thickness = 0.15 metres.

Identify:

1. Exterior walls.
2. Interior walls.
3. Garage walls.
4. Bathroom walls.
5. Kitchen walls.
6. Bedroom walls.
7. Great room walls.
8. Foyer walls.
9. Mud room walls.
10. WIC walls.

Do NOT create:

- furniture
- floors
- roofs
- ceilings
- doors
- windows

For every wall provide:

- name
- start
- end
- height
- thickness

Example:

{
    ""walls"": [
        {
            ""name"": ""Exterior Wall"",
            ""start"": {
                ""x"": 0,
                ""z"": 0
            },
            ""end"": {
                ""x"": 10,
                ""z"": 0
            },
            ""height"": 2.7,
            ""thickness"": 0.15
        }
    ]
}

The start and end coordinates represent
the CENTER LINE of the wall.

Use long continuous wall segments when possible.

Use dimensions printed on the floor plan
when clearly readable.

If exact dimensions cannot be determined,
estimate using the scale and neighboring dimensions.

The uploaded image is the source of truth.

Return ONLY the JSON object.
";
    }


    // =========================================================
    // GENERATE WALLS
    // =========================================================

    private void GenerateWallsFromResponse(
        string response
    )
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            Debug.LogError(
                "Empty Qwen3-VL response."
            );

            SetOutput(
                "ERROR: Empty AI response."
            );

            return;
        }


        string json =
            ExtractJson(response);


        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError(
                "No JSON object found."
            );


            Debug.LogError(
                response
            );


            SetOutput(
                "ERROR: Qwen3-VL did not return valid JSON."
            );


            return;
        }


        Debug.Log(
            "Extracted wall JSON:\n" +
            json
        );


        HouseLayout layout;


        try
        {
            layout =
                JsonUtility.FromJson<HouseLayout>(
                    json
                );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "JSON parsing failed: " +
                e.Message
            );


            SetOutput(
                "ERROR: Wall JSON could not be parsed."
            );


            return;
        }


        if (
            layout == null ||
            layout.walls == null ||
            layout.walls.Count == 0
        )
        {
            Debug.LogError(
                "No walls were returned."
            );


            SetOutput(
                "ERROR: Qwen3-VL returned no walls."
            );


            return;
        }


        Debug.Log(
            "Received " +
            layout.walls.Count +
            " walls."
        );


        unityToolManager.ClearGeneratedHouse();


        int created = 0;


        foreach (
            WallData wall
            in layout.walls
        )
        {
            if (wall == null)
                continue;


            float height =
                wall.height > 0
                    ? wall.height
                    : defaultWallHeight;


            float thickness =
                wall.thickness > 0
                    ? wall.thickness
                    : defaultWallThickness;


            bool success =
                unityToolManager.CreateWall(
                    wall.name,
                    wall.start,
                    wall.end,
                    height,
                    thickness,
                    houseParent
                );


            if (success)
            {
                created++;
            }
        }


        SetOutput(
            "House generated.\n" +
            "Walls created: " +
            created
        );


        Debug.Log(
            "House generation finished. " +
            created +
            " walls created."
        );
    }


    // =========================================================
    // EXTRACT JSON
    // =========================================================

    private string ExtractJson(
        string response
    )
    {
        string json =
            response.Trim();


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


            int fenceIndex =
                json.LastIndexOf(
                    "```"
                );


            if (fenceIndex >= 0)
            {
                json =
                    json.Substring(
                        0,
                        fenceIndex
                    );
            }


            json =
                json.Trim();
        }


        int start =
            json.IndexOf('{');


        int end =
            json.LastIndexOf('}');


        if (
            start < 0 ||
            end < start
        )
        {
            return null;
        }


        return json.Substring(
            start,
            end - start + 1
        );
    }


    // =========================================================
    // UI
    // =========================================================

    private void SetOutput(
        string message
    )
    {
        if (outputText != null)
        {
            outputText.text =
                message;
        }
    }


    // =========================================================
    // OLLAMA JSON
    // =========================================================

    [Serializable]
    private class OllamaChatRequest
    {
        public string model;
        public List<OllamaMessage> messages;
        public bool stream;
    }


    [Serializable]
    private class OllamaMessage
    {
        public string role;
        public string content;
        public List<string> images;
    }


    [Serializable]
    private class OllamaChatResponse
    {
        public string model;
        public OllamaResponseMessage message;
        public bool done;
    }


    [Serializable]
    private class OllamaResponseMessage
    {
        public string role;
        public string content;
    }
}