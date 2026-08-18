using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// ============================================================
// DEMO CHAT
// ============================================================
//
// SYSTEMS
// --------
// 1. TERRAIN
// 2. TREES
// 3. CROPS
// 4. ENVIRONMENT
//
// IMPORTANT
// ----------
// The LLM NEVER directly manipulates Unity objects.
//
// Pipeline:
//
// Voice / Text
//      ↓
// Transcript validation
//      ↓
// Ollama / Qwen
//      ↓
// ONE JSON command
//      ↓
// Command validation
//      ↓
// Confidence check
//      ↓
// ┌───────────────┬────────────────┬────────────────┐
// │ HIGH >= 0.80  │ MEDIUM 0.50-79 │ LOW < 0.50     │
// │ Execute       │ Confirm        │ Clarify        │
// └───────────────┴────────────────┴────────────────┘
//      ↓
// Unity Manager
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


    // =========================================================
    // UI
    // =========================================================

    [Header("UI")]

    [SerializeField]
    private TMP_InputField inputField;

    [SerializeField]
    private TMP_Text outputText;

    [SerializeField]
    private UnityEngine.UI.Button sendButton;


    // =========================================================
    // TERRAIN
    // =========================================================

    [Header("Terrain")]

    [SerializeField]
    private TerrainGenerator terrainGenerator;


    // =========================================================
    // TREES
    // =========================================================

    [Header("Trees")]

    [SerializeField]
    private TreeManager treeManager;


    // =========================================================
    // XR TREE POINTING
    // =========================================================

    [Header("XR Tree Pointing")]

    [SerializeField]
    private TreePointingDetector treePointingDetector;


    // =========================================================
    // CROPS
    // =========================================================

    [Header("Crops")]

    [SerializeField]
    private CropManager cropManager;


    // =========================================================
    // ENVIRONMENT
    // =========================================================

    [Header("Environment")]

    [SerializeField]
    private EnvironmentManager environmentManager;


    // =========================================================
    // CONFIDENCE
    // =========================================================

    [Header("AI Confidence")]

    [SerializeField]
    private float highConfidenceThreshold =
        0.80f;

    [SerializeField]
    private float mediumConfidenceThreshold =
        0.50f;


    // =========================================================
    // STATE
    // =========================================================

    private bool isGenerating = false;


    // =========================================================
    // PENDING CONFIRMATION
    // =========================================================

    private bool hasPendingConfirmation = false;

    private LandscapeCommand pendingCommand = null;

    private SpatialContext pendingSpatialContext = null;


    // =========================================================
    // YIELD TRACKING
    // =========================================================

    private readonly Dictionary<string, int>
        harvestedYield =
        new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase
        );


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool IsGenerating
    {
        get
        {
            return isGenerating;
        }
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(
                SendCurrentPrompt
            );
        }

        if (outputText != null)
        {
            outputText.text =
                "Describe the landscape, trees, crops, or environment you want.";
        }
    }


    // =========================================================
    // SEND BUTTON
    // =========================================================

    public void SendCurrentPrompt()
    {
        if (inputField == null)
        {
            Debug.LogError(
                "[DemoChat] InputField is not assigned."
            );

            return;
        }

        string prompt =
            inputField.text.Trim();

        if (string.IsNullOrWhiteSpace(prompt))
        {
            SetOutput(
                "Please describe what you want."
            );

            return;
        }

        SendPrompt(prompt);
    }


    // =========================================================
    // PUBLIC PROMPT
    // =========================================================

    public void SendPrompt(
        string userPrompt
    )
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            Debug.LogWarning(
                "[DemoChat] Empty prompt ignored."
            );

            return;
        }

        userPrompt =
            userPrompt.Trim();

        // -----------------------------------------------------
        // CONFIRMATION RESPONSE
        // -----------------------------------------------------

        if (hasPendingConfirmation)
        {
            if (IsConfirmationYes(userPrompt))
            {
                ConfirmPendingCommand();

                return;
            }

            if (IsConfirmationNo(userPrompt))
            {
                CancelPendingCommand();

                return;
            }

            // -------------------------------------------------
            // Treat anything else as a new request.
            // -------------------------------------------------

            ClearPendingConfirmation();
        }

        // -----------------------------------------------------
        // TRANSCRIPT VALIDATION
        // -----------------------------------------------------

        if (!IsUsableTranscript(userPrompt))
        {
            SetOutput(
                "I could not clearly understand the request. " +
                "Please try saying it again."
            );

            return;
        }

        SpatialContext spatialContext =
            CaptureSpatialContext();

        StartCoroutine(
            SendToOllama(
                userPrompt,
                spatialContext,
                null
            )
        );
    }


    // =========================================================
    // ASYNC ASK
    // =========================================================

    public async Task Ask(
        string userPrompt
    )
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            Debug.LogWarning(
                "[DemoChat] Ask() received empty prompt."
            );

            return;
        }

        userPrompt =
            userPrompt.Trim();

        if (!IsUsableTranscript(userPrompt))
        {
            SetOutput(
                "I could not clearly understand the request."
            );

            return;
        }

        while (isGenerating)
        {
            await Task.Yield();
        }

        SpatialContext spatialContext =
            CaptureSpatialContext();

        TaskCompletionSource<bool>
            completionSource =
            new TaskCompletionSource<bool>();

        StartCoroutine(
            SendToOllama(
                userPrompt,
                spatialContext,
                completionSource
            )
        );

        await completionSource.Task;
    }


    // =========================================================
    // TRANSCRIPT VALIDATION
    // =========================================================

    private bool IsUsableTranscript(
        string transcript
    )
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return false;
        }

        string text =
            transcript.Trim();

        if (text.Length < 2)
        {
            return false;
        }

        string withoutPunctuation =
            Regex.Replace(
                text,
                @"[\p{P}\p{S}\d]",
                ""
            );

        if (
            string.IsNullOrWhiteSpace(
                withoutPunctuation
            )
        )
        {
            return false;
        }

        if (
            text.Length >= 5 &&
            Regex.IsMatch(
                text,
                @"^(.)\1{4,}$"
            )
        )
        {
            return false;
        }

        return true;
    }


    // =========================================================
    // CONFIRMATION
    // =========================================================

    private bool IsConfirmationYes(
        string text
    )
    {
        string normalized =
            text.Trim().ToLowerInvariant();

        return
            normalized == "yes" ||
            normalized == "yeah" ||
            normalized == "yep" ||
            normalized == "sure" ||
            normalized == "okay" ||
            normalized == "ok" ||
            normalized == "confirm" ||
            normalized == "do it" ||
            normalized == "go ahead" ||
            normalized == "that's right" ||
            normalized == "correct";
    }


    private bool IsConfirmationNo(
        string text
    )
    {
        string normalized =
            text.Trim().ToLowerInvariant();

        return
            normalized == "no" ||
            normalized == "nope" ||
            normalized == "cancel" ||
            normalized == "stop" ||
            normalized == "don't" ||
            normalized == "do not";
    }


    // =========================================================
    // CONFIRM PENDING COMMAND
    // =========================================================

    private void ConfirmPendingCommand()
    {
        if (
            !hasPendingConfirmation ||
            pendingCommand == null
        )
        {
            SetOutput(
                "There is nothing waiting for confirmation."
            );

            return;
        }

        LandscapeCommand command =
            pendingCommand;

        SpatialContext spatialContext =
            pendingSpatialContext;

        ClearPendingConfirmation();

        SetOutput(
            "Confirmed. Executing the request..."
        );

        bool success =
            ExecuteValidatedCommand(
                command,
                spatialContext
            );

        if (!success)
        {
            Debug.LogWarning(
                "[DemoChat] Confirmed command failed."
            );
        }
    }


    // =========================================================
    // CANCEL PENDING COMMAND
    // =========================================================

    private void CancelPendingCommand()
    {
        ClearPendingConfirmation();

        SetOutput(
            "Okay. I cancelled that action."
        );
    }


    // =========================================================
    // CLEAR PENDING
    // =========================================================

    private void ClearPendingConfirmation()
    {
        hasPendingConfirmation =
            false;

        pendingCommand =
            null;

        pendingSpatialContext =
            null;
    }


    // =========================================================
    // SPATIAL CONTEXT
    // =========================================================

    private SpatialContext CaptureSpatialContext()
    {
        SpatialContext context =
            new SpatialContext();

        if (treePointingDetector == null)
        {
            return context;
        }

        // -----------------------------------------------------
        // SELECTED TREE
        // -----------------------------------------------------

        TreeSelectable selectedTree =
            treePointingDetector.GetSelectedTree();

        if (selectedTree != null)
        {
            context.hasSelectedTree = true;

            context.selectedTreeIndex =
                selectedTree.treeIndex;

            context.selectedTreeName =
                selectedTree.GetTreeName();

            TreeTypeInfo typeInfo =
                selectedTree.GetComponent<TreeTypeInfo>();

            if (typeInfo != null)
            {
                context.selectedTreeType =
                    typeInfo.GetTreeType();
            }
            else
            {
                context.selectedTreeType =
                    "Unknown";
            }
        }

        // -----------------------------------------------------
        // GROUND POSITION
        // -----------------------------------------------------

        Vector3 groundPosition;

        if (
            treePointingDetector.TryGetGroundPosition(
                out groundPosition
            )
        )
        {
            context.hasGroundPosition = true;

            context.groundPosition =
                groundPosition;
        }

        return context;
    }


    // =========================================================
    // SPATIAL CONTEXT TEXT
    // =========================================================

    private string BuildSpatialContext(
        SpatialContext context
    )
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "XR SPATIAL CONTEXT:"
        );

        builder.AppendLine(
            "This information comes directly from Unity."
        );

        builder.AppendLine();

        // -----------------------------------------------------
        // TREE
        // -----------------------------------------------------

        if (context.hasSelectedTree)
        {
            builder.AppendLine(
                "Selected tree: " +
                context.selectedTreeName
            );

            builder.AppendLine(
                "Selected tree index: " +
                context.selectedTreeIndex
            );

            builder.AppendLine(
                "Selected tree type: " +
                context.selectedTreeType
            );
        }
        else
        {
            builder.AppendLine(
                "Selected tree: none"
            );
        }

        // -----------------------------------------------------
        // GROUND
        // -----------------------------------------------------

        if (context.hasGroundPosition)
        {
            Vector3 p =
                context.groundPosition;

            builder.AppendLine(
                "Pointing at terrain: true"
            );

            builder.AppendLine(
                "Ground position X: " +
                p.x.ToString("F3")
            );

            builder.AppendLine(
                "Ground position Y: " +
                p.y.ToString("F3")
            );

            builder.AppendLine(
                "Ground position Z: " +
                p.z.ToString("F3")
            );
        }
        else
        {
            builder.AppendLine(
                "Pointing at terrain: false"
            );
        }

        builder.AppendLine();

        builder.AppendLine(
            "SPATIAL REFERENCE RULES:"
        );

        builder.AppendLine(
            "Unity provides the selected tree."
        );

        builder.AppendLine(
            "Unity provides the tree ID."
        );

        builder.AppendLine(
            "Unity provides the pointed ground position."
        );

        builder.AppendLine(
            "Unity controls physical placement."
        );

        builder.AppendLine();

        builder.AppendLine(
            "The AI MUST NOT invent tree IDs."
        );

        builder.AppendLine(
            "The AI MUST NOT invent exact target coordinates."
        );

        builder.AppendLine(
            "For move_tree, use the Unity spatial context."
        );

        return builder.ToString();
    }


    // =========================================================
    // OLLAMA
    // =========================================================

    private IEnumerator SendToOllama(
        string userPrompt,
        SpatialContext spatialContext,
        TaskCompletionSource<bool>
            completionSource
    )
    {
        if (isGenerating)
        {
            while (isGenerating)
            {
                yield return null;
            }
        }

        isGenerating =
            true;

        SetButtonInteractable(
            false
        );

        SetOutput(
            "AI is thinking..."
        );

        string systemPrompt =
            BuildSystemPrompt();

        string spatialText =
            BuildSpatialContext(
                spatialContext
            );

        string userMessage =
            "USER REQUEST:\n" +
            userPrompt +
            "\n\n" +
            spatialText;

        OllamaRequest request =
            new OllamaRequest();

        request.model =
            demoModel;

        request.stream =
            false;

        request.messages =
            new OllamaMessage[]
            {
                new OllamaMessage
                {
                    role = "system",
                    content = systemPrompt
                },

                new OllamaMessage
                {
                    role = "user",
                    content = userMessage
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

            yield return webRequest.SendWebRequest();

            // -------------------------------------------------
            // CONNECTION ERROR
            // -------------------------------------------------

            if (
                webRequest.result !=
                UnityWebRequest.Result.Success
            )
            {
                SetOutput(
                    "Could not connect to Ollama.\n\n" +
                    webRequest.error
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            string response =
                webRequest.downloadHandler.text;

            OllamaResponse ollamaResponse =
                null;

            try
            {
                ollamaResponse =
                    JsonUtility.FromJson<OllamaResponse>(
                        response
                    );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    exception
                );

                SetOutput(
                    "Invalid response from Ollama."
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            if (
                ollamaResponse == null ||
                ollamaResponse.message == null
            )
            {
                SetOutput(
                    "Ollama returned no message."
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            string llmContent =
                ollamaResponse.message.content;

            Debug.Log(
                "[DemoChat] LLM:\n" +
                llmContent
            );

            // -------------------------------------------------
            // JSON
            // -------------------------------------------------

            string commandJson =
                ExtractJson(
                    llmContent
                );

            if (string.IsNullOrEmpty(commandJson))
            {
                SetOutput(
                    "I could not understand the AI response."
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            LandscapeCommand command =
                null;

            try
            {
                command =
                    JsonUtility.FromJson<LandscapeCommand>(
                        commandJson
                    );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    exception
                );

                SetOutput(
                    "The AI command could not be parsed."
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            if (command == null)
            {
                SetOutput(
                    "The AI returned an empty command."
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            // -------------------------------------------------
            // COMMAND TYPE
            // -------------------------------------------------

            string commandType =
                command.commandType;

            if (
                string.IsNullOrWhiteSpace(
                    commandType
                )
            )
            {
                commandType =
                    DetectCommandType(
                        commandJson
                    );
            }

            commandType =
                commandType
                    .Trim()
                    .ToLowerInvariant();

            command.commandType =
                commandType;

            // -------------------------------------------------
            // DEBUG
            // -------------------------------------------------

            Debug.Log(
                "[DemoChat] Command type = " +
                commandType
            );

            Debug.Log(
                "[DemoChat] Confidence = " +
                command.confidence.ToString("F2")
            );

            Debug.Log(
                "[DemoChat] Reason = " +
                command.reason
            );

            // -------------------------------------------------
            // VALIDATION
            // -------------------------------------------------

            string validationError;

            if (
                !ValidateCommand(
                    command,
                    spatialContext,
                    out validationError
                )
            )
            {
                SetOutput(
                    validationError
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            // -------------------------------------------------
            // UNKNOWN
            // -------------------------------------------------

            if (
                commandType == "unknown" ||
                commandType == "unclear" ||
                commandType == "ambiguous"
            )
            {
                HandleUnknownCommand(
                    command
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            // -------------------------------------------------
            // CONFIDENCE
            // -------------------------------------------------

            float confidence =
                Mathf.Clamp01(
                    command.confidence
                );

            // -------------------------------------------------
            // LOW CONFIDENCE
            // -------------------------------------------------

            if (
                confidence <
                mediumConfidenceThreshold
            )
            {
                HandleLowConfidence(
                    command
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            // -------------------------------------------------
            // MEDIUM CONFIDENCE
            // -------------------------------------------------

            if (
                confidence <
                highConfidenceThreshold
            )
            {
                HandleMediumConfidence(
                    command,
                    spatialContext
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }

            // -------------------------------------------------
            // HIGH CONFIDENCE
            // -------------------------------------------------

            bool success =
                ExecuteValidatedCommand(
                    command,
                    spatialContext
                );

            FinishRequest(
                completionSource,
                success
            );
        }
    }


    // =========================================================
    // VALIDATE COMMAND
    // =========================================================

    private bool ValidateCommand(
        LandscapeCommand command,
        SpatialContext spatialContext,
        out string error
    )
    {
        error = "";

        if (command == null)
        {
            error =
                "The AI returned no command.";

            return false;
        }

        if (
            string.IsNullOrWhiteSpace(
                command.commandType
            )
        )
        {
            error =
                "The AI did not specify what system to use.";

            return false;
        }

        string commandType =
            command.commandType
                .Trim()
                .ToLowerInvariant();

        // -----------------------------------------------------
        // UNKNOWN
        // -----------------------------------------------------

        if (
            commandType == "unknown" ||
            commandType == "unclear" ||
            commandType == "ambiguous"
        )
        {
            return true;
        }

        // -----------------------------------------------------
        // TERRAIN
        // -----------------------------------------------------

        if (
            commandType == "terrain" ||
            commandType == "create_terrain" ||
            commandType == "modify_terrain"
        )
        {
            if (command.terrain == null)
            {
                error =
                    "The AI identified a terrain action " +
                    "but did not provide terrain settings.";

                return false;
            }

            return true;
        }

        // -----------------------------------------------------
        // TREES
        // -----------------------------------------------------

        if (
            commandType == "trees" ||
            commandType == "tree" ||
            commandType == "plant_trees" ||
            commandType == "remove_trees" ||
            commandType == "move_tree"
        )
        {
            if (command.trees == null)
            {
                error =
                    "The AI identified a tree action " +
                    "but did not provide tree settings.";

                return false;
            }

            string action =
                command.trees.action;

            if (
                string.IsNullOrWhiteSpace(
                    action
                )
            )
            {
                error =
                    "The AI did not specify the tree action.";

                return false;
            }

            return true;
        }

        // -----------------------------------------------------
        // CROPS
        // -----------------------------------------------------

        if (
            commandType == "crops" ||
            commandType == "crop" ||
            commandType == "farming"
        )
        {
            if (command.crops == null)
            {
                error =
                    "The AI identified a crop action " +
                    "but did not provide crop settings.";

                return false;
            }

            if (
                string.IsNullOrWhiteSpace(
                    command.crops.action
                )
            )
            {
                error =
                    "The AI did not specify the crop action.";

                return false;
            }

            return true;
        }

        // -----------------------------------------------------
        // ENVIRONMENT
        // -----------------------------------------------------

        if (
            commandType == "environment" ||
            commandType == "env"
        )
        {
            if (command.environment == null)
            {
                error =
                    "The AI identified an environment action " +
                    "but did not provide environment settings.";

                return false;
            }

            if (
                string.IsNullOrWhiteSpace(
                    command.environment.action
                )
            )
            {
                error =
                    "The AI did not specify the environment action.";

                return false;
            }

            return true;
        }

        error =
            "I could not determine which system " +
            "you want to control.";

        return false;
    }


    // =========================================================
    // UNKNOWN COMMAND
    // =========================================================

    private void HandleUnknownCommand(
        LandscapeCommand command
    )
    {
        string reason =
            command.reason;

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason =
                "The request is not specific enough.";
        }

        SetOutput(
            "I am not confident enough to act on that request.\n\n" +
            reason +
            "\n\n" +
            "Please tell me more specifically what you want " +
            "to change."
        );
    }


    // =========================================================
    // LOW CONFIDENCE
    // =========================================================

    private void HandleLowConfidence(
        LandscapeCommand command
    )
    {
        string reason =
            command.reason;

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason =
                "I am not confident that I understood the request.";
        }

        SetOutput(
            "I don't want to make the wrong change.\n\n" +
            reason +
            "\n\n" +
            "Please clarify what you want."
        );
    }


    // =========================================================
    // MEDIUM CONFIDENCE
    // =========================================================

    private void HandleMediumConfidence(
        LandscapeCommand command,
        SpatialContext spatialContext
    )
    {
        hasPendingConfirmation =
            true;

        pendingCommand =
            command;

        pendingSpatialContext =
            spatialContext;

        string actionDescription =
            DescribeCommand(
                command
            );

        SetOutput(
            "I think you want to:\n\n" +
            actionDescription +
            "\n\n" +
            "Confidence: " +
            command.confidence.ToString("P0") +
            "\n\n" +
            "Should I do that?\n\n" +
            "Say \"yes\" to confirm or \"no\" to cancel."
        );
    }


    // =========================================================
    // COMMAND DESCRIPTION
    // =========================================================

    private string DescribeCommand(
        LandscapeCommand command
    )
    {
        if (command == null)
        {
            return "perform the requested action";
        }

        string type =
            command.commandType
                .Trim()
                .ToLowerInvariant();

        // -----------------------------------------------------
        // TERRAIN
        // -----------------------------------------------------

        if (
            type == "terrain" ||
            type == "create_terrain" ||
            type == "modify_terrain"
        )
        {
            if (command.terrain == null)
            {
                return "modify the terrain";
            }

            return
                "modify the terrain to create " +
                command.terrain.terrainType +
                " terrain";
        }

        // -----------------------------------------------------
        // TREES
        // -----------------------------------------------------

        if (
            type == "trees" ||
            type == "tree" ||
            type == "plant_trees" ||
            type == "remove_trees" ||
            type == "move_tree"
        )
        {
            if (command.trees == null)
            {
                return "perform a tree operation";
            }

            string action =
                command.trees.action
                    .Trim()
                    .ToLowerInvariant();

            if (action == "move_tree")
            {
                if (
                    pendingSpatialContext != null &&
                    pendingSpatialContext.hasSelectedTree
                )
                {
                    return
                        "move " +
                        pendingSpatialContext.selectedTreeName +
                        " to the pointed location";
                }

                return "move the selected tree";
            }

            if (
                action == "plant" ||
                action == "plant_trees" ||
                action == "create_forest"
            )
            {
                return
                    "plant " +
                    Mathf.Max(
                        command.trees.count,
                        1
                    ) +
                    " " +
                    NormalizeName(
                        command.trees.treeType
                    ) +
                    " tree(s)";
            }

            if (
                action == "remove" ||
                action == "remove_trees"
            )
            {
                return "remove trees from the selected area";
            }

            if (
                action == "remove_all_trees" ||
                action == "clear_trees"
            )
            {
                return "remove all trees";
            }

            return
                "perform tree action: " +
                action;
        }

        // -----------------------------------------------------
        // CROPS
        // -----------------------------------------------------

        if (
            type == "crops" ||
            type == "crop" ||
            type == "farming"
        )
        {
            if (command.crops == null)
            {
                return "perform a crop operation";
            }

            string action =
                command.crops.action
                    .Trim()
                    .ToLowerInvariant();

            string crop =
                NormalizeName(
                    command.crops.cropType
                );

            if (
                action == "plant" ||
                action == "plant_crop" ||
                action == "plant_crops" ||
                action == "sow"
            )
            {
                return
                    "plant " +
                    Mathf.Max(
                        command.crops.count,
                        1
                    ) +
                    " " +
                    crop +
                    " crop(s)";
            }

            if (
                action == "grow" ||
                action == "grow_crop"
            )
            {
                return
                    "grow the " +
                    crop +
                    " crops";
            }

            if (
                action == "grow_all" ||
                action == "grow_all_crops" ||
                action == "mature_all"
            )
            {
                return
                    "grow the " +
                    crop +
                    " crops to maturity";
            }

            if (
                action == "harvest" ||
                action == "harvest_crop" ||
                action == "harvest_crops"
            )
            {
                return
                    "harvest the " +
                    crop +
                    " crops";
            }

            if (
                action == "get_yield" ||
                action == "yield" ||
                action == "crop_yield"
            )
            {
                return
                    "report the yield of the " +
                    crop +
                    " crops";
            }

            if (
                action == "crop_info" ||
                action == "get_crop_info" ||
                action == "status"
            )
            {
                return
                    "show information about the " +
                    crop +
                    " crops";
            }

            return
                "perform crop action: " +
                action;
        }

        // -----------------------------------------------------
        // ENVIRONMENT
        // -----------------------------------------------------

        if (
            type == "environment" ||
            type == "env"
        )
        {
            if (command.environment == null)
            {
                return "modify the environment";
            }

            string action =
                command.environment.action
                    .Trim()
                    .ToLowerInvariant();

            if (
                action == "greener" ||
                action == "more_green" ||
                action == "make_green" ||
                action == "increase_green"
            )
            {
                return "make the environment greener";
            }

            if (
                action == "less_green" ||
                action == "reduce_green" ||
                action == "less_greenery"
            )
            {
                return "make the environment less green";
            }

            if (
                action == "brighter" ||
                action == "increase_brightness" ||
                action == "more_light"
            )
            {
                return "make the environment brighter";
            }

            if (
                action == "darker" ||
                action == "decrease_brightness" ||
                action == "less_light"
            )
            {
                return "make the environment darker";
            }

            if (
                action == "warmer" ||
                action == "warm"
            )
            {
                return "make the environment warmer";
            }

            if (
                action == "cooler" ||
                action == "cool"
            )
            {
                return "make the environment cooler";
            }

            if (
                action == "stronger_shadows" ||
                action == "more_shadows"
            )
            {
                return "make the shadows stronger";
            }

            if (
                action == "softer_shadows" ||
                action == "less_shadows"
            )
            {
                return "make the shadows softer";
            }

            if (
                action == "add_fog" ||
                action == "fog"
            )
            {
                return "add fog to the environment";
            }

            if (
                action == "remove_fog" ||
                action == "no_fog"
            )
            {
                return "remove the fog";
            }

            if (
                action == "reset" ||
                action == "reset_environment"
            )
            {
                return "reset the environment";
            }

            return
                "perform environment action: " +
                action;
        }

        return "perform the requested action";
    }


    // =========================================================
    // EXECUTE VALIDATED COMMAND
    // =========================================================

    private bool ExecuteValidatedCommand(
        LandscapeCommand command,
        SpatialContext spatialContext
    )
    {
        if (command == null)
        {
            SetOutput(
                "No command was available to execute."
            );

            return false;
        }

        string commandType =
            command.commandType
                .Trim()
                .ToLowerInvariant();

        // =====================================================
        // TERRAIN
        // =====================================================

        if (
            commandType == "terrain" ||
            commandType == "create_terrain" ||
            commandType == "modify_terrain"
        )
        {
            return ProcessTerrainCommand(
                command
            );
        }

        // =====================================================
        // TREES
        // =====================================================

        if (
            commandType == "trees" ||
            commandType == "tree" ||
            commandType == "plant_trees" ||
            commandType == "remove_trees" ||
            commandType == "move_tree"
        )
        {
            return ProcessTreeCommand(
                command,
                spatialContext
            );
        }

        // =====================================================
        // CROPS
        // =====================================================

        if (
            commandType == "crops" ||
            commandType == "crop" ||
            commandType == "farming"
        )
        {
            return ProcessCropCommand(
                command
            );
        }

        // =====================================================
        // ENVIRONMENT
        // =====================================================

        if (
            commandType == "environment" ||
            commandType == "env"
        )
        {
            return ProcessEnvironmentCommand(
                command
            );
        }

        SetOutput(
            "I could not determine the requested action."
        );

        return false;
    }


    // =========================================================
    // TERRAIN
    // =========================================================

    private bool ProcessTerrainCommand(
        LandscapeCommand command
    )
    {
        if (terrainGenerator == null)
        {
            SetOutput(
                "TerrainGenerator is not assigned."
            );

            return false;
        }

        if (command.terrain == null)
        {
            SetOutput(
                "The AI returned no terrain settings."
            );

            return false;
        }

        TerrainSettings settings =
            command.terrain;

        terrainGenerator.GenerateTerrain(
            settings
        );

        SetOutput(
            "Terrain generated.\n\n" +
            "Type: " +
            settings.terrainType +
            "\nSize: " +
            settings.width +
            " x " +
            settings.depth +
            "\nHeight: " +
            settings.height
        );

        return true;
    }


    // =========================================================
    // TREES
    // =========================================================

    private bool ProcessTreeCommand(
        LandscapeCommand command,
        SpatialContext spatialContext
    )
    {
        if (treeManager == null)
        {
            SetOutput(
                "TreeManager is not assigned."
            );

            return false;
        }

        if (command.trees == null)
        {
            SetOutput(
                "The AI returned no tree settings."
            );

            return false;
        }

        TreeCommand tree =
            command.trees;

        string action =
            tree.action;

        if (string.IsNullOrWhiteSpace(action))
        {
            SetOutput(
                "The AI did not specify a tree action."
            );

            return false;
        }

        action =
            action.Trim().ToLowerInvariant();

        // =====================================================
        // MOVE TREE
        // =====================================================

        if (action == "move_tree")
        {
            return ProcessMoveTreeCommand(
                spatialContext
            );
        }

        // =====================================================
        // PLANT
        // =====================================================

        if (
            action == "plant_trees" ||
            action == "plant" ||
            action == "create_forest"
        )
        {
            int count =
                Mathf.Clamp(
                    tree.count,
                    1,
                    5000
                );

            float radius =
                Mathf.Max(
                    tree.radius,
                    1f
                );

            float spacing =
                Mathf.Max(
                    tree.spacing,
                    0.5f
                );

            string requestedType =
                NormalizeName(
                    tree.treeType
                );

            if (
                requestedType.Equals(
                    "mixed",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                treeManager.PlantMixedTrees(
                    count,
                    tree.centerX,
                    tree.centerZ,
                    radius,
                    spacing
                );

                SetOutput(
                    "Mixed forest planted.\n\n" +
                    "Count: " +
                    count
                );

                return true;
            }

            GameObject prefab =
                treeManager.GetTreePrefab(
                    requestedType
                );

            if (prefab == null)
            {
                SetOutput(
                    "Tree type '" +
                    requestedType +
                    "' was not found in TreeManager."
                );

                return false;
            }

            treeManager.PlantTrees(
                count,
                requestedType,
                tree.centerX,
                tree.centerZ,
                radius,
                spacing
            );

            SetOutput(
                requestedType +
                " trees planted.\n\n" +
                "Count: " +
                count
            );

            return true;
        }

        // =====================================================
        // REMOVE
        // =====================================================

        if (
            action == "remove_trees" ||
            action == "remove"
        )
        {
            treeManager.RemoveTrees(
                tree.centerX,
                tree.centerZ,
                Mathf.Max(
                    tree.radius,
                    1f
                )
            );

            SetOutput(
                "Trees removed."
            );

            return true;
        }

        // =====================================================
        // REMOVE ALL
        // =====================================================

        if (
            action == "remove_all_trees" ||
            action == "clear_trees"
        )
        {
            treeManager.RemoveAllTrees();

            SetOutput(
                "All trees removed."
            );

            return true;
        }

        SetOutput(
            "Unknown tree action: " +
            action
        );

        return false;
    }


    // =========================================================
    // MOVE TREE
    // =========================================================

    private bool ProcessMoveTreeCommand(
        SpatialContext spatialContext
    )
    {
        // -----------------------------------------------------
        // CRITICAL SAFETY CHECK
        // -----------------------------------------------------

        if (!spatialContext.hasSelectedTree)
        {
            SetOutput(
                "Please point at a tree first."
            );

            return false;
        }

        // -----------------------------------------------------
        // CRITICAL SAFETY CHECK
        // -----------------------------------------------------

        if (!spatialContext.hasGroundPosition)
        {
            SetOutput(
                "Please point at the ground where " +
                "you want to move the tree."
            );

            return false;
        }

        bool success =
            treeManager.MoveTreeToWorldPosition(
                spatialContext.selectedTreeIndex,
                spatialContext.groundPosition
            );

        if (!success)
        {
            SetOutput(
                "I could not move the selected tree."
            );

            return false;
        }

        SetOutput(
            "Moved " +
            spatialContext.selectedTreeName +
            " to the pointed location."
        );

        return true;
    }


    // =========================================================
    // CROPS
    // =========================================================

    private bool ProcessCropCommand(
        LandscapeCommand command
    )
    {
        if (cropManager == null)
        {
            SetOutput(
                "CropManager is not assigned."
            );

            return false;
        }

        if (command.crops == null)
        {
            SetOutput(
                "The AI returned no crop settings."
            );

            return false;
        }

        CropCommand crop =
            command.crops;

        string action =
            crop.action;

        if (string.IsNullOrWhiteSpace(action))
        {
            SetOutput(
                "The AI did not specify a crop action."
            );

            return false;
        }

        action =
            action.Trim().ToLowerInvariant();

        string cropType =
            NormalizeName(
                crop.cropType
            );

        Debug.Log(
            "[DemoChat] Crop action = " +
            action +
            " | Crop = " +
            cropType
        );

        // =====================================================
        // PLANT
        // =====================================================

        if (
            action == "plant_crop" ||
            action == "plant_crops" ||
            action == "plant" ||
            action == "sow"
        )
        {
            return PlantCrops(
                crop
            );
        }

        // =====================================================
        // GROW ONE
        // =====================================================

        if (
            action == "grow" ||
            action == "grow_crop"
        )
        {
            return GrowCrops(
                cropType,
                false
            );
        }

        // =====================================================
        // GROW ALL
        // =====================================================

        if (
            action == "grow_all" ||
            action == "grow_all_crops" ||
            action == "mature_all"
        )
        {
            return GrowCrops(
                cropType,
                true
            );
        }

        // =====================================================
        // HARVEST
        // =====================================================

        if (
            action == "harvest" ||
            action == "harvest_crop" ||
            action == "harvest_crops"
        )
        {
            return HarvestCrops(
                cropType
            );
        }

        // =====================================================
        // YIELD
        // =====================================================

        if (
            action == "get_yield" ||
            action == "yield" ||
            action == "crop_yield"
        )
        {
            return ReportYield(
                cropType
            );
        }

        // =====================================================
        // INFORMATION
        // =====================================================

        if (
            action == "crop_info" ||
            action == "get_crop_info" ||
            action == "status"
        )
        {
            return ReportCropInformation(
                cropType
            );
        }

        SetOutput(
            "Unknown crop action: " +
            action
        );

        return false;
    }


    // =========================================================
    // ENVIRONMENT
    // =========================================================

    private bool ProcessEnvironmentCommand(
        LandscapeCommand command
    )
    {
        if (environmentManager == null)
        {
            SetOutput(
                "EnvironmentManager is not assigned."
            );

            return false;
        }

        if (command.environment == null)
        {
            SetOutput(
                "The AI returned no environment settings."
            );

            return false;
        }

        EnvironmentCommand environment =
            command.environment;

        string action =
            environment.action;

        if (string.IsNullOrWhiteSpace(action))
        {
            SetOutput(
                "The AI did not specify the environment action."
            );

            return false;
        }

        action =
            action.Trim().ToLowerInvariant();

        float amount =
            Mathf.Clamp(
                environment.amount,
                0f,
                1f
            );

        Debug.Log(
            "[DemoChat] Environment action = " +
            action +
            " | Amount = " +
            amount.ToString("F2")
        );

        bool success =
            environmentManager.ApplyEnvironmentCommand(
                action,
                amount
            );

        if (!success)
        {
            SetOutput(
                "I could not apply the environment change."
            );

            return false;
        }

        SetOutput(
            "Environment updated.\n\n" +
            "Action: " +
            action
        );

        return true;
    }


    // =========================================================
    // PLANT CROPS
    // =========================================================

    private bool PlantCrops(
        CropCommand command
    )
    {
        if (cropManager == null)
        {
            SetOutput(
                "CropManager is not assigned."
            );

            return false;
        }

        int count =
            Mathf.Clamp(
                command.count <= 0
                    ? 1
                    : command.count,
                1,
                5000
            );

        string cropType =
            NormalizeName(
                command.cropType
            );

        Vector3 center =
            new Vector3(
                command.centerX,
                0f,
                command.centerZ
            );

        float radius =
            command.radius <= 0f
                ? 30f
                : command.radius;

        float spacing =
            command.spacing <= 0f
                ? 2f
                : command.spacing;

        int planted =
            cropManager.PlantCrops(
                cropType,
                count,
                center,
                radius,
                spacing
            );

        if (planted <= 0)
        {
            SetOutput(
                "No " +
                cropType +
                " crops were planted.\n\n" +
                "Make sure the planting position is inside " +
                "a SoilArea and the crop type exists."
            );

            return false;
        }

        SetOutput(
            "Planted " +
            planted +
            " " +
            cropType +
            " crop(s)."
        );

        return true;
    }


    // =========================================================
    // GET ALL CROPS
    // =========================================================

    private CropManager.CropInstance[] GetAllCrops()
    {
        return FindObjectsByType<CropManager.CropInstance>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
    }


    // =========================================================
    // GROW CROPS
    // =========================================================

    private bool GrowCrops(
        string cropType,
        bool all
    )
    {
        CropManager.CropInstance[] crops =
            GetAllCrops();

        int affected =
            0;

        foreach (
            CropManager.CropInstance crop
            in crops
        )
        {
            if (crop == null)
            {
                continue;
            }

            if (
                !IsCropMatch(
                    crop,
                    cropType
                )
            )
            {
                continue;
            }

            if (all)
            {
                crop.GrowToMaturity();

                affected++;
            }
            else
            {
                if (crop.Grow())
                {
                    affected++;
                }
            }
        }

        if (affected == 0)
        {
            SetOutput(
                "No matching crops were found to grow."
            );

            return false;
        }

        SetOutput(
            "Grew " +
            affected +
            " " +
            cropType +
            " crop(s)."
        );

        return true;
    }


    // =========================================================
    // HARVEST CROPS
    // =========================================================

    private bool HarvestCrops(
        string cropType
    )
    {
        CropManager.CropInstance[] crops =
            GetAllCrops();

        List<CropManager.CropInstance>
            harvestList =
            new List<CropManager.CropInstance>();

        foreach (
            CropManager.CropInstance crop
            in crops
        )
        {
            if (crop == null)
            {
                continue;
            }

            if (
                !IsCropMatch(
                    crop,
                    cropType
                )
            )
            {
                continue;
            }

            if (!crop.IsMature)
            {
                continue;
            }

            harvestList.Add(
                crop
            );
        }

        if (harvestList.Count == 0)
        {
            SetOutput(
                "No mature " +
                cropType +
                " crops are ready to harvest."
            );

            return false;
        }

        int harvested =
            harvestList.Count;

        foreach (
            CropManager.CropInstance crop
            in harvestList
        )
        {
            if (crop == null)
            {
                continue;
            }

            cropManager.RemoveCrop(
                crop.cropId
            );
        }

        AddYield(
            cropType,
            harvested
        );

        SetOutput(
            "Harvested " +
            harvested +
            " " +
            cropType +
            " crop(s).\n\n" +
            "Yield: " +
            GetYield(
                cropType
            )
        );

        return true;
    }


    // =========================================================
    // CROP MATCH
    // =========================================================

    private bool IsCropMatch(
        CropManager.CropInstance crop,
        string requestedType
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                requestedType
            )
        )
        {
            return true;
        }

        if (
            requestedType.Equals(
                "all",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return true;
        }

        if (
            requestedType.Equals(
                "default",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return true;
        }

        return
            crop.cropName.Equals(
                requestedType,
                StringComparison.OrdinalIgnoreCase
            );
    }


    // =========================================================
    // ADD YIELD
    // =========================================================

    private void AddYield(
        string cropType,
        int amount
    )
    {
        if (
            harvestedYield.ContainsKey(
                cropType
            )
        )
        {
            harvestedYield[cropType] +=
                amount;
        }
        else
        {
            harvestedYield[cropType] =
                amount;
        }
    }


    // =========================================================
    // GET YIELD
    // =========================================================

    private int GetYield(
        string cropType
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                cropType
            ) ||
            cropType.Equals(
                "all",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            int total =
                0;

            foreach (
                int value
                in harvestedYield.Values
            )
            {
                total += value;
            }

            return total;
        }

        int result;

        if (
            harvestedYield.TryGetValue(
                cropType,
                out result
            )
        )
        {
            return result;
        }

        return 0;
    }


    // =========================================================
    // REPORT YIELD
    // =========================================================

    private bool ReportYield(
        string cropType
    )
    {
        int yield =
            GetYield(
                cropType
            );

        if (
            cropType.Equals(
                "all",
                StringComparison.OrdinalIgnoreCase
            ) ||
            string.IsNullOrWhiteSpace(cropType)
        )
        {
            SetOutput(
                "Total crop yield: " +
                yield
            );
        }
        else
        {
            SetOutput(
                cropType +
                " yield: " +
                yield
            );
        }

        return true;
    }


    // =========================================================
    // REPORT CROP INFORMATION
    // =========================================================

    private bool ReportCropInformation(
        string cropType
    )
    {
        CropManager.CropInstance[] crops =
            GetAllCrops();

        int count =
            0;

        int mature =
            0;

        StringBuilder builder =
            new StringBuilder();

        foreach (
            CropManager.CropInstance crop
            in crops
        )
        {
            if (crop == null)
            {
                continue;
            }

            if (
                !IsCropMatch(
                    crop,
                    cropType
                )
            )
            {
                continue;
            }

            count++;

            if (crop.IsMature)
            {
                mature++;
            }

            builder.AppendLine(
                GetCropGrowthInformation(
                    crop
                )
            );

            builder.AppendLine();
        }

        if (count == 0)
        {
            SetOutput(
                "No " +
                cropType +
                " crops found."
            );

            return false;
        }

        SetOutput(
            "Crop information\n\n" +
            "Crop type: " +
            cropType +
            "\n" +
            "Total: " +
            count +
            "\n" +
            "Mature: " +
            mature +
            "\n\n" +
            builder.ToString()
        );

        return true;
    }


    // =========================================================
    // CROP INFORMATION
    // =========================================================

    private string GetCropGrowthInformation(
        CropManager.CropInstance crop
    )
    {
        if (crop == null)
        {
            return "";
        }

        return
            "ID: " +
            crop.cropId +
            " | " +
            "Type: " +
            crop.cropName +
            " | " +
            "Stage: " +
            crop.CurrentStage +
            " | " +
            "Mature: " +
            crop.IsMature +
            " | " +
            "Position: " +
            crop.transform.position;
    }


    // =========================================================
    // NORMALIZE NAME
    // =========================================================

    private string NormalizeName(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Default";
        }

        string result =
            value.Trim();

        result =
            Regex.Replace(
                result,
                @"\bcrops?\b",
                "",
                RegexOptions.IgnoreCase
            );

        result =
            Regex.Replace(
                result,
                @"\bplants?\b",
                "",
                RegexOptions.IgnoreCase
            );

        result =
            result.Trim();

        if (
            string.IsNullOrWhiteSpace(
                result
            )
        )
        {
            return "Default";
        }

        if (
            result.Equals(
                "all",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return "all";
        }

        return
            char.ToUpper(
                result[0]
            ) +
            result.Substring(1);
    }


    // =========================================================
    // COMMAND TYPE DETECTION
    // =========================================================

    private string DetectCommandType(
        string json
    )
    {
        if (string.IsNullOrEmpty(json))
        {
            return "";
        }

        string lower =
            json.ToLowerInvariant();

        if (
            lower.Contains(
                "\"unknown\""
            )
        )
        {
            return "unknown";
        }

        if (
            lower.Contains(
                "\"crops\""
            )
        )
        {
            return "crops";
        }

        if (
            lower.Contains(
                "\"trees\""
            )
        )
        {
            return "trees";
        }

        if (
            lower.Contains(
                "\"terrain\""
            )
        )
        {
            return "terrain";
        }

        if (
            lower.Contains(
                "\"environment\""
            )
        )
        {
            return "environment";
        }

        return "";
    }


    // =========================================================
    // SYSTEM PROMPT
    // =========================================================

    private string BuildSystemPrompt()
    {
        return
            "You are a Unity landscape and farming assistant.\n\n" +

            "You control FOUR systems:\n" +
            "1. Terrain\n" +
            "2. Trees\n" +
            "3. Crops\n" +
            "4. Environment\n\n" +

            "Your job is to convert natural language into EXACTLY " +
            "ONE JSON command.\n\n" +

            "VERY IMPORTANT:\n" +
            "You are NOT allowed to guess when the user's meaning " +
            "is unclear.\n\n" +

            "If the request is ambiguous, incomplete, vague, or " +
            "cannot confidently be mapped to a supported action, " +
            "return commandType = \"unknown\".\n\n" +

            "The system explicitly allows you to say that you " +
            "do not understand.\n\n" +

            "Return ONLY valid JSON.\n" +
            "No Markdown.\n" +
            "No code fences.\n" +
            "No explanation outside the JSON.\n\n" +

            // =================================================
            // CONFIDENCE
            // =================================================

            "CONFIDENCE:\n\n" +

            "Every command MUST contain:\n" +
            "\"confidence\": a number from 0.0 to 1.0\n" +
            "\"reason\": a short explanation of why the command " +
            "was selected.\n\n" +

            "Confidence means confidence that you correctly " +
            "understood the user's intended action.\n\n" +

            "0.80 to 1.00 = high confidence.\n" +
            "0.50 to 0.79 = medium confidence.\n" +
            "Below 0.50 = low confidence.\n\n" +

            "Do NOT artificially increase confidence.\n" +
            "Do NOT use 1.0 simply because one action is possible.\n\n" +

            // =================================================
            // UNKNOWN
            // =================================================

            "UNKNOWN COMMAND:\n\n" +

            "If the user says something vague such as:\n" +
            "\"make it nicer\"\n" +
            "\"make it beautiful\"\n" +
            "\"change it\"\n" +
            "\"do something with it\"\n" +
            "\"make this better\"\n\n" +

            "and the intended system or operation is unclear, " +
            "return:\n\n" +

            "{\n" +
            "  \"commandType\":\"unknown\",\n" +
            "  \"confidence\":0.20,\n" +
            "  \"reason\":\"The requested change is ambiguous.\"\n" +
            "}\n\n" +

            "NEVER force an ambiguous request into terrain, tree, " +
            "crop, or environment commands.\n\n" +

            // =================================================
            // TERRAIN
            // =================================================

            "TERRAIN COMMAND:\n\n" +

            "{\n" +
            "  \"commandType\":\"terrain\",\n" +
            "  \"confidence\":0.95,\n" +
            "  \"reason\":\"The user explicitly requested terrain modification.\",\n" +
            "  \"terrain\":{\n" +
            "    \"width\":200,\n" +
            "    \"depth\":200,\n" +
            "    \"height\":30,\n" +
            "    \"terrainType\":\"hills\",\n" +
            "    \"roughness\":0.5,\n" +
            "    \"detailScale\":0.03,\n" +
            "    \"octaves\":4,\n" +
            "    \"seed\":12345\n" +
            "  }\n" +
            "}\n\n" +

            "Terrain examples:\n" +
            "\"create hills\" -> terrain.\n" +
            "\"make the terrain mountainous\" -> terrain.\n" +
            "\"create a valley\" -> terrain.\n" +
            "\"make the terrain flatter\" -> terrain when supported.\n\n" +

            // =================================================
            // TREES
            // =================================================

            "TREE COMMAND:\n\n" +

            "{\n" +
            "  \"commandType\":\"trees\",\n" +
            "  \"confidence\":0.95,\n" +
            "  \"reason\":\"The user explicitly requested tree planting.\",\n" +
            "  \"trees\":{\n" +
            "    \"action\":\"plant_trees\",\n" +
            "    \"treeType\":\"Oak\",\n" +
            "    \"count\":20,\n" +
            "    \"centerX\":100,\n" +
            "    \"centerZ\":100,\n" +
            "    \"radius\":30,\n" +
            "    \"spacing\":5\n" +
            "  }\n" +
            "}\n\n" +

            "TREE TYPES:\n" +
            "TreeManager decides which species exist.\n" +
            "Never invent a replacement species.\n" +
            "Preserve the user's requested tree species.\n\n" +

            "Examples:\n" +
            "oak -> Oak\n" +
            "pine -> Pine\n" +
            "palm -> Palm\n" +
            "birch -> Birch\n" +
            "maple -> Maple\n" +
            "eucalyptus -> Eucalyptus\n" +
            "apple -> Apple\n\n" +

            // =================================================
            // MOVE TREE
            // =================================================

            "MOVE TREE:\n\n" +

            "If the user says:\n" +
            "\"move this tree over there\"\n" +
            "\"put this tree there\"\n" +
            "\"move it over there\"\n\n" +

            "and Unity provides a selected tree AND a pointed " +
            "ground position, return:\n\n" +

            "{\n" +
            "  \"commandType\":\"trees\",\n" +
            "  \"confidence\":0.95,\n" +
            "  \"reason\":\"The user requested moving the currently selected tree to the pointed location.\",\n" +
            "  \"trees\":{\n" +
            "    \"action\":\"move_tree\",\n" +
            "    \"treeType\":\"\"\n" +
            "  }\n" +
            "}\n\n" +

            "IMPORTANT:\n" +
            "Unity provides the selected tree.\n" +
            "Unity provides the tree ID.\n" +
            "Unity provides the target position.\n\n" +

            "NEVER invent a tree ID.\n" +
            "NEVER invent movement coordinates.\n\n" +

            // =================================================
            // CROPS
            // =================================================

            "CROP SYSTEM:\n\n" +

            "Use commandType = \"crops\" for crop operations.\n\n" +

            "PLANTING:\n\n" +

            "\"plant wheat\" -> Wheat.\n" +
            "\"plant 20 wheat crops\" -> count 20, Wheat.\n" +
            "\"plant corn\" -> Corn.\n\n" +

            "{\n" +
            "  \"commandType\":\"crops\",\n" +
            "  \"confidence\":0.95,\n" +
            "  \"reason\":\"The user explicitly requested crop planting.\",\n" +
            "  \"crops\":{\n" +
            "    \"action\":\"plant_crops\",\n" +
            "    \"cropType\":\"Wheat\",\n" +
            "    \"count\":20,\n" +
            "    \"centerX\":100,\n" +
            "    \"centerZ\":100,\n" +
            "    \"radius\":30,\n" +
            "    \"spacing\":2\n" +
            "  }\n" +
            "}\n\n" +

            "CropManager checks SoilArea before planting.\n" +
            "Do not claim that crops were planted unless Unity " +
            "accepts the command.\n\n" +

            // =================================================
            // GROW
            // =================================================

            "GROWING:\n\n" +

            "\"grow the wheat\" -> grow Wheat.\n" +
            "\"let the wheat grow\" -> grow Wheat.\n" +
            "\"make the wheat mature\" -> grow_all_crops Wheat.\n" +
            "\"grow all crops\" -> grow_all_crops all.\n\n" +

            // =================================================
            // HARVEST
            // =================================================

            "HARVESTING:\n\n" +

            "\"harvest the wheat\" -> harvest Wheat.\n" +
            "\"harvest my wheat\" -> harvest Wheat.\n" +
            "\"collect the wheat\" -> harvest Wheat.\n" +
            "\"harvest all crops\" -> harvest all.\n\n" +

            // =================================================
            // YIELD
            // =================================================

            "YIELD:\n\n" +

            "\"what is the yield?\"\n" +
            "\"what was the crop yield?\"\n" +
            "\"how much wheat did I harvest?\"\n" +
            "\"how much did I get from the wheat?\"\n\n" +

            "These mean get_yield.\n\n" +

            "The AI MUST NEVER invent yield numbers.\n" +
            "Unity calculates the actual yield.\n\n" +

            // =================================================
            // STATUS
            // =================================================

            "CROP STATUS:\n\n" +

            "\"how are the wheat crops doing?\"\n" +
            "\"are the wheat mature?\"\n" +
            "\"show me the crop status\"\n\n" +

            "These mean crop_info.\n\n" +

            // =================================================
            // ENVIRONMENT
            // =================================================

            "ENVIRONMENT SYSTEM:\n\n" +

            "Use commandType = \"environment\" for visual " +
            "environment changes.\n\n" +

            "This includes:\n" +
            "- greenery\n" +
            "- brightness\n" +
            "- darkness\n" +
            "- warmth\n" +
            "- coolness\n" +
            "- shadows\n" +
            "- fog\n\n" +

            "The EnvironmentManager performs the actual Unity " +
            "visual changes.\n\n" +

            "The AI only selects the requested action.\n\n" +

            "SUPPORTED ENVIRONMENT ACTIONS:\n\n" +

            "\"make the environment greener\" -> greener.\n" +
            "\"make the grass more green\" -> greener.\n" +
            "\"make everything greener\" -> greener.\n" +
            "\"make it greener\" -> greener ONLY when the context " +
            "clearly refers to the environment.\n\n" +

            "\"make the environment less green\" -> less_green.\n" +
            "\"make it less green\" -> less_green when the context " +
            "clearly refers to the environment.\n\n" +

            "\"make it brighter\" -> brighter.\n" +
            "\"make the environment brighter\" -> brighter.\n" +
            "\"add more light\" -> brighter.\n\n" +

            "\"make it darker\" -> darker.\n" +
            "\"make the environment darker\" -> darker.\n\n" +

            "\"make it warmer\" -> warmer.\n" +
            "\"make the lighting warmer\" -> warmer.\n\n" +

            "\"make it cooler\" -> cooler.\n" +
            "\"make the lighting cooler\" -> cooler.\n\n" +

            "\"make the shadows stronger\" -> stronger_shadows.\n" +
            "\"make the shadows darker\" -> stronger_shadows.\n\n" +

            "\"make the shadows softer\" -> softer_shadows.\n" +
            "\"reduce the shadows\" -> softer_shadows.\n\n" +

            "\"add fog\" -> add_fog.\n" +
            "\"make it foggy\" -> add_fog.\n\n" +

            "\"remove the fog\" -> remove_fog.\n" +
            "\"clear the fog\" -> remove_fog.\n\n" +

            "\"reset the environment\" -> reset.\n" +
            "\"restore the environment\" -> reset.\n\n" +

            "ENVIRONMENT JSON EXAMPLE:\n\n" +

            "{\n" +
            "  \"commandType\":\"environment\",\n" +
            "  \"confidence\":0.95,\n" +
            "  \"reason\":\"The user explicitly requested a greener environment.\",\n" +
            "  \"environment\":{\n" +
            "    \"action\":\"greener\",\n" +
            "    \"amount\":0.25\n" +
            "  }\n" +
            "}\n\n" +

            "ENVIRONMENT AMOUNT:\n" +
            "Use a number from 0.0 to 1.0.\n" +
            "Small change = approximately 0.20.\n" +
            "Moderate change = approximately 0.25.\n" +
            "Large change = approximately 0.50.\n\n" +

            "Do not invent Unity object names.\n" +
            "Do not directly manipulate Unity objects.\n" +
            "EnvironmentManager performs the actual change.\n\n" +

            // =================================================
            // NATURAL LANGUAGE
            // =================================================

            "NATURAL LANGUAGE:\n\n" +

            "\"grow the wheat\" -> grow Wheat.\n" +
            "\"let the wheat grow\" -> grow Wheat.\n" +
            "\"make the wheat mature\" -> grow_all_crops Wheat.\n" +
            "\"harvest the wheat\" -> harvest Wheat.\n" +
            "\"harvest my wheat\" -> harvest Wheat.\n" +
            "\"collect the wheat\" -> harvest Wheat.\n" +
            "\"what is my wheat yield\" -> get_yield Wheat.\n" +
            "\"how much wheat did I get\" -> get_yield Wheat.\n" +
            "\"what was the harvest\" -> get_yield all.\n" +
            "\"what is the yield\" -> get_yield all.\n" +
            "\"make the environment greener\" -> environment greener.\n" +
            "\"make everything greener\" -> environment greener.\n" +
            "\"make it brighter\" -> environment brighter when " +
            "the context refers to the environment.\n" +
            "\"make the lighting warmer\" -> environment warmer.\n" +
            "\"add fog\" -> environment add_fog.\n\n" +

            // =================================================
            // DEFAULTS
            // =================================================

            "DEFAULT CROP VALUES:\n" +
            "count = 20 if unspecified.\n" +
            "centerX = 100 if unspecified.\n" +
            "centerZ = 100 if unspecified.\n" +
            "radius = 30 if unspecified.\n" +
            "spacing = 2 if unspecified.\n\n" +

            "DEFAULT TREE VALUES:\n" +
            "count = 20 if unspecified.\n" +
            "radius = 30 if unspecified.\n" +
            "spacing = 5 if unspecified.\n\n" +

            // =================================================
            // FINAL RULES
            // =================================================

            "FINAL RULES:\n\n" +

            "1. Return exactly ONE JSON command.\n" +
            "2. Return ONLY JSON.\n" +
            "3. Always include confidence.\n" +
            "4. Always include reason.\n" +
            "5. Never guess when the request is ambiguous.\n" +
            "6. Use unknown when necessary.\n" +
            "7. Never invent tree IDs.\n" +
            "8. Never invent exact movement coordinates.\n" +
            "9. Never invent crop yield numbers.\n" +
            "10. Never claim Unity performed an action.\n" +
            "11. Unity is responsible for physical execution.\n" +
            "12. Preserve the user's requested tree or crop type.\n" +
            "13. Use commandType = \"environment\" for environment changes.\n" +
            "14. EnvironmentManager performs environment changes.\n";
    }


    // =========================================================
    // JSON EXTRACTION
    // =========================================================

    private string ExtractJson(
        string text
    )
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        text =
            text.Trim();

        text =
            Regex.Replace(
                text,
                @"```json\s*",
                "",
                RegexOptions.IgnoreCase
            );

        text =
            Regex.Replace(
                text,
                @"```\s*",
                "",
                RegexOptions.IgnoreCase
            );

        int start =
            text.IndexOf('{');

        int end =
            text.LastIndexOf('}');

        if (
            start < 0 ||
            end <= start
        )
        {
            return null;
        }

        return text.Substring(
            start,
            end - start + 1
        );
    }


    // =========================================================
    // OUTPUT
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

        Debug.Log(
            "[DemoChat] " +
            message
        );
    }


    // =========================================================
    // BUTTON
    // =========================================================

    private void SetButtonInteractable(
        bool value
    )
    {
        if (sendButton != null)
        {
            sendButton.interactable =
                value;
        }
    }


    // =========================================================
    // FINISH
    // =========================================================

    private void FinishRequest(
        TaskCompletionSource<bool>
            completionSource,
        bool success
    )
    {
        isGenerating =
            false;

        SetButtonInteractable(
            true
        );

        if (completionSource != null)
        {
            completionSource.TrySetResult(
                success
            );
        }
    }


    // =========================================================
    // SPATIAL CONTEXT
    // =========================================================

    [Serializable]
    private class SpatialContext
    {
        public bool hasSelectedTree;

        public int selectedTreeIndex;

        public string selectedTreeName;

        public string selectedTreeType;

        public bool hasGroundPosition;

        public Vector3 groundPosition;
    }


    // =========================================================
    // LANDSCAPE COMMAND
    // =========================================================

    [Serializable]
    private class LandscapeCommand
    {
        public string commandType;

        public float confidence;

        public string reason;

        public TerrainSettings terrain;

        public TreeCommand trees;

        public CropCommand crops;

        public EnvironmentCommand environment;
    }


    // =========================================================
    // TREE COMMAND
    // =========================================================

    [Serializable]
    private class TreeCommand
    {
        public string action;

        public string treeType;

        public int count;

        public float centerX;

        public float centerZ;

        public float radius;

        public float spacing;
    }


    // =========================================================
    // CROP COMMAND
    // =========================================================

    [Serializable]
    private class CropCommand
    {
        public string action;

        public string cropType;

        public int count;

        public float centerX;

        public float centerZ;

        public float radius;

        public float spacing;
    }


    // =========================================================
    // ENVIRONMENT COMMAND
    // =========================================================

    [Serializable]
    private class EnvironmentCommand
    {
        public string action;

        public float amount;
    }


    // =========================================================
    // OLLAMA REQUEST
    // =========================================================

    [Serializable]
    private class OllamaRequest
    {
        public string model;

        public bool stream;

        public OllamaMessage[] messages;
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
    // OLLAMA MESSAGE
    // =========================================================

    [Serializable]
    private class OllamaMessage
    {
        public string role;

        public string content;
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(
                SendCurrentPrompt
            );
        }
    }
}