using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class DemoChat : MonoBehaviour
{
  

    [Header("Ollama")]

    [SerializeField]
    private string ollamaUrl =
        "http://localhost:11434/api/chat";

    [SerializeField]
    private string demoModel =
        "qwen3:4b";

   

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
    // TREE POINTING
    // =========================================================

    [Header("XR Tree Pointing")]

    [SerializeField]
    private TreePointingDetector treePointingDetector;


    // =========================================================
    // BUILDINGS
    // =========================================================

    [Header("Buildings")]

    [SerializeField]
    private BuildingGenerator buildingGenerator;


    // =========================================================
    // WATER
    // =========================================================

    [Header("Water")]

    [SerializeField]
    private WaterManager waterManager;


    // =========================================================
    // CONFIDENCE
    // =========================================================

    [Header("AI Confidence")]

    [SerializeField]
    private float highConfidenceThreshold = 0.80f;

    [SerializeField]
    private float mediumConfidenceThreshold = 0.50f;


    // =========================================================
    // CONVERSATION
    // =========================================================

    [Header("Conversation")]

    [SerializeField]
    private bool useConversationHistory = true;

    [SerializeField]
    [Range(1, 10)]
    private int maximumConversationTurns = 6;


    // =========================================================
    // LLM ROLE
    // =========================================================

    [Header("LLM Role")]

    [SerializeField]
    private bool allowDesignAdvice = true;

    [SerializeField]
    private bool allowClarification = true;


    // =========================================================
    // STATE
    // =========================================================

    private bool isGenerating = false;

    private bool hasPendingConfirmation = false;

    private LandscapeCommand pendingCommand = null;

    private SpatialContext pendingSpatialContext = null;


    // =========================================================
    // CONVERSATION HISTORY
    // =========================================================

    private List<ConversationTurn> conversationHistory =
        new List<ConversationTurn>();


    private LandscapeCommand lastAdviceCommand = null;

    private string lastAdviceText = "";


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool IsGenerating
    {
        get { return isGenerating; }
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
                "Describe the terrain, mountains, trees, buildings, or water you want.";
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
    // SEND PROMPT
    // =========================================================

    public void SendPrompt(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return;
        }

        userPrompt =
            userPrompt.Trim();


        // -----------------------------------------------------
        // CONFIRMATION
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

            ClearPendingConfirmation();
        }


        // -----------------------------------------------------
        // QUICK "DO IT" / "DO THAT"
        // -----------------------------------------------------

        // If the previous AI response contained an actionable
        // recommendation, allow the user to directly execute it.

        if (
            HasActionableAdvice() &&
            IsExecutePreviousAdviceRequest(userPrompt)
        )
        {
            ExecuteLastAdvice();
            return;
        }


        // -----------------------------------------------------
        // TRANSCRIPT
        // -----------------------------------------------------

        if (!IsUsableTranscript(userPrompt))
        {
            SetOutput(
                "I could not clearly understand the request. " +
                "Please try again."
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

    public async Task Ask(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return;
        }

        userPrompt =
            userPrompt.Trim();


        // -----------------------------------------------------
        // DIRECT EXECUTION OF LAST ADVICE
        // -----------------------------------------------------

        if (
            HasActionableAdvice() &&
            IsExecutePreviousAdviceRequest(userPrompt)
        )
        {
            ExecuteLastAdvice();
            return;
        }


        // -----------------------------------------------------
        // TRANSCRIPT
        // -----------------------------------------------------

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

    private bool IsUsableTranscript(string transcript)
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

        if (string.IsNullOrWhiteSpace(withoutPunctuation))
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
    // CONFIRMATION YES
    // =========================================================

    private bool IsConfirmationYes(string text)
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
            normalized == "do that" ||
            normalized == "do it too" ||
            normalized == "go ahead" ||
            normalized == "that's right" ||
            normalized == "correct";
    }


    // =========================================================
    // CONFIRMATION NO
    // =========================================================

    private bool IsConfirmationNo(string text)
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
    // PREVIOUS ADVICE EXECUTION REQUEST
    // =========================================================

    private bool IsExecutePreviousAdviceRequest(
        string text
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized =
            text.Trim().ToLowerInvariant();

        return
            normalized == "do it" ||
            normalized == "do that" ||
            normalized == "do it too" ||
            normalized == "do that too" ||
            normalized == "go ahead" ||
            normalized == "apply that" ||
            normalized == "apply it" ||
            normalized == "make it happen" ||
            normalized == "make that happen" ||
            normalized == "yes, do it" ||
            normalized == "yes do it" ||
            normalized == "yes, do that" ||
            normalized == "yes do that";
    }


    // =========================================================
    // HAS ACTIONABLE ADVICE
    // =========================================================

    private bool HasActionableAdvice()
    {
        return
            lastAdviceCommand != null &&
            !string.IsNullOrWhiteSpace(
                lastAdviceCommand.commandType
            );
    }


    // =========================================================
    // EXECUTE LAST ADVICE
    // =========================================================

    private void ExecuteLastAdvice()
    {
        if (!HasActionableAdvice())
        {
            SetOutput(
                "I do not have a previous actionable recommendation to apply."
            );

            return;
        }

        LandscapeCommand command =
            CloneCommand(
                lastAdviceCommand
            );

        SpatialContext spatialContext =
            CaptureSpatialContext();

        SetOutput(
            "Applying my previous recommendation..."
        );

        bool success =
            ExecuteValidatedCommand(
                command,
                spatialContext
            );

        if (success)
        {
            AddConversationTurn(
                "Do it.",
                "Applied the previous recommendation: " +
                DescribeCommand(command)
            );

            // Clear the recommendation after execution.
            lastAdviceCommand = null;
            lastAdviceText = "";
        }
        else
        {
            AddConversationTurn(
                "Do it.",
                "The previous recommendation could not be executed."
            );
        }
    }


    // =========================================================
    // CLONE COMMAND
    // =========================================================

    private LandscapeCommand CloneCommand(
        LandscapeCommand source
    )
    {
        if (source == null)
        {
            return null;
        }

        LandscapeCommand clone =
            new LandscapeCommand();

        clone.commandType =
            source.commandType;

        clone.confidence =
            source.confidence;

        clone.reason =
            source.reason;

        clone.assistantMessage =
            source.assistantMessage;


        if (source.terrain != null)
        {
            clone.terrain =
                new TerrainCommand();

            clone.terrain.action =
                source.terrain.action;

            clone.terrain.count =
                source.terrain.count;

            clone.terrain.heightChange =
                source.terrain.heightChange;
        }


        if (source.trees != null)
        {
            clone.trees =
                new TreeCommand();

            clone.trees.action =
                source.trees.action;

            clone.trees.treeType =
                source.trees.treeType;

            clone.trees.count =
                source.trees.count;

            clone.trees.centerX =
                source.trees.centerX;

            clone.trees.centerZ =
                source.trees.centerZ;

            clone.trees.radius =
                source.trees.radius;

            clone.trees.spacing =
                source.trees.spacing;
        }


        if (source.building != null)
        {
            clone.building =
                new BuildingCommand();

            clone.building.action =
                source.building.action;

            clone.building.buildingType =
                source.building.buildingType;

            clone.building.count =
                source.building.count;
        }


        if (source.water != null)
        {
            clone.water =
                new WaterCommand();

            clone.water.action =
                source.water.action;
        }


        return clone;
    }


    // =========================================================
    // CONFIRM
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
    // CANCEL
    // =========================================================

    private void CancelPendingCommand()
    {
        ClearPendingConfirmation();

        SetOutput(
            "Okay. I cancelled that action."
        );
    }


    // =========================================================
    // CLEAR CONFIRMATION
    // =========================================================

    private void ClearPendingConfirmation()
    {
        hasPendingConfirmation = false;
        pendingCommand = null;
        pendingSpatialContext = null;
    }


    // =========================================================
    // SPATIAL CONTEXT
    // =========================================================

    private SpatialContext CaptureSpatialContext()
    {
        SpatialContext context =
            new SpatialContext();


        // =====================================================
        // TREE + CURRENT TERRAIN GROUND POSITION
        // =====================================================

        if (treePointingDetector != null)
        {
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


            // =================================================
            // CURRENT POINTED TERRAIN POSITION
            // =================================================

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
        }


        return context;
    }


    // =========================================================
    // SPATIAL TEXT
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


        // =====================================================
        // TREE
        // =====================================================

        if (context.hasSelectedTree)
        {
            builder.AppendLine(
                "Selected tree: " +
                context.selectedTreeName
            );

            builder.AppendLine(
                "Selected tree ID: " +
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


        // =====================================================
        // CURRENT POINTED TERRAIN POSITION
        // =====================================================

        builder.AppendLine();

        builder.AppendLine(
            "CURRENT POINTED TERRAIN POSITION:"
        );

        builder.AppendLine(
            "This Unity-provided position can be used as the spatial reference for mountain modification and tree movement."
        );

        if (context.hasGroundPosition)
        {
            Vector3 p =
                context.groundPosition;

            builder.AppendLine(
                "Pointing at terrain: true"
            );

            builder.AppendLine(
                "Current ground position X: " +
                p.x.ToString("F3")
            );

            builder.AppendLine(
                "Current ground position Y: " +
                p.y.ToString("F3")
            );

            builder.AppendLine(
                "Current ground position Z: " +
                p.z.ToString("F3")
            );
        }
        else
        {
            builder.AppendLine(
                "Pointing at terrain: false"
            );
        }


        // =====================================================
        // SPATIAL RULES
        // =====================================================

        builder.AppendLine();

        builder.AppendLine(
            "SPATIAL RULES:"
        );

        builder.AppendLine(
            "Unity provides selected tree information."
        );

        builder.AppendLine(
            "Unity provides the current pointed Terrain ground position."
        );

        builder.AppendLine(
            "The current pointed Terrain ground position is the ONLY location reference for mountain modification."
        );

        builder.AppendLine(
            "The same current pointed Terrain ground position can be used for tree movement destinations."
        );

        builder.AppendLine(
            "This works for BOTH mountains that already existed on the Terrain and mountains generated by Unity."
        );

        builder.AppendLine(
            "Mountain IDs are NOT required."
        );

        builder.AppendLine(
            "Mountain IDs are NOT used for mountain modification."
        );

        builder.AppendLine(
            "The AI MUST NOT invent tree IDs."
        );

        builder.AppendLine(
            "The AI MUST NOT invent movement coordinates."
        );

        builder.AppendLine(
            "For 'this tree', use the selected tree."
        );

        builder.AppendLine(
            "For 'this mountain', use the CURRENT pointed Terrain ground position."
        );

        builder.AppendLine(
            "For 'it', use the currently selected object when the reference is clear."
        );

        builder.AppendLine(
            "For 'over there', use the Unity-provided pointed position."
        );

        builder.AppendLine(
            "Buildings use BuildingGenerator's random terrain placement."
        );

        builder.AppendLine(
            "Water uses WaterManager's own safe placement logic."
        );

        return builder.ToString();
    }


    // =========================================================
    // CONVERSATION CONTEXT
    // =========================================================

    private string BuildConversationContext()
    {
        if (
            !useConversationHistory ||
            conversationHistory == null ||
            conversationHistory.Count == 0
        )
        {
            return
                "There is no previous conversation context.";
        }

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "RECENT CONVERSATION SUMMARY:"
        );

        int startIndex =
            Mathf.Max(
                0,
                conversationHistory.Count -
                maximumConversationTurns
            );

        for (
            int i = startIndex;
            i < conversationHistory.Count;
            i++
        )
        {
            ConversationTurn turn =
                conversationHistory[i];

            builder.AppendLine(
                "User: " +
                turn.userMessage
            );

            builder.AppendLine(
                "Assistant: " +
                turn.assistantResult
            );

            builder.AppendLine();
        }

        return builder.ToString();
    }


    // =========================================================
    // ADD CONVERSATION TURN
    // =========================================================

    private void AddConversationTurn(
        string userMessage,
        string assistantResult
    )
    {
        if (!useConversationHistory)
        {
            return;
        }

        if (conversationHistory == null)
        {
            conversationHistory =
                new List<ConversationTurn>();
        }

        conversationHistory.Add(
            new ConversationTurn
            {
                userMessage = userMessage,
                assistantResult = assistantResult
            }
        );

        while (
            conversationHistory.Count >
            maximumConversationTurns
        )
        {
            conversationHistory.RemoveAt(0);
        }
    }


    // =========================================================
    // OLLAMA
    // =========================================================

    private IEnumerator SendToOllama(
        string userPrompt,
        SpatialContext spatialContext,
        TaskCompletionSource<bool> completionSource
    )
    {
        if (isGenerating)
        {
            while (isGenerating)
            {
                yield return null;
            }
        }

        isGenerating = true;

        SetButtonInteractable(false);

        SetOutput(
            "AI is thinking..."
        );


        string systemPrompt =
            BuildSystemPrompt();

        string spatialText =
            BuildSpatialContext(
                spatialContext
            );

        string conversationText =
            BuildConversationContext();


        // =====================================================
        // CURRENT USER MESSAGE
        // =====================================================

        string userMessage =
            "USER REQUEST:\n" +
            userPrompt +
            "\n\n" +
            spatialText +
            "\n\n" +
            conversationText;


        // =====================================================
        // BUILD ACTUAL OLLAMA CONVERSATION
        // =====================================================

        List<OllamaMessage> messages =
            new List<OllamaMessage>();


        // SYSTEM

        messages.Add(
            new OllamaMessage
            {
                role = "system",
                content = systemPrompt
            }
        );


        // PREVIOUS CONVERSATION

        if (
            useConversationHistory &&
            conversationHistory != null
        )
        {
            int startIndex =
                Mathf.Max(
                    0,
                    conversationHistory.Count -
                    maximumConversationTurns
                );

            for (
                int i = startIndex;
                i < conversationHistory.Count;
                i++
            )
            {
                ConversationTurn turn =
                    conversationHistory[i];

                messages.Add(
                    new OllamaMessage
                    {
                        role = "user",
                        content =
                            turn.userMessage
                    }
                );

                messages.Add(
                    new OllamaMessage
                    {
                        role = "assistant",
                        content =
                            turn.assistantResult
                    }
                );
            }
        }


        // CURRENT REQUEST

        messages.Add(
            new OllamaMessage
            {
                role = "user",
                content = userMessage
            }
        );


        // =====================================================
        // REQUEST
        // =====================================================

        OllamaRequest request =
            new OllamaRequest();

        request.model =
            demoModel;

        request.stream =
            false;

        request.messages =
            messages.ToArray();


        string json =
            JsonUtility.ToJson(request);

        byte[] body =
            Encoding.UTF8.GetBytes(json);


        using (
            UnityWebRequest webRequest =
                new UnityWebRequest(
                    ollamaUrl,
                    "POST"
                )
        )
        {
            webRequest.uploadHandler =
                new UploadHandlerRaw(body);

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
                Debug.LogError(exception);

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
                ExtractJson(llmContent);

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
                Debug.LogError(exception);

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
            // ADVICE
            // -------------------------------------------------

            if (commandType == "advice")
            {
                HandleAdvice(
                    command
                );

                string adviceResult =
                    string.IsNullOrWhiteSpace(
                        command.assistantMessage
                    )
                        ? command.reason
                        : command.assistantMessage;

                AddConversationTurn(
                    userPrompt,
                    adviceResult
                );

                FinishRequest(
                    completionSource,
                    true
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
                HandleUnknownCommand(command);

                AddConversationTurn(
                    userPrompt,
                    command.reason
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


            if (
                confidence <
                mediumConfidenceThreshold
            )
            {
                HandleLowConfidence(command);

                AddConversationTurn(
                    userPrompt,
                    command.reason
                );

                FinishRequest(
                    completionSource,
                    false
                );

                yield break;
            }


            if (
                confidence <
                highConfidenceThreshold
            )
            {
                HandleMediumConfidence(
                    command,
                    spatialContext
                );

                AddConversationTurn(
                    userPrompt,
                    DescribeCommand(command)
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

            AddConversationTurn(
                userPrompt,
                success
                    ? DescribeCommand(command)
                    : "The requested action could not be executed."
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


        // UNKNOWN

        if (
            commandType == "unknown" ||
            commandType == "unclear" ||
            commandType == "ambiguous"
        )
        {
            if (!allowClarification)
            {
                error =
                    "Clarification is currently disabled.";

                return false;
            }

            return true;
        }


        // ADVICE

        if (commandType == "advice")
        {
            if (!allowDesignAdvice)
            {
                error =
                    "Design advice is currently disabled.";

                return false;
            }

            if (
                string.IsNullOrWhiteSpace(
                    command.assistantMessage
                ) &&
                string.IsNullOrWhiteSpace(
                    command.reason
                )
            )
            {
                error =
                    "The AI did not provide an explanation.";

                return false;
            }

            // suggestedCommand is optional.
            // Advice can exist without an executable action.

            if (
                command.suggestedCommand != null
            )
            {
                string suggestedType =
                    command.suggestedCommand.commandType;

                if (
                    string.IsNullOrWhiteSpace(
                        suggestedType
                    )
                )
                {
                    error =
                        "The AI provided an invalid recommendation.";

                    return false;
                }

                suggestedType =
                    suggestedType
                        .Trim()
                        .ToLowerInvariant();

                if (
                    suggestedType == "advice" ||
                    suggestedType == "unknown" ||
                    suggestedType == "unclear" ||
                    suggestedType == "ambiguous"
                )
                {
                    error =
                        "The AI recommendation is not an executable Unity action.";

                    return false;
                }

                string suggestedError;

                if (
                    !ValidateCommand(
                        command.suggestedCommand,
                        spatialContext,
                        out suggestedError
                    )
                )
                {
                    error =
                        "The AI recommendation could not be validated.\n\n" +
                        suggestedError;

                    return false;
                }
            }

            return true;
        }


        // TERRAIN

        if (
            commandType == "terrain" ||
            commandType == "modify_terrain" ||
            commandType == "terrain_modify"
        )
        {
            if (command.terrain == null)
            {
                error =
                    "The AI identified a terrain action " +
                    "but did not provide terrain settings.";

                return false;
            }

            if (
                string.IsNullOrWhiteSpace(
                    command.terrain.action
                )
            )
            {
                error =
                    "The AI did not specify the terrain action.";

                return false;
            }

            return true;
        }


        // TREES

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

            if (
                string.IsNullOrWhiteSpace(
                    command.trees.action
                )
            )
            {
                error =
                    "The AI did not specify the tree action.";

                return false;
            }

            return true;
        }


        // WATER

        if (
            commandType == "water" ||
            commandType == "river" ||
            commandType == "lake"
        )
        {
            if (command.water == null)
            {
                error =
                    "The AI identified a water action " +
                    "but did not provide water settings.";

                return false;
            }

            if (
                string.IsNullOrWhiteSpace(
                    command.water.action
                )
            )
            {
                error =
                    "The AI did not specify the water action.";

                return false;
            }

            return true;
        }


        // BUILDINGS

        if (
            commandType == "building" ||
            commandType == "buildings" ||
            commandType == "building_generation"
        )
        {
            if (command.building == null)
            {
                error =
                    "The AI identified a building action " +
                    "but did not provide building settings.";

                return false;
            }

            if (
                string.IsNullOrWhiteSpace(
                    command.building.action
                )
            )
            {
                error =
                    "The AI did not specify the building action.";

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
    // ADVICE
    // =========================================================

    private void HandleAdvice(
        LandscapeCommand command
    )
    {
        string message =
            command.assistantMessage;

        if (string.IsNullOrWhiteSpace(message))
        {
            message =
                command.reason;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message =
                "I can help you think about the design, but I need more information about what you want to achieve.";
        }


        // -----------------------------------------------------
        // REMEMBER ACTIONABLE RECOMMENDATION
        // -----------------------------------------------------

        if (
            command.suggestedCommand != null
        )
        {
            lastAdviceCommand =
                CloneCommand(
                    command.suggestedCommand
                );

            lastAdviceText =
                message;

            message +=
                "\n\n" +
                "If you want me to apply this recommendation, say \"do it\".";
        }
        else
        {
            lastAdviceCommand = null;
            lastAdviceText = "";
        }


        SetOutput(
            message
        );
    }


    // =========================================================
    // UNKNOWN
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
            "Please tell me more specifically what you want."
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
        hasPendingConfirmation = true;

        pendingCommand = command;

        pendingSpatialContext =
            spatialContext;

        string actionDescription =
            DescribeCommand(command);

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
    // DESCRIBE COMMAND
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


        // ADVICE

        if (type == "advice")
        {
            return
                "provide design advice";
        }


        // TERRAIN

        if (
            type == "terrain" ||
            type == "modify_terrain" ||
            type == "terrain_modify"
        )
        {
            if (command.terrain == null)
            {
                return "modify the existing terrain";
            }

            string action =
                command.terrain.action
                    .Trim()
                    .ToLowerInvariant();

            int count =
                Mathf.Max(
                    command.terrain.count,
                    1
                );


            if (
                action == "mountain" ||
                action == "mountains" ||
                action == "create_mountain" ||
                action == "create_mountains"
            )
            {
                return
                    "create " +
                    count +
                    " mountain(s) on the existing terrain";
            }


            if (
                action == "modify_mountain" ||
                action == "modify_selected_mountain" ||
                action == "mountain_modify"
            )
            {
                if (
                    command.terrain.heightChange >
                    0.01f
                )
                {
                    return
                        "make the pointed mountain taller by approximately " +
                        command.terrain.heightChange.ToString("F1") +
                        " metres";
                }

                if (
                    command.terrain.heightChange <
                    -0.01f
                )
                {
                    return
                        "make the pointed mountain shorter by approximately " +
                        Mathf.Abs(
                            command.terrain.heightChange
                        ).ToString("F1") +
                        " metres";
                }

                return
                    "modify the pointed mountain";
            }


            if (
                action == "reset" ||
                action == "reset_terrain"
            )
            {
                return
                    "restore the existing terrain to its original shape";
            }


            return
                "modify the existing terrain";
        }


        // TREES

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
                return
                    "remove trees from the selected area";
            }


            if (
                action == "remove_all_trees" ||
                action == "clear_trees"
            )
            {
                return
                    "remove all trees";
            }


            return
                "perform tree action: " +
                action;
        }


        // WATER

        if (
            type == "water" ||
            type == "river" ||
            type == "lake"
        )
        {
            if (command.water == null)
            {
                return "perform a water operation";
            }

            string action =
                command.water.action
                    .Trim()
                    .ToLowerInvariant();


            if (
                action == "create_river" ||
                action == "river"
            )
            {
                return
                    "create a river at a random safe nearby location";
            }


            if (
                action == "create_lake" ||
                action == "lake"
            )
            {
                return
                    "create a lake at a random safe nearby location";
            }


            if (
                action == "delete" ||
                action == "delete_water" ||
                action == "remove_water" ||
                action == "clear_water"
            )
            {
                return
                    "remove the generated water and restore the terrain";
            }


            return
                "perform water action: " +
                action;
        }


        // BUILDINGS

        if (
            type == "building" ||
            type == "buildings" ||
            type == "building_generation"
        )
        {
            if (command.building == null)
            {
                return "perform a building operation";
            }

            string action =
                command.building.action
                    .Trim()
                    .ToLowerInvariant();

            int count =
                Mathf.Max(
                    command.building.count,
                    1
                );


            if (
                action == "create_house" ||
                action == "create_houses" ||
                action == "house" ||
                action == "houses"
            )
            {
                return
                    "create " +
                    count +
                    " house(s) on the existing terrain";
            }


            if (
                action == "create_temple" ||
                action == "create_temples" ||
                action == "temple" ||
                action == "temples"
            )
            {
                return
                    "create " +
                    count +
                    " temple(s) on the existing terrain";
            }


            if (
                action == "create_random" ||
                action == "create_random_building" ||
                action == "random_building"
            )
            {
                return
                    "create " +
                    count +
                    " random building(s)";
            }


            if (
                action == "clear" ||
                action == "clear_buildings" ||
                action == "remove_all_buildings"
            )
            {
                return
                    "remove all generated buildings";
            }


            return
                "perform building action: " +
                action;
        }


        return "perform the requested action";
    }


    // =========================================================
    // EXECUTE COMMAND
    // =========================================================

    private bool ExecuteValidatedCommand(
        LandscapeCommand command,
        SpatialContext spatialContext
    )
    {
        if (command == null)
        {
            return false;
        }

        string commandType =
            command.commandType
                .Trim()
                .ToLowerInvariant();


        if (commandType == "advice")
        {
            HandleAdvice(command);
            return true;
        }


        if (
            commandType == "terrain" ||
            commandType == "modify_terrain" ||
            commandType == "terrain_modify"
        )
        {
            return ProcessTerrainCommand(
                command,
                spatialContext
            );
        }


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


        if (
            commandType == "building" ||
            commandType == "buildings" ||
            commandType == "building_generation"
        )
        {
            return ProcessBuildingCommand(command);
        }


        // WATER

        if (
            commandType == "water" ||
            commandType == "river" ||
            commandType == "lake"
        )
        {
            return ProcessWaterCommand(command);
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
        LandscapeCommand command,
        SpatialContext spatialContext
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
            return false;
        }

        string action =
            command.terrain.action
                .Trim()
                .ToLowerInvariant();


        // -----------------------------------------------------
        // CREATE MOUNTAINS
        // -----------------------------------------------------

        int count =
            Mathf.Clamp(
                command.terrain.count <= 0
                    ? 1
                    : command.terrain.count,
                1,
                20
            );


        if (
            action == "mountain" ||
            action == "mountains" ||
            action == "create_mountain" ||
            action == "create_mountains"
        )
        {
            terrainGenerator.CreateRandomMountains(
                count
            );

            SetOutput(
                "Created " +
                count +
                " mountain(s) on the existing terrain."
            );

            return true;
        }
        if(
            action == "mountain_range" 
           
        )
        {
            terrainGenerator.CreateRandomMountainRange();

            SetOutput(
                "Created a range of mountain on the existing terrain."
            );

            return true;
        }

        // -----------------------------------------------------
        // RESET
        // -----------------------------------------------------

        if (
            action == "reset" ||
            action == "reset_terrain"
        )
        {
            terrainGenerator.ResetTerrain();

            SetOutput(
                "The existing terrain has been restored."
            );

            return true;
        }


        // -----------------------------------------------------
        // MOUNTAIN MODIFICATION — POSITION ONLY
        // -----------------------------------------------------

        if (
            action == "modify_mountain" ||
            action == "modify_selected_mountain" ||
            action == "mountain_modify"
        )
        {
            if (!spatialContext.hasGroundPosition)
            {
                SetOutput(
                    "Please point at the mountain first."
                );

                return false;
            }

            float heightChange =
                command.terrain.heightChange;

            if (Mathf.Abs(heightChange) < 0.01f)
            {
                SetOutput(
                    "Please say whether you want the pointed mountain taller or shorter."
                );

                return false;
            }

            bool success =
                terrainGenerator.ModifyMountainAtWorldPosition(
                    spatialContext.groundPosition,
                    Mathf.Clamp(
                        heightChange,
                        -20f,
                        20f
                    )
                );

            if (!success)
            {
                SetOutput(
                    "I could not modify the mountain at the pointed position."
                );

                return false;
            }

            SetOutput(
                heightChange > 0f
                    ? "The pointed mountain was made taller."
                    : "The pointed mountain was made shorter."
            );

            return true;
        }


        SetOutput(
            "Unknown terrain action: " +
            action
        );

        return false;
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
            return false;
        }

        TreeCommand tree =
            command.trees;

        string action =
            tree.action
                .Trim()
                .ToLowerInvariant();


        // MOVE TREE

        if (action == "move_tree")
        {
            return ProcessMoveTreeCommand(
                spatialContext
            );
        }


        // PLANT

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


        // REMOVE

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


        // REMOVE ALL

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
        if (!spatialContext.hasSelectedTree)
        {
            SetOutput(
                "Please point at a tree first."
            );

            return false;
        }

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
    // BUILDINGS
    // =========================================================

    private bool ProcessBuildingCommand(
        LandscapeCommand command
    )
    {
        if (buildingGenerator == null)
        {
            SetOutput(
                "BuildingGenerator is not assigned."
            );

            return false;
        }

        if (command.building == null)
        {
            return false;
        }

        BuildingCommand building =
            command.building;

        string action =
            building.action
                .Trim()
                .ToLowerInvariant();

        int count =
            Mathf.Clamp(
                building.count <= 0
                    ? 1
                    : building.count,
                1,
                100
            );


        // HOUSE

        if (
            action == "house" ||
            action == "houses" ||
            action == "create_house" ||
            action == "create_houses"
        )
        {
            int created = 0;

            for (
                int i = 0;
                i < count;
                i++
            )
            {
                int before =
                    buildingGenerator.GetBuildingCount();

                buildingGenerator.CreateRandomHouse();

                int after =
                    buildingGenerator.GetBuildingCount();

                if (after > before)
                {
                    created++;
                }
            }

            if (created == 0)
            {
                SetOutput(
                    "I could not create any houses."
                );

                return false;
            }

            SetOutput(
                "Created " +
                created +
                " house(s)."
            );

            return true;
        }


        // TEMPLE

        if (
            action == "temple" ||
            action == "temples" ||
            action == "create_temple" ||
            action == "create_temples"
        )
        {
            int created = 0;

            for (
                int i = 0;
                i < count;
                i++
            )
            {
                int before =
                    buildingGenerator.GetBuildingCount();

                buildingGenerator.CreateRandomTemple();

                int after =
                    buildingGenerator.GetBuildingCount();

                if (after > before)
                {
                    created++;
                }
            }

            if (created == 0)
            {
                SetOutput(
                    "I could not create any temples."
                );

                return false;
            }

            SetOutput(
                "Created " +
                created +
                " temple(s)."
            );

            return true;
        }


        // RANDOM BUILDING

        if (
            action == "random" ||
            action == "create_random" ||
            action == "create_random_building" ||
            action == "random_building"
        )
        {
            int created = 0;

            for (
                int i = 0;
                i < count;
                i++
            )
            {
                int before =
                    buildingGenerator.GetBuildingCount();

                buildingGenerator.CreateRandomBuilding();

                int after =
                    buildingGenerator.GetBuildingCount();

                if (after > before)
                {
                    created++;
                }
            }

            if (created == 0)
            {
                SetOutput(
                    "I could not create any random buildings."
                );

                return false;
            }

            SetOutput(
                "Created " +
                created +
                " random building(s)."
            );

            return true;
        }


        // MULTIPLE RANDOM BUILDINGS

        if (
            action == "create_random_buildings" ||
            action == "random_buildings"
        )
        {
            int before =
                buildingGenerator.GetBuildingCount();

            buildingGenerator.CreateRandomBuildings(
                count
            );

            int after =
                buildingGenerator.GetBuildingCount();

            int created =
                Mathf.Max(
                    after - before,
                    0
                );

            if (created == 0)
            {
                SetOutput(
                    "No buildings were created."
                );

                return false;
            }

            SetOutput(
                "Created " +
                created +
                " random building(s)."
            );

            return true;
        }


        // CLEAR

        if (
            action == "clear" ||
            action == "clear_buildings" ||
            action == "remove_all_buildings" ||
            action == "remove_buildings"
        )
        {
            int previousCount =
                buildingGenerator.GetBuildingCount();

            buildingGenerator.ClearBuildings();

            SetOutput(
                "Removed " +
                previousCount +
                " generated building(s)."
            );

            return true;
        }


        SetOutput(
            "Unknown building action: " +
            action
        );

        return false;
    }


    // =========================================================
    // WATER
    // =========================================================

    private bool ProcessWaterCommand(
        LandscapeCommand command
    )
    {
        if (waterManager == null)
        {
            SetOutput(
                "WaterManager is not assigned."
            );

            return false;
        }

        if (command.water == null)
        {
            SetOutput(
                "No water command was provided."
            );

            return false;
        }

        string action =
            command.water.action
                .Trim()
                .ToLowerInvariant();


        if (
            action == "create_river" ||
            action == "river"
        )
        {
            waterManager.CreateRiver();

            SetOutput(
                "Created a river at a random safe nearby location."
            );

            return true;
        }


        if (
            action == "create_lake" ||
            action == "lake"
        )
        {
            waterManager.CreateLake();

            SetOutput(
                "Created a lake at a random safe nearby location."
            );

            return true;
        }


        if (
            action == "delete" ||
            action == "delete_water" ||
            action == "remove_water" ||
            action == "clear_water"
        )
        {
            waterManager.DeleteWater();

            SetOutput(
                "Removed the generated water and restored the terrain."
            );

            return true;
        }


        SetOutput(
            "Unknown water action: " +
            action
        );

        return false;
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

        if (string.IsNullOrWhiteSpace(result))
        {
            return "Default";
        }

        return
            char.ToUpper(result[0]) +
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
                "\"advice\""
            )
        )
        {
            return "advice";
        }

        if (
            lower.Contains(
                "\"water\""
            )
        )
        {
            return "water";
        }

        if (
            lower.Contains(
                "\"building\""
            )
        )
        {
            return "building";
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

        return "";
    }


    // =========================================================
    // SYSTEM PROMPT
    // =========================================================

    private string BuildSystemPrompt()
    {
        return
            "You are an interactive 3D environment design assistant inside a Unity application.\n\n" +

            "Your role is NOT simply to match keywords to commands.\n" +
            "Your job is to understand what the user is trying to accomplish, interpret natural language using the Unity spatial context and conversation context, determine whether Unity can perform the requested action, and then either provide a supported Unity command, provide useful design advice, or clearly explain why the request cannot be performed.\n\n" +

            "IMPORTANT ARCHITECTURE:\n" +
            "You decide WHAT the user wants.\n" +
            "You may provide DESIGN ADVICE.\n" +
            "Unity decides HOW the physical environment is changed.\n" +
            "Do NOT calculate terrain geometry yourself.\n" +
            "Do NOT invent coordinates.\n" +
            "Do NOT invent object IDs.\n" +
            "Do NOT pretend that an action has already happened.\n\n" +


           

            "CONVERSATION:\n" +
            "You are allowed to use recent conversation context to understand follow-up requests.\n" +
            "The current Unity spatial context is authoritative.\n" +
            "Never allow an old conversation coordinate or old object reference to override current Unity spatial context.\n" +
            "The user may refer to previous advice using phrases such as:\n" +
            "\"do it\"\n" +
            "\"do that\"\n" +
            "\"apply that\"\n" +
            "\"go ahead\"\n" +
            "\"do it too\"\n" +
            "Use the previous advice when the reference is clear.\n\n" +


            

            "You control FOUR Unity environment systems:\n" +
            "1. Existing Terrain and Mountains\n" +
            "2. Trees\n" +
            "3. Buildings\n" +
            "4. Water\n\n" +


           

            "INTENT UNDERSTANDING:\n" +
            "First determine the user's intent.\n" +
            "The user may be asking you to:\n" +
            "- create something\n" +
            "- modify something\n" +
            "- remove something\n" +
            "- move something\n" +
            "- restore something\n" +
            "- ask for design advice\n" +
            "- ask what would look good\n" +
            "- ask whether something is possible\n" +
            "- ask an ambiguous question that requires clarification\n" +
            "- ask you to apply a recommendation from earlier in the conversation\n\n" +

            "Do not treat every sentence as an instruction to modify the environment.\n\n" +


            

            "DESIGN ADVICE:\n" +
            "If the user asks for suggestions, recommendations, opinions, explanations, or design guidance, use commandType = \"advice\".\n" +
            "Do NOT modify the Unity environment merely because the user asks for advice.\n\n" +

            "When useful, advice SHOULD include ONE PRIMARY ACTIONABLE RECOMMENDATION.\n" +
            "Put the human-readable recommendation in assistantMessage.\n" +
            "Put the executable recommendation in suggestedCommand.\n" +
            "The suggestedCommand must be a normal supported Unity command.\n\n" +

            "For example, if the user asks:\n" +
            "\"How can I improve this area?\"\n\n" +
            "you may answer with advice such as:\n" +
            "\"Adding a small group of trees would make the area feel more natural.\"\n\n" +
            "and provide a suggestedCommand that plants trees.\n\n" +

            "The suggestedCommand is OPTIONAL.\n" +
            "Use it when your advice naturally leads to one clear supported action.\n" +
            "Do NOT invent a suggestedCommand for unsupported functionality.\n\n" +

            "IMPORTANT:\n" +
            "Give ONE primary executable recommendation, not a list of multiple executable commands.\n" +
            "The current command protocol executes one command at a time.\n\n" +


         

            "APPLYING ADVICE:\n" +
            "If the user says \"do it\", \"do that\", \"apply it\", \"go ahead\", or similar and the previous assistant advice contained a suggestedCommand, the user is asking you to apply that previous recommendation.\n" +
            "Return the actual executable command rather than another advice response.\n" +
            "Use the previous recommendation as the intended action.\n\n" +


            

            "CLARIFICATION:\n" +
            "If the user request is genuinely ambiguous, do not guess.\n" +
            "Use commandType = \"unknown\".\n" +
            "Use the reason field to explain what information is missing.\n" +
            "Ask the user to clarify in a natural way.\n\n" +


            // =================================================
            // TERRAIN
            // =================================================

            "IMPORTANT TERRAIN RULE:\n" +
            "There is ALREADY a Unity Terrain in the scene.\n" +
            "NEVER create a new Unity Terrain.\n" +
            "NEVER replace the existing Terrain.\n" +
            "NEVER delete the existing Terrain.\n" +
            "Terrain commands modify the EXISTING Terrain.\n\n" +


            // =================================================
            // MOUNTAIN POINTING
            // =================================================

            "IMPORTANT MOUNTAIN POINTING RULE:\n" +
            "Mountains exist on the existing Unity Terrain.\n" +
            "Unity provides the CURRENT pointed Terrain ground position.\n" +
            "The CURRENT pointed world position is the ONLY reference used to identify the mountain for modification.\n" +
            "This works for BOTH mountains that already existed on the Terrain and mountains generated by Unity.\n" +
            "Mountain IDs are NOT required and are NOT used for mountain modification.\n" +
            "If the user says 'this mountain', use the CURRENT pointed Terrain ground position.\n" +
            "If the user says 'this mountain is too short', modify the mountain at the CURRENT pointed Terrain ground position.\n" +
            "If the user says 'make this mountain taller', modify the mountain at the CURRENT pointed Terrain ground position.\n" +
            "If the user says 'make this mountain shorter', modify the mountain at the CURRENT pointed Terrain ground position.\n" +
            "If the user asks to change the height of the pointed mountain, use the CURRENT pointed Terrain ground position.\n" +
            "If Unity did NOT provide a current pointed Terrain ground position, do not invent one.\n\n" +


            // =================================================
            // TREE POINTING
            // =================================================

            "TREE POINTING:\n" +
            "Unity may provide a selected tree.\n" +
            "Unity may provide a pointed ground position.\n" +
            "If the user says 'this tree', use the selected tree.\n" +
            "If the user says 'over there', use the pointed ground position.\n" +
            "NEVER invent a tree ID.\n" +
            "NEVER invent movement coordinates.\n\n" +


            // =================================================
            // WATER
            // =================================================

            "WATER SYSTEM:\n" +
            "You can control water using commandType = \"water\".\n" +
            "Allowed actions:\n" +
            "- create_river\n" +
            "- create_lake\n" +
            "- delete\n\n" +

            "When the user asks to create a river or lake:\n" +
            "- Do NOT ask the user to point.\n" +
            "- Do NOT generate coordinates.\n" +
            "- Do NOT use the pointing position.\n" +
            "- WaterManager automatically chooses a random nearby safe location.\n" +
            "- WaterManager avoids mountains and steep terrain.\n" +
            "- Water may be close to mountains, but should not overlap mountains.\n\n" +

            "IMPORTANT WATER COMMAND RULE:\n" +
            "The water command contains ONLY the action.\n" +
            "Do NOT add centerX.\n" +
            "Do NOT add centerZ.\n" +
            "Do NOT add coordinates or pointing positions to the water command.\n\n" +


            // =================================================
            // BUILDINGS
            // =================================================

            "IMPORTANT BUILDING RULE:\n" +
            "BuildingGenerator creates buildings on the existing terrain.\n" +
            "BuildingGenerator controls valid placement, terrain height, slope, spacing and rotation.\n" +
            "Do NOT invent building coordinates or IDs.\n\n" +


            // =================================================
            // CAPABILITY
            // =================================================

            "CAPABILITY AWARENESS:\n" +
            "Only promise functionality represented by the supported commands below.\n" +
            "The current system can create and modify mountains, plant/remove/move trees, create/remove buildings, and create/remove rivers and lakes.\n" +
            "The system does NOT currently provide arbitrary procedural generation of every possible object.\n" +
            "For example, do not pretend that you can directly create a waterfall, road, bridge, fence, road network, animal, vehicle, or arbitrary custom object unless an available command explicitly supports it.\n" +
            "If a request cannot be represented by the available commands, use commandType = \"unknown\" and explain the limitation.\n" +
            "Do not hallucinate new Unity functions or systems.\n\n" +


            // =================================================
            // CONFIDENCE
            // =================================================

            "CONFIDENCE:\n" +
            "Every command MUST contain:\n" +
            "\"confidence\": a number from 0.0 to 1.0\n" +
            "\"reason\": a short explanation.\n" +
            "0.80 to 1.00 = high confidence.\n" +
            "0.50 to 0.79 = medium confidence.\n" +
            "Below 0.50 = low confidence.\n" +
            "Do NOT artificially increase confidence.\n\n" +


            // =================================================
            // UNKNOWN
            // =================================================

            "UNKNOWN:\n" +
            "If the request is unclear or unsupported, return:\n" +
            "{\n" +
            "  \"commandType\":\"unknown\",\n" +
            "  \"confidence\":0.20,\n" +
            "  \"reason\":\"Explain clearly why the request cannot currently be performed or what clarification is required.\"\n" +
            "}\n\n" +


            // =================================================
            // TERRAIN COMMANDS
            // =================================================

            "TERRAIN:\n" +
            "Use commandType = \"terrain\".\n" +
            "The terrain already exists.\n\n" +

            "Supported terrain actions:\n" +
            "mountain\n" +
            "mountains\n" +
            "modify_mountain\n" +
            "reset\n\n" +

            "\"create a mountain\" -> mountain\n" +
            "\"create mountains\" -> mountains\n" +
            "\"create a range of mountain\" -> moutain_range\n"+
            "\"make the terrain mountainous\" -> mountain\n\n" +

            "If the user refers to a pointed mountain:\n" +
            "\"make this mountain taller\" -> modify_mountain\n" +
            "\"make this mountain shorter\" -> modify_mountain\n" +
            "\"change this mountain\" -> modify_mountain\n" +
            "\"modify this mountain\" -> modify_mountain\n\n" +

            "For mountain height changes, use heightChange:\n" +
            "taller / higher / raise -> positive heightChange, normally 5\n" +
            "shorter / lower / reduce -> negative heightChange, normally -5\n\n" +


            // =================================================
            // TREES
            // =================================================

            "TREES:\n" +
            "Use commandType = \"trees\".\n\n" +

            "Supported actions:\n" +
            "plant_trees\n" +
            "remove_trees\n" +
            "remove_all_trees\n" +
            "move_tree\n\n" +

            "Tree types may include:\n" +
            "Oak\n" +
            "Pine\n" +
            "Palm\n" +
            "Birch\n" +
            "Maple\n" +
            "Eucalyptus\n" +
            "Apple\n" +
            "mixed\n\n" +


          

            "TREE PLANTING:\n" +
            "The tree planting command uses centerX, centerZ, radius and spacing.\n" +
            "These are implementation parameters required by Unity.\n" +
            "Do not use imaginary spatial coordinates from the conversation.\n" +
            "If the user gives explicit numerical coordinates, they may be used as requested.\n" +
            "Otherwise use reasonable defaults for a general planting request.\n\n" +


            // =================================================
            // MOVE TREE
            // =================================================

            "MOVE TREE:\n" +
            "If the user says:\n" +
            "\"move this tree over there\"\n" +
            "\"put this tree there\"\n" +
            "\"move it over there\"\n\n" +

            "and Unity provides a selected tree and ground position, return:\n" +
            "{\n" +
            "  \"commandType\":\"trees\",\n" +
            "  \"confidence\":0.95,\n" +
            "  \"reason\":\"The user requested moving the selected tree to the pointed location.\",\n" +
            "  \"trees\":{\n" +
            "    \"action\":\"move_tree\",\n" +
            "    \"treeType\":\"\"\n" +
            "  }\n" +
            "}\n\n" +


            // =================================================
            // BUILDINGS
            // =================================================

            "BUILDINGS:\n" +
            "Use commandType = \"building\".\n\n" +

            "Supported building types:\n" +
            "House\n" +
            "Temple\n" +
            "Random building\n\n" +

            "BuildingGenerator chooses the physical location.\n" +
            "Do NOT invent coordinates.\n\n" +

            "\"create a house\" -> create_house\n" +
            "\"create 5 houses\" -> create_houses, count 5\n" +
            "\"create a temple\" -> create_temple\n" +
            "\"create 3 temples\" -> create_temples, count 3\n" +
            "\"create a random building\" -> create_random\n" +
            "\"create 10 random buildings\" -> create_random_buildings, count 10\n" +
            "\"remove all buildings\" -> clear_buildings\n\n" +


            // =================================================
            // WATER
            // =================================================

            "WATER EXAMPLES:\n" +

            "\"create a river\"\n" +
            "{\n" +
            "  \"commandType\":\"water\",\n" +
            "  \"confidence\":1.0,\n" +
            "  \"reason\":\"User requested a river.\",\n" +
            "  \"water\":{\n" +
            "    \"action\":\"create_river\"\n" +
            "  }\n" +
            "}\n\n" +

            "\"make a lake\"\n" +
            "{\n" +
            "  \"commandType\":\"water\",\n" +
            "  \"confidence\":1.0,\n" +
            "  \"reason\":\"User requested a lake.\",\n" +
            "  \"water\":{\n" +
            "    \"action\":\"create_lake\"\n" +
            "  }\n" +
            "}\n\n" +


            // =================================================
            // ADVICE EXAMPLE
            // =================================================

            "ADVICE EXAMPLE:\n" +
            "If the user asks:\n" +
            "\"What would look good around this mountain?\"\n\n" +

            "Return an advice response similar to:\n" +
            "{\n" +
            "  \"commandType\":\"advice\",\n" +
            "  \"confidence\":0.90,\n" +
            "  \"reason\":\"The user is asking for design advice rather than requesting an immediate modification.\",\n" +
            "  \"assistantMessage\":\"A natural-looking approach would be to add a small group of trees around the lower slopes while leaving some open space near the mountain.\",\n" +
            "  \"suggestedCommand\":{\n" +
            "    \"commandType\":\"trees\",\n" +
            "    \"confidence\":0.95,\n" +
            "    \"reason\":\"This recommendation can be implemented by the Unity tree system.\",\n" +
            "    \"trees\":{\n" +
            "      \"action\":\"plant_trees\",\n" +
            "      \"treeType\":\"mixed\",\n" +
            "      \"count\":20,\n" +
            "      \"centerX\":0,\n" +
            "      \"centerZ\":0,\n" +
            "      \"radius\":15,\n" +
            "      \"spacing\":3\n" +
            "    }\n" +
            "  }\n" +
            "}\n\n" +


            // =================================================
            // APPLY ADVICE EXAMPLE
            // =================================================

            "APPLY ADVICE EXAMPLE:\n" +
            "If the previous advice recommended planting trees and the user says:\n" +
            "\"Do it\"\n" +
            "\"Do that\"\n" +
            "\"Apply it\"\n" +
            "\"Go ahead\"\n\n" +

            "return the actual executable trees command rather than commandType = advice.\n\n" +


            // =================================================
            // FINAL RULES
            // =================================================

            "FINAL RULES:\n" +
            "1. Return exactly ONE JSON command.\n" +
            "2. Return ONLY JSON.\n" +
            "3. Always include confidence.\n" +
            "4. Always include reason.\n" +
            "5. Never guess genuinely ambiguous requests.\n" +
            "6. Use unknown when necessary.\n" +
            "7. Never invent tree IDs.\n" +
            "8. Never invent mountain IDs.\n" +
            "9. Never invent movement coordinates.\n" +
            "10. Never invent building coordinates.\n" +
            "11. Never invent building IDs.\n" +
            "12. Never claim Unity performed an action.\n" +
            "13. Terrain means MODIFY THE EXISTING TERRAIN.\n" +
            "14. NEVER CREATE A NEW TERRAIN.\n" +
            "15. NEVER DELETE OR REPLACE THE EXISTING TERRAIN.\n" +
            "16. Unity owns tree IDs.\n" +
            "17. Unity owns physical object placement logic.\n" +
            "18. Use advice when the user asks for design guidance rather than an action.\n" +
            "19. Advice may contain ONE suggestedCommand when there is one clear supported recommendation.\n" +
            "20. If the user asks to apply previous advice, execute the previous supported recommendation.\n" +
            "21. Use unknown when the requested capability does not exist.\n" +
            "22. Do not invent functionality that Unity does not provide.\n" +
            "23. Interpret natural language rather than requiring exact command wording.\n";
    }


    // =========================================================
    // JSON EXTRACTION
    // =========================================================

    private string ExtractJson(string text)
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

    private void SetOutput(string message)
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

    private void SetButtonInteractable(bool value)
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
        TaskCompletionSource<bool> completionSource,
        bool success
    )
    {
        isGenerating = false;

        SetButtonInteractable(true);

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
        // TREE

        public bool hasSelectedTree;

        public int selectedTreeIndex;

        public string selectedTreeName;

        public string selectedTreeType;


        // CURRENT POINTED TERRAIN POSITION

        public bool hasGroundPosition;

        public Vector3 groundPosition;
    }


    // =========================================================
    // CONVERSATION TURN
    // =========================================================

    [Serializable]
    private class ConversationTurn
    {
        public string userMessage;

        public string assistantResult;
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

        // Used when commandType = advice.
        public string assistantMessage;

        // NEW:
        // The single primary actionable recommendation
        // associated with advice.
        public LandscapeCommand suggestedCommand;

        public TerrainCommand terrain;

        public TreeCommand trees;

        public BuildingCommand building;

        public WaterCommand water;
    }


    // =========================================================
    // WATER COMMAND
    // =========================================================

    [Serializable]
    private class WaterCommand
    {
        public string action;
    }


   

    [Serializable]
    private class TerrainCommand
    {
        public string action;

        public int count;

        // Positive = taller, negative = shorter.
        public float heightChange;
    }


    

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
    // BUILDING COMMAND
    // =========================================================

    [Serializable]
    private class BuildingCommand
    {
        public string action;

        public string buildingType;

        public int count;
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


    

    [Serializable]
    private class OllamaResponse
    {
        public OllamaMessage message;
    }


 

    [Serializable]
    private class OllamaMessage
    {
        public string role;

        public string content;
    }


   

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