using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// ============================================================
// DEMO CHAT
// ============================================================
//
// AI LANDSCAPE CONTROLLER
//
// Supports:
//
// 1. Terrain
// 2. Trees
//    - Plant
//    - Move
//    - Remove
//    - Remove all
// 3. Soil
// 4. Crops
// 5. Environment
//
// AI VOICE
// --------
//
// Ollama
//    |
//    v
// JSON command
//    |
//    v
// Unity managers
//    |
//    v
// Natural AI response
//    |
//    v
// AITextToSpeech
//    |
//    v
// Quest 3 speaker
//
// IMPORTANT
// ----------
//
// The LLM decides WHAT the user wants.
//
// Unity managers decide HOW to perform it.
//
// The LLM NEVER directly manipulates Unity objects.
//
// ============================================================

public class DemoChat : MonoBehaviour
{
    // =========================================================
    // OLLAMA
    // =========================================================

    [Header("Ollama")]

    [SerializeField]
    private string ollamaUrl =
        "http://localhost:11434/api/chat";

    [SerializeField]
    private string demoModel =
        "qwen3:4b";

    [SerializeField]
    private float requestTimeout =
        120f;


    // =========================================================
    // MANAGERS
    // =========================================================

    [Header("Managers")]

    [SerializeField]
    private TerrainGenerator terrainGenerator;

    [SerializeField]
    private TreeManager treeManager;

    [SerializeField]
    private SoilManager soilManager;

    [SerializeField]
    private CropManager cropManager;

    [SerializeField]
    private EnvironmentManager environmentManager;


    // =========================================================
    // AI VOICE
    // =========================================================

    [Header("AI Voice")]

    [SerializeField]
    private AITextToSpeech aiTextToSpeech;


    [SerializeField]
    private bool speakAIResponses =
        true;


    // =========================================================
    // TERRAIN DEFAULTS
    // =========================================================

    [Header("Terrain Defaults")]

    [SerializeField]
    private int terrainWidth =
        200;

    [SerializeField]
    private int terrainDepth =
        200;

    [SerializeField]
    private int terrainHeight =
        30;

    [SerializeField]
    private float terrainRoughness =
        0.5f;

    [SerializeField]
    private float terrainDetailScale =
        0.03f;

    [SerializeField]
    private int terrainOctaves =
        4;


    // =========================================================
    // SYSTEM PROMPT
    // =========================================================

    private const string SYSTEM_PROMPT = @"
You are an AI landscape designer controlling a Unity XR environment.

The Unity application contains:

TerrainGenerator
TreeManager
SoilManager
CropManager
EnvironmentManager

The LLM NEVER directly manipulates Unity GameObjects.

The LLM ONLY returns ONE valid JSON command.

============================================================
TERRAIN
============================================================

Supported terrain types:

hills
mountains
valley
island
desert
canyon
flat

Examples:

User:
create hills

Return:
{
  ""type"": ""terrain"",
  ""action"": ""generate"",
  ""terrainType"": ""hills""
}

User:
create mountains

Return:
{
  ""type"": ""terrain"",
  ""action"": ""generate"",
  ""terrainType"": ""mountains""
}

User:
create a valley

Return:
{
  ""type"": ""terrain"",
  ""action"": ""generate"",
  ""terrainType"": ""valley""
}

User:
create an island

Return:
{
  ""type"": ""terrain"",
  ""action"": ""generate"",
  ""terrainType"": ""island""
}

User:
create a desert

Return:
{
  ""type"": ""terrain"",
  ""action"": ""generate"",
  ""terrainType"": ""desert""
}

User:
create a canyon

Return:
{
  ""type"": ""terrain"",
  ""action"": ""generate"",
  ""terrainType"": ""canyon""
}

User:
flatten the terrain

Return:
{
  ""type"": ""terrain"",
  ""action"": ""generate"",
  ""terrainType"": ""flat""
}

Never invent another terrain type.

============================================================
TREES
============================================================

TreeManager supports:

planting
moving
removing
removing all

Known tree types may include:

Oak
Pine
Palm
Birch

Never invent a tree type.

============================================================
PLANT TREES
============================================================

If the user says:

plant two oak trees

return:

{
  ""type"": ""trees"",
  ""action"": ""plant"",
  ""count"": 2,
  ""treeType"": ""Oak"",
  ""radius"": 5,
  ""spacing"": 3
}

If the user says:

plant two pine trees

return:

{
  ""type"": ""trees"",
  ""action"": ""plant"",
  ""count"": 2,
  ""treeType"": ""Pine"",
  ""radius"": 5,
  ""spacing"": 3
}

If no tree type is specified:

{
  ""type"": ""trees"",
  ""action"": ""plant"",
  ""count"": 1,
  ""treeType"": ""Default"",
  ""radius"": 5,
  ""spacing"": 3
}

============================================================
TREE MOVEMENT
============================================================

The user can point at a tree using the XR controller.

The system remembers the selected tree even after the ray moves away.

The system also remembers the most recent terrain position
that the XR ray pointed at.

Therefore:

User:
move this tree over there

Return:

{
  ""type"": ""trees"",
  ""action"": ""move"",
  ""useSelectedTree"": true,
  ""useGroundPosition"": true
}

User:
move this tree there

Return:

{
  ""type"": ""trees"",
  ""action"": ""move"",
  ""useSelectedTree"": true,
  ""useGroundPosition"": true
}

User:
move this tree to that location

Return:

{
  ""type"": ""trees"",
  ""action"": ""move"",
  ""useSelectedTree"": true,
  ""useGroundPosition"": true
}

If the user explicitly provides a tree ID:

User:
move Tree_3 over there

Return:

{
  ""type"": ""trees"",
  ""action"": ""move"",
  ""treeId"": 3,
  ""useSelectedTree"": false,
  ""useGroundPosition"": true
}

User:
move tree 5 over there

Return:

{
  ""type"": ""trees"",
  ""action"": ""move"",
  ""treeId"": 5,
  ""useSelectedTree"": false,
  ""useGroundPosition"": true
}

IMPORTANT:

MOVE IS NOT REMOVE.

If the user says:

move
relocate
put this tree over there
place this tree over there
move this tree
move that tree
move Tree_3
move tree 3

the action MUST be:

""move""

Never interpret movement as removal.

============================================================
TREE MOVEMENT SPATIAL RULES
============================================================

If ""useSelectedTree"" is true:

DemoChat uses the tree selected by TreePointingDetector.

Do NOT invent the tree ID.

If ""useGroundPosition"" is true:

DemoChat uses the most recently recorded terrain position.

Do NOT invent coordinates.

If there is no selected tree:

do not invent a tree.

If there is no destination ground position:

do not invent coordinates.

============================================================
REMOVE TREE
============================================================

If the user explicitly says:

remove this tree

return:

{
  ""type"": ""trees"",
  ""action"": ""remove"",
  ""useSelectedTree"": true
}

If the user says:

remove tree 3

return:

{
  ""type"": ""trees"",
  ""action"": ""remove"",
  ""treeId"": 3,
  ""useSelectedTree"": false
}

============================================================
REMOVE ALL TREES
============================================================

If the user says:

remove all trees

return:

{
  ""type"": ""trees"",
  ""action"": ""remove_all""
}

============================================================
SOIL
============================================================

Soil is NOT automatically created.

If the user says:

create a soil area

return:

{
  ""type"": ""soil"",
  ""action"": ""create"",
  ""width"": 10,
  ""depth"": 10
}

If the user says:

create a large farm

return:

{
  ""type"": ""soil"",
  ""action"": ""create"",
  ""width"": 30,
  ""depth"": 30
}

If the user says:

create a small farm

return:

{
  ""type"": ""soil"",
  ""action"": ""create"",
  ""width"": 10,
  ""depth"": 10
}

If the user says:

remove soil

return:

{
  ""type"": ""soil"",
  ""action"": ""remove"",
  ""count"": 0
}

If the user says:

remove all soil

return:

{
  ""type"": ""soil"",
  ""action"": ""remove_all""
}

Do NOT automatically create soil.

============================================================
CROPS
============================================================

Crops are planted inside existing SoilArea.

The user must create soil before planting crops.

If the user says:

plant five tomatoes

return:

{
  ""type"": ""crops"",
  ""action"": ""plant"",
  ""count"": 5,
  ""cropType"": ""Tomato"",
  ""spacing"": 0.8
}

If the user says:

plant three carrots

return:

{
  ""type"": ""crops"",
  ""action"": ""plant"",
  ""count"": 3,
  ""cropType"": ""Carrot"",
  ""spacing"": 0.8
}

If the user says:

plant ten corn

return:

{
  ""type"": ""crops"",
  ""action"": ""plant"",
  ""count"": 10,
  ""cropType"": ""Corn"",
  ""spacing"": 1
}

If the user says:

plant six potatoes

return:

{
  ""type"": ""crops"",
  ""action"": ""plant"",
  ""count"": 6,
  ""cropType"": ""Potato"",
  ""spacing"": 0.8
}

Do NOT create soil automatically.

============================================================
ENVIRONMENT
============================================================

Supported environment actions:

greener
less_green
brighter
darker
warmer
cooler
stronger_shadows
softer_shadows
add_fog
remove_fog
reset

If the user says:

make it greener

return:

{
  ""type"": ""environment"",
  ""action"": ""greener"",
  ""amount"": 0.2
}

If the user says:

make it less green

return:

{
  ""type"": ""environment"",
  ""action"": ""less_green"",
  ""amount"": 0.2
}

If the user says:

make it brighter

return:

{
  ""type"": ""environment"",
  ""action"": ""brighter"",
  ""amount"": 0.2
}

If the user says:

make it darker

return:

{
  ""type"": ""environment"",
  ""action"": ""darker"",
  ""amount"": 0.2
}

If the user says:

make it warmer

return:

{
  ""type"": ""environment"",
  ""action"": ""warmer"",
  ""amount"": 0.2
}

If the user says:

make it cooler

return:

{
  ""type"": ""environment"",
  ""action"": ""cooler"",
  ""amount"": 0.2
}

If the user says:

make the shadows stronger

return:

{
  ""type"": ""environment"",
  ""action"": ""stronger_shadows""
}

If the user says:

make the shadows softer

return:

{
  ""type"": ""environment"",
  ""action"": ""softer_shadows""
}

If the user says:

add fog

return:

{
  ""type"": ""environment"",
  ""action"": ""add_fog""
}

If the user says:

remove fog

return:

{
  ""type"": ""environment"",
  ""action"": ""remove_fog""
}

If the user says:

reset the environment

return:

{
  ""type"": ""environment"",
  ""action"": ""reset""
}

============================================================
OUTPUT
============================================================

Return ONLY valid JSON.

No markdown.

No code fences.

No explanation.

ONE command per response.
";


    // =========================================================
    // COMMAND
    // =========================================================

    [Serializable]
    private class LandscapeCommand
    {
        public string type;
        public string action;

        // Terrain
        public string terrainType;

        // Trees
        public int count;
        public string treeType;
        public float radius;

        // Tree movement
        public int treeId;
        public bool useSelectedTree;
        public bool useGroundPosition;

        // Crops
        public string cropType;

        // General
        public float spacing;

        // Environment
        public float amount;

        // Soil
        public float width;
        public float depth;

        // Position
        public float x;
        public float z;
    }


    // =========================================================
    // OLLAMA MESSAGE
    // =========================================================

    [Serializable]
    private class OllamaMessage
    {
        public string role;
        public string content;
    }


    // =========================================================
    // OLLAMA REQUEST
    // =========================================================

    [Serializable]
    private class OllamaRequest
    {
        public string model;
        public OllamaMessage[] messages;
        public bool stream;
    }


    // =========================================================
    // OLLAMA RESPONSE
    // =========================================================

    [Serializable]
    private class OllamaResponse
    {
        public OllamaMessage message;
    }


    // =========================================================
    // ASK
    // =========================================================

    public Task Ask(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return Task.CompletedTask;
        }


        TaskCompletionSource<bool> completion =
            new TaskCompletionSource<bool>();


        StartCoroutine(
            SendToOllama(
                userText,
                completion
            )
        );


        return completion.Task;
    }


    // =========================================================
    // SEND TO OLLAMA
    // =========================================================

    private IEnumerator SendToOllama(
        string userText,
        TaskCompletionSource<bool> completion
    )
    {
        Debug.Log(
            "[DemoChat] USER: " +
            userText
        );


        string spatialContext =
            BuildSpatialContext();


        OllamaRequest request =
            new OllamaRequest
            {
                model = demoModel,

                stream = false,

                messages =
                    new OllamaMessage[]
                    {
                        new OllamaMessage
                        {
                            role = "system",
                            content = SYSTEM_PROMPT
                        },

                        new OllamaMessage
                        {
                            role = "user",

                            content =
                                spatialContext +
                                "\n\nUSER REQUEST:\n" +
                                userText
                        }
                    }
            };


        string json =
            JsonUtility.ToJson(
                request
            );


        byte[] body =
            Encoding.UTF8.GetBytes(
                json
            );


        using (
            UnityWebRequest webRequest =
                new UnityWebRequest(
                    ollamaUrl,
                    "POST"
                )
        )
        {
            webRequest.uploadHandler =
                new UploadHandlerRaw(
                    body
                );


            webRequest.downloadHandler =
                new DownloadHandlerBuffer();


            webRequest.SetRequestHeader(
                "Content-Type",
                "application/json"
            );


            webRequest.timeout =
                Mathf.RoundToInt(
                    requestTimeout
                );


            yield return
                webRequest.SendWebRequest();


            // -------------------------------------------------
            // OLLAMA ERROR
            // -------------------------------------------------

            if (
                webRequest.result !=
                UnityWebRequest.Result.Success
            )
            {
                Debug.LogError(
                    "[DemoChat] Ollama error: " +
                    webRequest.error
                );


                SpeakAI(
                    "Sorry, I could not connect to the AI."
                );


                completion.TrySetResult(
                    false
                );


                yield break;
            }


            // -------------------------------------------------
            // RESPONSE
            // -------------------------------------------------

            string responseText =
                webRequest.downloadHandler.text;


            OllamaResponse response;


            try
            {
                response =
                    JsonUtility.FromJson<OllamaResponse>(
                        responseText
                    );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[DemoChat] Ollama JSON response error:\n" +
                    exception
                );


                SpeakAI(
                    "Sorry, I could not understand the AI response."
                );


                completion.TrySetResult(
                    false
                );


                yield break;
            }


            if (
                response == null ||
                response.message == null
            )
            {
                Debug.LogError(
                    "[DemoChat] Invalid Ollama response."
                );


                SpeakAI(
                    "Sorry, I received an invalid AI response."
                );


                completion.TrySetResult(
                    false
                );


                yield break;
            }


            // -------------------------------------------------
            // CLEAN COMMAND
            // -------------------------------------------------

            string commandText =
                CleanJson(
                    response.message.content
                );


            Debug.Log(
                "[DemoChat] Command:\n" +
                commandText
            );


            LandscapeCommand command;


            try
            {
                command =
                    JsonUtility.FromJson<LandscapeCommand>(
                        commandText
                    );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[DemoChat] Command JSON error:\n" +
                    exception
                );


                SpeakAI(
                    "Sorry, I could not understand that command."
                );


                completion.TrySetResult(
                    false
                );


                yield break;
            }


            if (command == null)
            {
                Debug.LogError(
                    "[DemoChat] Command is null."
                );


                SpeakAI(
                    "Sorry, I could not understand that command."
                );


                completion.TrySetResult(
                    false
                );


                yield break;
            }


            // -------------------------------------------------
            // EXECUTE
            // -------------------------------------------------

            bool success =
                ExecuteCommand(
                    command
                );


            // -------------------------------------------------
            // VOICE RESPONSE
            // -------------------------------------------------

            if (success)
            {
                string reply =
                    BuildAIReply(
                        command
                    );


                Debug.Log(
                    "[DemoChat] AI REPLY: " +
                    reply
                );


                SpeakAI(
                    reply
                );
            }
            else
            {
                string failureReply =
                    BuildFailureReply(
                        command
                    );


                SpeakAI(
                    failureReply
                );
            }


            completion.TrySetResult(
                success
            );
        }
    }


    // =========================================================
    // CLEAN JSON
    // =========================================================

    private string CleanJson(
        string text
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }


        text =
            text.Trim();


        text =
            Regex.Replace(
                text,
                @"```json",
                "",
                RegexOptions.IgnoreCase
            );


        text =
            Regex.Replace(
                text,
                @"```",
                "",
                RegexOptions.IgnoreCase
            );


        text =
            Regex.Replace(
                text,
                @"<think>.*?</think>",
                "",
                RegexOptions.Singleline |
                RegexOptions.IgnoreCase
            );


        int start =
            text.IndexOf('{');


        int end =
            text.LastIndexOf('}');


        if (
            start >= 0 &&
            end > start
        )
        {
            text =
                text.Substring(
                    start,
                    end - start + 1
                );
        }


        return text.Trim();
    }


    // =========================================================
    // EXECUTE COMMAND
    // =========================================================

    private bool ExecuteCommand(
        LandscapeCommand command
    )
    {
        if (command == null)
        {
            return false;
        }


        string type =
            string.IsNullOrWhiteSpace(
                command.type
            )
            ? ""
            : command.type.Trim().ToLower();


        string action =
            string.IsNullOrWhiteSpace(
                command.action
            )
            ? ""
            : command.action.Trim().ToLower();


        switch (type)
        {
            case "terrain":

                return ExecuteTerrainCommand(
                    command,
                    action
                );


            case "trees":

                return ExecuteTreeCommand(
                    command,
                    action
                );


            case "soil":

                return ExecuteSoilCommand(
                    command,
                    action
                );


            case "crops":

                return ExecuteCropCommand(
                    command,
                    action
                );


            case "environment":

                return ExecuteEnvironmentCommand(
                    command,
                    action
                );


            default:

                Debug.LogWarning(
                    "[DemoChat] Unknown command type: " +
                    command.type
                );


                return false;
        }
    }


    // =========================================================
    // TERRAIN
    // =========================================================

    private bool ExecuteTerrainCommand(
        LandscapeCommand command,
        string action
    )
    {
        if (terrainGenerator == null)
        {
            Debug.LogError(
                "[DemoChat] TerrainGenerator is not assigned."
            );


            return false;
        }


        if (action != "generate")
        {
            Debug.LogWarning(
                "[DemoChat] Unknown terrain action: " +
                action
            );


            return false;
        }


        string terrainType =
            string.IsNullOrWhiteSpace(
                command.terrainType
            )
            ? "hills"
            : command.terrainType
                .Trim()
                .ToLower();


        switch (terrainType)
        {
            case "hill":

                terrainType =
                    "hills";

                break;


            case "mountain":

                terrainType =
                    "mountains";

                break;


            case "islands":

                terrainType =
                    "island";

                break;
        }


        bool validTerrainType =
            terrainType == "hills" ||
            terrainType == "mountains" ||
            terrainType == "valley" ||
            terrainType == "island" ||
            terrainType == "desert" ||
            terrainType == "canyon" ||
            terrainType == "flat";


        if (!validTerrainType)
        {
            Debug.LogWarning(
                "[DemoChat] Unknown terrain type '" +
                terrainType +
                "'. Using hills."
            );


            terrainType =
                "hills";
        }


        TerrainSettings settings =
            new TerrainSettings();


        settings.width =
            Mathf.Clamp(
                terrainWidth,
                20,
                1000
            );


        settings.depth =
            Mathf.Clamp(
                terrainDepth,
                20,
                1000
            );


        settings.height =
            Mathf.Clamp(
                terrainHeight,
                1,
                500
            );


        settings.terrainType =
            terrainType;


        settings.roughness =
            Mathf.Clamp01(
                terrainRoughness
            );


        settings.detailScale =
            Mathf.Clamp(
                terrainDetailScale,
                0.001f,
                0.2f
            );


        settings.octaves =
            Mathf.Clamp(
                terrainOctaves,
                1,
                8
            );


        settings.seed =
            UnityEngine.Random.Range(
                0,
                999999
            );


        Debug.Log(
            "[DemoChat] Generating terrain: " +
            terrainType
        );


        terrainGenerator.GenerateTerrain(
            settings
        );


        return true;
    }


    // =========================================================
    // TREES
    // =========================================================

    private bool ExecuteTreeCommand(
        LandscapeCommand command,
        string action
    )
    {
        if (treeManager == null)
        {
            Debug.LogError(
                "[DemoChat] TreeManager is not assigned."
            );


            return false;
        }


        // -----------------------------------------------------
        // MOVE
        // -----------------------------------------------------

        if (action == "move")
        {
            return ExecuteTreeMoveCommand(
                command
            );
        }


        // -----------------------------------------------------
        // PLANT
        // -----------------------------------------------------

        if (action == "plant")
        {
            int count =
                Mathf.Clamp(
                    command.count,
                    1,
                    5000
                );


            float radius =
                Mathf.Max(
                    command.radius,
                    1f
                );


            float spacing =
                Mathf.Max(
                    command.spacing,
                    0.5f
                );


            string treeType =
                string.IsNullOrWhiteSpace(
                    command.treeType
                )
                ? "Default"
                : command.treeType.Trim();


            Terrain terrain =
                GetGeneratedTerrain();


            if (terrain == null)
            {
                Debug.LogWarning(
                    "[DemoChat] No generated terrain detected."
                );


                return false;
            }


            float centerX =
                terrain.terrainData.size.x *
                0.5f;


            float centerZ =
                terrain.terrainData.size.z *
                0.5f;


            Debug.Log(
                "[DemoChat] Planting " +
                count +
                " " +
                treeType +
                " trees."
            );


            if (
                treeType.Equals(
                    "Default",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                treeManager.PlantTrees(
                    count,
                    centerX,
                    centerZ,
                    radius,
                    spacing
                );
            }
            else
            {
                treeManager.PlantTrees(
                    count,
                    treeType,
                    centerX,
                    centerZ,
                    radius,
                    spacing
                );
            }


            return true;
        }


        // -----------------------------------------------------
        // REMOVE ALL
        // -----------------------------------------------------

        if (action == "remove_all")
        {
            treeManager.RemoveAllTrees();

            return true;
        }


        // -----------------------------------------------------
        // REMOVE
        // -----------------------------------------------------

        if (action == "remove")
        {
            return ExecuteTreeRemoveCommand(
                command
            );
        }


        Debug.LogWarning(
            "[DemoChat] Unknown tree action: " +
            action
        );


        return false;
    }


    // =========================================================
    // TREE MOVE
    // =========================================================

    private bool ExecuteTreeMoveCommand(
        LandscapeCommand command
    )
    {
        Debug.Log(
            "[DemoChat TREE MOVE] ================================="
        );


        Debug.Log(
            "[DemoChat TREE MOVE] Movement command received."
        );


        int treeId =
            -1;


        // -----------------------------------------------------
        // SELECTED TREE
        // -----------------------------------------------------

        if (command.useSelectedTree)
        {
            if (
                !TryGetSelectedTreeId(
                    out treeId
                )
            )
            {
                Debug.LogWarning(
                    "[DemoChat TREE MOVE] " +
                    "No tree is currently selected. " +
                    "Point at a tree first."
                );


                return false;
            }


            Debug.Log(
                "[DemoChat TREE MOVE] " +
                "Using selected Tree ID = " +
                treeId
            );
        }
        else
        {
            treeId =
                command.treeId;


            Debug.Log(
                "[DemoChat TREE MOVE] " +
                "Using command Tree ID = " +
                treeId
            );
        }


        // -----------------------------------------------------
        // VALIDATE
        // -----------------------------------------------------

        if (treeId < 0)
        {
            Debug.LogWarning(
                "[DemoChat TREE MOVE] " +
                "Invalid tree ID."
            );


            return false;
        }


        // -----------------------------------------------------
        // DESTINATION
        // -----------------------------------------------------

        Vector3 destination;


        if (command.useGroundPosition)
        {
            if (
                !TryGetGroundPosition(
                    out destination
                )
            )
            {
                Debug.LogWarning(
                    "[DemoChat TREE MOVE] " +
                    "No destination ground position detected. " +
                    "Point at the destination first."
                );


                return false;
            }
        }
        else
        {
            Terrain terrain =
                GetGeneratedTerrain();


            if (terrain == null)
            {
                Debug.LogWarning(
                    "[DemoChat TREE MOVE] " +
                    "No terrain found."
                );


                return false;
            }


            destination =
                TerrainLocalToWorld(
                    terrain,
                    command.x,
                    command.z
                );
        }


        Debug.Log(
            "[DemoChat TREE MOVE] " +
            "Moving Tree ID = " +
            treeId +
            " to " +
            destination
        );


        bool success =
            treeManager.MoveTreeToWorldPosition(
                treeId,
                destination
            );


        if (success)
        {
            Debug.Log(
                "[DemoChat TREE MOVE] SUCCESS."
            );
        }
        else
        {
            Debug.LogWarning(
                "[DemoChat TREE MOVE] FAILED."
            );
        }


        return success;
    }


    // =========================================================
    // TREE REMOVE
    // =========================================================

    private bool ExecuteTreeRemoveCommand(
        LandscapeCommand command
    )
    {
        if (command.useSelectedTree)
        {
            int selectedTreeId;


            if (
                !TryGetSelectedTreeId(
                    out selectedTreeId
                )
            )
            {
                Debug.LogWarning(
                    "[DemoChat TREE REMOVE] " +
                    "No tree is currently selected."
                );


                return false;
            }


            Debug.Log(
                "[DemoChat TREE REMOVE] " +
                "Removing selected Tree ID = " +
                selectedTreeId
            );


            treeManager.RemoveTree(
                selectedTreeId
            );


            return true;
        }


        if (command.treeId >= 0)
        {
            Debug.Log(
                "[DemoChat TREE REMOVE] " +
                "Removing Tree ID = " +
                command.treeId
            );


            treeManager.RemoveTree(
                command.treeId
            );


            return true;
        }


        treeManager.RemoveTrees(
            command.x,
            command.z,
            Mathf.Max(
                command.radius,
                1f
            )
        );


        return true;
    }


    // =========================================================
    // SELECTED TREE ID
    // =========================================================

    private bool TryGetSelectedTreeId(
        out int treeId
    )
    {
        treeId =
            -1;


        TreePointingDetector detector =
            FindFirstObjectByType<TreePointingDetector>();


        if (detector == null)
        {
            Debug.LogWarning(
                "[DemoChat] TreePointingDetector not found."
            );


            return false;
        }


        TreeSelectable selectedTree =
            detector.GetSelectedTree();


        if (selectedTree == null)
        {
            return false;
        }


        treeId =
            selectedTree.GetTreeId();


        return treeId >= 0;
    }


    // =========================================================
    // GROUND POSITION
    // =========================================================

    private bool TryGetGroundPosition(
        out Vector3 groundPosition
    )
    {
        groundPosition =
            Vector3.zero;


        TreePointingDetector detector =
            FindFirstObjectByType<TreePointingDetector>();


        if (detector == null)
        {
            Debug.LogWarning(
                "[DemoChat] TreePointingDetector not found."
            );


            return false;
        }


        if (
            !detector.TryGetGroundPosition(
                out groundPosition
            )
        )
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // SOIL
    // =========================================================

    private bool ExecuteSoilCommand(
        LandscapeCommand command,
        string action
    )
    {
        if (soilManager == null)
        {
            Debug.LogError(
                "[DemoChat] SoilManager is not assigned."
            );


            return false;
        }


        if (action == "create")
        {
            Terrain terrain =
                GetGeneratedTerrain();


            if (terrain == null)
            {
                Debug.LogWarning(
                    "[DemoChat] Terrain has not been generated."
                );


                return false;
            }


            float width =
                Mathf.Max(
                    command.width,
                    1f
                );


            float depth =
                Mathf.Max(
                    command.depth,
                    1f
                );


            Vector3 center;


            if (
                Mathf.Abs(command.x) >
                0.001f ||
                Mathf.Abs(command.z) >
                0.001f
            )
            {
                center =
                    new Vector3(
                        terrain.transform.position.x +
                        command.x,

                        terrain.transform.position.y,

                        terrain.transform.position.z +
                        command.z
                    );
            }
            else
            {
                center =
                    terrain.transform.position +
                    new Vector3(
                        terrain.terrainData.size.x *
                        0.5f,

                        0f,

                        terrain.terrainData.size.z *
                        0.5f
                    );
            }


            Debug.Log(
                "[DemoChat] Creating soil area: " +
                width +
                " x " +
                depth
            );


            SoilArea soil =
                soilManager.CreateSoilArea(
                    center,
                    width,
                    depth
                );


            if (soil != null)
            {
                Debug.Log(
                    "[DemoChat] Soil created successfully."
                );


                return true;
            }


            return false;
        }


        if (action == "remove")
        {
            soilManager.RemoveSoil(
                command.count
            );


            return true;
        }


        if (action == "remove_all")
        {
            soilManager.RemoveAllSoil();


            return true;
        }


        Debug.LogWarning(
            "[DemoChat] Unknown soil action: " +
            action
        );


        return false;
    }


    // =========================================================
    // CROPS
    // =========================================================

    private bool ExecuteCropCommand(
        LandscapeCommand command,
        string action
    )
    {
        if (cropManager == null)
        {
            Debug.LogError(
                "[DemoChat] CropManager is not assigned."
            );


            return false;
        }


        if (action == "plant")
        {
            int count =
                Mathf.Clamp(
                    command.count,
                    1,
                    5000
                );


            float spacing =
                command.spacing <= 0f
                ? 0.8f
                : command.spacing;


            string cropType =
                string.IsNullOrWhiteSpace(
                    command.cropType
                )
                ? "Default"
                : command.cropType.Trim();


            Debug.Log(
                "[DemoChat] Planting " +
                count +
                " " +
                cropType +
                " crops."
            );


            int planted =
                cropManager.PlantCrops(
                    count,
                    cropType,
                    spacing
                );


            Debug.Log(
                "[DemoChat] Crop planting result: " +
                planted +
                " planted."
            );


            return planted > 0;
        }


        if (action == "remove")
        {
            cropManager.RemoveCrop(
                command.count
            );


            return true;
        }


        if (action == "remove_all")
        {
            cropManager.RemoveAllCrops();


            return true;
        }


        Debug.LogWarning(
            "[DemoChat] Unknown crop action: " +
            action
        );


        return false;
    }


    // =========================================================
    // ENVIRONMENT
    // =========================================================

    private bool ExecuteEnvironmentCommand(
        LandscapeCommand command,
        string action
    )
    {
        if (environmentManager == null)
        {
            Debug.LogError(
                "[DemoChat] EnvironmentManager is not assigned."
            );


            return false;
        }


        bool success =
            environmentManager.ApplyEnvironmentCommand(
                action,
                command.amount
            );


        if (success)
        {
            Debug.Log(
                "[DemoChat] Environment command executed: " +
                action +
                " | amount = " +
                command.amount
            );
        }
        else
        {
            Debug.LogWarning(
                "[DemoChat] Environment command failed: " +
                action
            );
        }


        return success;
    }


    // =========================================================
    // GENERATED TERRAIN
    // =========================================================

    private Terrain GetGeneratedTerrain()
    {
        if (terrainGenerator != null)
        {
            try
            {
                Terrain generated =
                    terrainGenerator.GetGeneratedTerrain();


                if (
                    generated != null &&
                    generated.terrainData != null
                )
                {
                    return generated;
                }
            }
            catch
            {
                // Continue to fallback.
            }
        }


        Terrain active =
            Terrain.activeTerrain;


        if (
            active != null &&
            active.terrainData != null
        )
        {
            return active;
        }


        return null;
    }


    // =========================================================
    // TERRAIN LOCAL -> WORLD
    // =========================================================

    private Vector3 TerrainLocalToWorld(
        Terrain currentTerrain,
        float localX,
        float localZ
    )
    {
        if (currentTerrain == null)
        {
            return Vector3.zero;
        }


        TerrainData data =
            currentTerrain.terrainData;


        if (data == null)
        {
            return Vector3.zero;
        }


        localX =
            Mathf.Clamp(
                localX,
                0f,
                data.size.x
            );


        localZ =
            Mathf.Clamp(
                localZ,
                0f,
                data.size.z
            );


        float worldX =
            currentTerrain.transform.position.x +
            localX;


        float worldZ =
            currentTerrain.transform.position.z +
            localZ;


        float terrainHeight =
            currentTerrain.SampleHeight(
                new Vector3(
                    worldX,
                    currentTerrain.transform.position.y,
                    worldZ
                )
            );


        return new Vector3(
            worldX,

            currentTerrain.transform.position.y +
            terrainHeight,

            worldZ
        );
    }


    // =========================================================
    // SPATIAL CONTEXT
    // =========================================================

    private string BuildSpatialContext()
    {
        StringBuilder context =
            new StringBuilder();


        context.AppendLine(
            "SPATIAL CONTEXT:"
        );


        // =====================================================
        // TERRAIN
        // =====================================================

        Terrain terrain =
            GetGeneratedTerrain();


        if (terrain != null)
        {
            context.AppendLine(
                "Generated terrain exists."
            );


            context.AppendLine(
                "Terrain name: " +
                terrain.name
            );


            if (terrain.terrainData != null)
            {
                context.AppendLine(
                    "Terrain size: " +
                    terrain.terrainData.size
                );
            }
        }
        else
        {
            context.AppendLine(
                "Generated terrain does not exist yet."
            );
        }


        // =====================================================
        // SOIL
        // =====================================================

        if (soilManager != null)
        {
            context.AppendLine(
                "Soil areas: " +
                soilManager.GetSoilCount()
            );
        }


        // =====================================================
        // CROPS
        // =====================================================

        if (cropManager != null)
        {
            context.AppendLine(
                "Crops: " +
                cropManager.GetCropCount()
            );
        }


        // =====================================================
        // TREES
        // =====================================================

        if (treeManager != null)
        {
            context.AppendLine(
                "Trees: " +
                treeManager.GetTreeCount()
            );
        }


        // =====================================================
        // SELECTED TREE
        // =====================================================

        TreePointingDetector detector =
            FindFirstObjectByType<TreePointingDetector>();


        if (detector != null)
        {
            TreeSelectable selectedTree =
                detector.GetSelectedTree();


            if (selectedTree != null)
            {
                int selectedTreeId =
                    selectedTree.GetTreeId();


                context.AppendLine(
                    "SELECTED TREE EXISTS."
                );


                context.AppendLine(
                    "Selected tree ID: " +
                    selectedTreeId
                );


                context.AppendLine(
                    "Selected tree name: " +
                    selectedTree.GetTreeName()
                );


                if (treeManager != null)
                {
                    GameObject selectedObject =
                        treeManager.GetTreeById(
                            selectedTreeId
                        );


                    if (selectedObject != null)
                    {
                        string treeType =
                            treeManager.GetTreeType(
                                selectedTreeId
                            );


                        context.AppendLine(
                            "Selected tree type: " +
                            treeType
                        );
                    }
                }
            }
            else
            {
                context.AppendLine(
                    "No tree is currently selected."
                );
            }
        }
        else
        {
            context.AppendLine(
                "TreePointingDetector is not available."
            );


            context.AppendLine(
                "No tree is currently selected."
            );
        }


        // =====================================================
        // GROUND POSITION
        // =====================================================

        Vector3 groundPosition;


        if (
            TryGetGroundPosition(
                out groundPosition
            )
        )
        {
            context.AppendLine(
                "GROUND DESTINATION EXISTS."
            );


            context.AppendLine(
                "Pointed ground world position: " +
                groundPosition
            );
        }
        else
        {
            context.AppendLine(
                "No pointed ground destination exists."
            );
        }


        // =====================================================
        // ENVIRONMENT
        // =====================================================

        if (environmentManager != null)
        {
            context.AppendLine(
                "EnvironmentManager is available."
            );


            context.AppendLine(
                "Current green amount: " +
                environmentManager.GetGreenAmount()
            );


            context.AppendLine(
                "Current brightness: " +
                environmentManager.GetBrightness()
            );


            context.AppendLine(
                "Current warmth: " +
                environmentManager.GetWarmth()
            );
        }
        else
        {
            context.AppendLine(
                "EnvironmentManager is not available."
            );
        }


        return context.ToString();
    }


    // =========================================================
    // AI VOICE
    // =========================================================

    private void SpeakAI(
        string text
    )
    {
        if (!speakAIResponses)
        {
            return;
        }


        if (aiTextToSpeech == null)
        {
            Debug.LogWarning(
                "[DemoChat] AITextToSpeech is not assigned."
            );


            return;
        }


        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }


        aiTextToSpeech.Speak(
            text
        );
    }


    // =========================================================
    // BUILD AI REPLY
    // =========================================================

    private string BuildAIReply(
        LandscapeCommand command
    )
    {
        if (command == null)
        {
            return "Done.";
        }


        string type =
            string.IsNullOrWhiteSpace(
                command.type
            )
            ? ""
            : command.type.Trim().ToLower();


        string action =
            string.IsNullOrWhiteSpace(
                command.action
            )
            ? ""
            : command.action.Trim().ToLower();


        // -----------------------------------------------------
        // TERRAIN
        // -----------------------------------------------------

        if (type == "terrain")
        {
            string terrain =
                string.IsNullOrWhiteSpace(
                    command.terrainType
                )
                ? "terrain"
                : command.terrainType;


            if (action == "generate")
            {
                return
                    "I've created the " +
                    terrain +
                    " terrain for you.";
            }
        }


        // -----------------------------------------------------
        // TREES
        // -----------------------------------------------------

        if (type == "trees")
        {
            if (action == "plant")
            {
                int count =
                    Mathf.Max(
                        command.count,
                        1
                    );


                string treeType =
                    string.IsNullOrWhiteSpace(
                        command.treeType
                    )
                    ? "trees"
                    : command.treeType;


                string plural =
                    count == 1
                    ? treeType + " tree"
                    : treeType + " trees";


                return
                    "I've planted " +
                    count +
                    " " +
                    plural +
                    ".";
            }


            if (action == "move")
            {
                return
                    "I've moved the selected tree to the new location.";
            }


            if (action == "remove")
            {
                return
                    "I've removed the tree.";
            }


            if (action == "remove_all")
            {
                return
                    "I've removed all the trees.";
            }
        }


        // -----------------------------------------------------
        // SOIL
        // -----------------------------------------------------

        if (type == "soil")
        {
            if (action == "create")
            {
                return
                    "I've created the soil area for you.";
            }


            if (action == "remove")
            {
                return
                    "I've removed the soil area.";
            }


            if (action == "remove_all")
            {
                return
                    "I've removed all the soil areas.";
            }
        }


        // -----------------------------------------------------
        // CROPS
        // -----------------------------------------------------

        if (type == "crops")
        {
            if (action == "plant")
            {
                int count =
                    Mathf.Max(
                        command.count,
                        1
                    );


                string cropType =
                    string.IsNullOrWhiteSpace(
                        command.cropType
                    )
                    ? "crops"
                    : command.cropType;


                return
                    "I've planted " +
                    count +
                    " " +
                    cropType +
                    " crops.";
            }


            if (action == "remove")
            {
                return
                    "I've removed the crop.";
            }


            if (action == "remove_all")
            {
                return
                    "I've removed all the crops.";
            }
        }


        // -----------------------------------------------------
        // ENVIRONMENT
        // -----------------------------------------------------

        if (type == "environment")
        {
            switch (action)
            {
                case "greener":

                    return
                        "I've made the environment greener.";


                case "less_green":

                    return
                        "I've reduced the greenery.";


                case "brighter":

                    return
                        "I've made the environment brighter.";


                case "darker":

                    return
                        "I've made the environment darker.";


                case "warmer":

                    return
                        "I've made the environment warmer.";


                case "cooler":

                    return
                        "I've made the environment cooler.";


                case "stronger_shadows":

                    return
                        "I've made the shadows stronger.";


                case "softer_shadows":

                    return
                        "I've made the shadows softer.";


                case "add_fog":

                    return
                        "I've added fog to the environment.";


                case "remove_fog":

                    return
                        "I've removed the fog.";


                case "reset":

                    return
                        "I've reset the environment.";
            }
        }


        return "Done.";
    }


    // =========================================================
    // FAILURE REPLY
    // =========================================================

    private string BuildFailureReply(
        LandscapeCommand command
    )
    {
        if (command == null)
        {
            return
                "I could not complete that request.";
        }


        string type =
            string.IsNullOrWhiteSpace(
                command.type
            )
            ? ""
            : command.type.Trim().ToLower();


        string action =
            string.IsNullOrWhiteSpace(
                command.action
            )
            ? ""
            : command.action.Trim().ToLower();


        if (
            type == "trees" &&
            action == "move"
        )
        {
            return
                "I couldn't move the tree. Please select a tree and point at the destination.";
        }


        if (
            type == "crops" &&
            action == "plant"
        )
        {
            return
                "I couldn't plant the crops. Please make sure a soil area exists first.";
        }


        if (
            type == "terrain"
        )
        {
            return
                "I couldn't generate the terrain.";
        }


        return
            "I couldn't complete that request.";
    }
}