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
    // =========================================================
    // OLLAMA
    // =========================================================

    [Header("Ollama")]

    [SerializeField]
    private string ollamaUrl = "http://localhost:11434/api/chat";

    [SerializeField]
    private string demoModel = "qwen3:4b";

    [Tooltip("Disable qwen3 'thinking' output so the reply contains only JSON.")]
    [SerializeField]
    private bool disableModelThinking = true;


    // =========================================================
    // TRANSCRIPT RULES (Round 2)
    // =========================================================

    [Header("Transcript Rules")]

    [Tooltip("Reject transcripts that contain no recognised command word, so misheard speech is not executed.")]
    [SerializeField]
    private bool requireCommandWord = true;

    [Tooltip("Create at the pointed position only when the request says 'here', 'there', 'this spot' or similar.")]
    [SerializeField]
    private bool pointedPositionOnlyWithDeixis = true;

    [Tooltip("Tree count used when the request gives no number (for example 'plant some trees').")]
    [SerializeField]
    private int defaultTreeCount = 10;


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
    // SYSTEMS
    // =========================================================

    [Header("Terrain")]

    [SerializeField]
    private TerrainGenerator terrainGenerator;

    [Header("Trees")]

    [SerializeField]
    private TreeManager treeManager;

    [Header("Water")]

    [SerializeField]
    private WaterManager waterManager;


    // =========================================================
    // XR POINTING
    // =========================================================

    [Header("XR Pointing")]

    [SerializeField]
    private WaterPointingDetector waterPointingDetector;

    [SerializeField]
    private TreePointingDetector treePointingDetector;

    [SerializeField]
    private MountainPointingDetector mountainPointingDetector;


    // =========================================================
    // CONFIDENCE
    // =========================================================

    [Header("AI Confidence")]

    [Tooltip("At or above this value a command is executed without comment.")]
    [SerializeField]
    private float highConfidenceThreshold = 0.80f;

    [Tooltip("Below this value a command is rejected and the user is asked to rephrase.")]
    [SerializeField]
    private float mediumConfidenceThreshold = 0.50f;


    // =========================================================
    // HELP / ADVICE
    // =========================================================

    [Header("Help and Advice")]

    [Tooltip("Answer help questions locally instead of sending them to the AI.")]
    [SerializeField]
    private bool enableHelpAndAdvice = true;

    [Tooltip("Include suggestions for the object the user is currently pointing at.")]
    [SerializeField]
    private bool showContextualHelpForWhatCanIDoHere = true;


    // =========================================================
    // DEFAULTS
    // =========================================================

    [Header("Defaults")]

    [SerializeField]
    private float defaultTreeRemovalRadius = 10f;

    [SerializeField]
    private float selectedTreeRemovalRadius = 0.25f;


    // =========================================================
    // STATE
    // =========================================================

    private bool isGenerating = false;

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
            sendButton.onClick.AddListener(SendCurrentPrompt);

        if (terrainGenerator == null)
            terrainGenerator = FindFirstObjectByType<TerrainGenerator>();

        if (treeManager == null)
            treeManager = FindFirstObjectByType<TreeManager>();

        if (waterManager == null)
            waterManager = FindFirstObjectByType<WaterManager>();

        if (waterPointingDetector == null)
            waterPointingDetector = FindFirstObjectByType<WaterPointingDetector>();

        if (treePointingDetector == null)
            treePointingDetector = FindFirstObjectByType<TreePointingDetector>();

        if (mountainPointingDetector == null)
            mountainPointingDetector = FindFirstObjectByType<MountainPointingDetector>();

        SetOutput(
            "You can create mountains, rivers, lakes and trees.\n\n" +
            (enableHelpAndAdvice
                ? "Try asking: \"What can I do here?\""
                : "Try: \"Create a mountain and a lake\"")
        );
    }


    // =========================================================
    // SEND BUTTON
    // =========================================================

    public void SendCurrentPrompt()
    {
        if (inputField == null)
        {
            Debug.LogError("[DemoChat] InputField is not assigned.");
            return;
        }

        string prompt = inputField.text.Trim();

        if (string.IsNullOrWhiteSpace(prompt))
        {
            SetOutput("Please describe what you want.");
            return;
        }

        SendPrompt(prompt);
    }


    // =========================================================
    // SEND PROMPT (UI)
    // =========================================================

    public void SendPrompt(string userPrompt)
    {
        // Evaluation logging: record exactly what was typed.
        Debug.Log("[DemoChat] TRANSCRIPT (typed): " + userPrompt);

        if (string.IsNullOrWhiteSpace(userPrompt))
            return;

        userPrompt = userPrompt.Trim();

        if (isGenerating)
        {
            SetOutput("AI is already processing a request.");
            return;
        }

        if (!IsUsableTranscript(userPrompt))
        {
            SetOutput("I could not clearly understand the request. Please try again.");
            return;
        }

        SpatialContext spatialContext = CaptureSpatialContext();

        if (TryHandleHelpRequest(userPrompt, spatialContext))
            return;

        StartCoroutine(SendToOllama(userPrompt, spatialContext, null));
    }


    // =========================================================
    // ASYNC ASK (VOICE)
    // =========================================================

    public async Task Ask(string userPrompt)
    {
        // Evaluation logging: record exactly what Whisper heard.
        Debug.Log("[DemoChat] TRANSCRIPT (voice): " + userPrompt);

        if (string.IsNullOrWhiteSpace(userPrompt))
            return;

        if (isGenerating)
        {
            Debug.Log("[DemoChat] AI is already processing a request. Ignoring new voice request.");
            return;
        }

        userPrompt = userPrompt.Trim();

        if (!IsUsableTranscript(userPrompt))
        {
            SetOutput("I could not clearly understand the request. Please try again.");
            return;
        }

        SpatialContext spatialContext = CaptureSpatialContext();

        if (TryHandleHelpRequest(userPrompt, spatialContext))
            return;

        TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();

        StartCoroutine(SendToOllama(userPrompt, spatialContext, completionSource));

        await completionSource.Task;
    }


    // =========================================================
    // HELP / ADVICE
    // =========================================================

    private static readonly Regex HelpPattern = new Regex(
        @"^(help|help me|what can i do( here)?|what can you do|what can i say|" +
        @"what can i make( here)?|what can i create( here)?|how does this work|" +
        @"what are my options|what should i do)( now| here)?$",
        RegexOptions.Compiled
    );

    private bool TryHandleHelpRequest(string userPrompt, SpatialContext context)
    {
        if (!enableHelpAndAdvice)
            return false;

        string normalized = NormalizeForIntent(userPrompt);

        if (!HelpPattern.IsMatch(normalized))
            return false;

        SetOutput(BuildHelpText(context));
        return true;
    }

    private string BuildHelpText(SpatialContext context)
    {
        StringBuilder builder = new StringBuilder();

        if (showContextualHelpForWhatCanIDoHere)
        {
            List<string> contextual = new List<string>();

            if (context.hasSelectedTree)
            {
                contextual.Add(
                    "You are pointing at " + context.selectedTreeName + ":\n" +
                    "  • \"Move this tree over there\"\n" +
                    "  • \"Delete this tree\""
                );
            }

            if (context.hasSelectedMountain)
            {
                contextual.Add(
                    "You are pointing at " + context.selectedMountainName + ":\n" +
                    "  • \"Make this mountain taller\" / \"shorter\"\n" +
                    "  • \"Delete this mountain\""
                );
            }

            if (context.hasSelectedRiver)
            {
                contextual.Add(
                    "You are pointing at a river:\n" +
                    "  • \"Make this river wider\" / \"narrower\" / \"deeper\" / \"shallower\"\n" +
                    "  • \"Delete this river\""
                );
            }

            if (context.hasSelectedLake)
            {
                contextual.Add(
                    "You are pointing at a lake:\n" +
                    "  • \"Make this lake wider\" / \"longer\" / \"deeper\"\n" +
                    "  • \"Delete this lake\""
                );
            }

            if (context.hasGroundPosition)
            {
                contextual.Add(
                    "You are pointing at the ground:\n" +
                    "  • \"Create a mountain here\"\n" +
                    "  • \"Plant 10 palm trees here\"\n" +
                    "  • \"Remove the trees here\""
                );
            }

            if (contextual.Count > 0)
            {
                builder.AppendLine(string.Join("\n\n", contextual));
                builder.AppendLine();
                builder.AppendLine("You can also:");
            }
        }

        builder.AppendLine("  • \"Create a mountain\" or \"Create 3 mountains\"");
        builder.AppendLine("  • \"Create a river\" or \"Create a lake\"");
        builder.AppendLine("  • \"Plant 20 oak trees\" (Oak, Palm, Birch, Maple, Eucalyptus, Apple, mixed)");
        builder.AppendLine("  • \"Remove all trees\"");
        builder.AppendLine("  • \"Reset the terrain\"");
        builder.AppendLine();
        builder.Append("You can combine requests: \"Create a mountain, a lake and 20 trees\".");

        return builder.ToString();
    }


    // =========================================================
    // NORMALIZE INTENT
    // =========================================================

    private string NormalizeForIntent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        string normalized = text.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^\p{L}\p{N}\s]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ");

        return normalized.Trim();
    }


    // =========================================================
    // TRANSCRIPT RULES (Round 2)
    // =========================================================

    private static readonly Regex CommandWordPattern = new Regex(
        @"\b(create|make|build|add|plant|put|place|move|delete|remove|clear|reset|change|modify|edit|raise|lower|grow|" +
        @"mountains?|hills?|rivers?|lakes?|trees?|forest|water|taller|shorter|higher|wider|narrower|deeper|shallower|longer|" +
        @"help|what|how)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly Regex DeixisPattern = new Regex(
        @"\b(here|there|this (spot|place|area|location)|that (spot|place|area|location)|where i('m| am) pointing)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly Regex NumberPattern = new Regex(
        @"\d|\b(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|fifteen|twenty|thirty|forty|fifty|hundred|single)\b|\ban? (\w+ )?tree\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly Regex TreeTypePattern = new Regex(
        @"\b(oak|pine|palm|birch|maple|eucalyptus|apple|mixed)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private bool HasCommandWord(string transcript)
    {
        return CommandWordPattern.IsMatch(transcript ?? "");
    }

    // Returns the part of the request that mentions the given feature,
    // so that "plant 30 trees and create a mountain here" applies "here"
    // only to the mountain.
    private string ClauseFor(string transcript, string featurePattern)
    {
        string[] clauses = Regex.Split(transcript ?? "", @",|\band\b|\bthen\b", RegexOptions.IgnoreCase);

        foreach (string clause in clauses)
        {
            if (Regex.IsMatch(clause, featurePattern, RegexOptions.IgnoreCase))
                return clause;
        }

        return transcript ?? "";
    }

    private void ApplyTranscriptRules(List<LandscapeCommand> commands, string transcript)
    {
        foreach (LandscapeCommand command in commands)
        {
            if (command == null)
                continue;

            // ---------------- TERRAIN CREATION ----------------

            if (command.terrain != null && !string.IsNullOrWhiteSpace(command.terrain.action))
            {
                string action = command.terrain.action.Trim().ToLowerInvariant();
                string feature =
                    action.Contains("mountain") ? @"\bmountains?\b|\bhills?\b" :
                    action.Contains("river") ? @"\brivers?\b" :
                    action.Contains("lake") ? @"\blakes?\b" : null;

                bool isCreation = feature != null &&
                                  !action.Contains("modify") &&
                                  !action.Contains("delete") &&
                                  !action.Contains("remove");

                if (isCreation && pointedPositionOnlyWithDeixis && command.terrain.usePointedPosition &&
                    !DeixisPattern.IsMatch(ClauseFor(transcript, feature)))
                {
                    command.terrain.usePointedPosition = false;
                    Debug.Log("[DemoChat] RULE: no 'here/there' for " + action + ", so the pointed position is not used.");
                }
            }

            // ---------------- TREE PLANTING ----------------

            if (command.trees != null && !string.IsNullOrWhiteSpace(command.trees.action))
            {
                string action = command.trees.action.Trim().ToLowerInvariant();

                if (action == "plant" || action == "plant_trees" || action == "create_forest")
                {
                    string clause = ClauseFor(transcript, @"\btrees?\b|\bforest\b|\bplant");

                    if (pointedPositionOnlyWithDeixis && command.trees.usePointedPosition && !DeixisPattern.IsMatch(clause))
                    {
                        command.trees.usePointedPosition = false;
                        Debug.Log("[DemoChat] RULE: no 'here/there' for planting, so the pointed position is not used.");
                    }

                    if (!NumberPattern.IsMatch(clause) && command.trees.count != defaultTreeCount)
                    {
                        Debug.Log("[DemoChat] RULE: no number given, tree count " + command.trees.count + " -> " + defaultTreeCount + ".");
                        command.trees.count = defaultTreeCount;
                    }

                    if (!TreeTypePattern.IsMatch(clause) &&
                        !string.Equals(command.trees.treeType, "Mixed", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log("[DemoChat] RULE: no tree type given, '" + command.trees.treeType + "' -> Mixed.");
                        command.trees.treeType = "Mixed";
                    }
                }
            }
        }
    }


    // =========================================================
    // TRANSCRIPT VALIDATION
    // =========================================================

    private bool IsUsableTranscript(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return false;

        string text = transcript.Trim();

        if (text.Length < 2)
            return false;

        string withoutPunctuation = Regex.Replace(text, @"[\p{P}\p{S}\d]", "");

        if (string.IsNullOrWhiteSpace(withoutPunctuation))
            return false;

        if (text.Length >= 5 && Regex.IsMatch(text, @"^(.)\1{4,}$"))
            return false;

        // Round 2: misheard speech such as "Ayesha. Ayesha." contains no
        // command word and is not sent to the model.
        if (requireCommandWord && !HasCommandWord(text))
        {
            Debug.Log("[DemoChat] RULE: transcript has no command word, not executed: " + text);
            return false;
        }

        return true;
    }


    // =========================================================
    // CAPTURE SPATIAL CONTEXT
    // =========================================================

    private SpatialContext CaptureSpatialContext()
    {
        SpatialContext context = new SpatialContext();

        // ---------------- TREE + GROUND ----------------

        if (treePointingDetector != null)
        {
            TreeSelectable selectedTree = treePointingDetector.GetSelectedTree();

            if (selectedTree != null)
            {
                context.hasSelectedTree = true;
                context.selectedTreeIndex = selectedTree.treeIndex;
                context.selectedTreeName = selectedTree.GetTreeName();

                TreeTypeInfo typeInfo = selectedTree.GetComponent<TreeTypeInfo>();
                context.selectedTreeType = typeInfo != null ? typeInfo.GetTreeType() : "Unknown";
            }

            Vector3 groundPosition;

            if (treePointingDetector.TryGetGroundPosition(out groundPosition))
            {
                context.hasGroundPosition = true;
                context.groundPosition = groundPosition;
            }
        }

        // ---------------- MOUNTAIN ----------------

        if (mountainPointingDetector != null)
        {
            int mountainId;

            if (mountainPointingDetector.TryGetSelectedMountainId(out mountainId))
            {
                context.hasSelectedMountain = true;
                context.selectedMountainIndex = mountainId;

                TerrainGenerator.MountainInfo mountain = mountainPointingDetector.GetSelectedMountain();

                context.selectedMountainName =
                    mountain != null && !string.IsNullOrWhiteSpace(mountain.name)
                        ? mountain.name
                        : "the selected mountain";
            }
        }

        // ---------------- WATER ----------------

        if (waterPointingDetector != null)
        {
            int riverId = waterPointingDetector.GetSelectedRiverId();

            if (riverId > 0)
            {
                context.hasSelectedRiver = true;
                context.selectedRiverId = riverId;
            }

            int lakeId = waterPointingDetector.GetSelectedLakeId();

            if (lakeId > 0)
            {
                context.hasSelectedLake = true;
                context.selectedLakeId = lakeId;
            }
        }

        Debug.Log("[DemoChat] CONTEXT: tree=" + (context.hasSelectedTree ? context.selectedTreeIndex + " (" + context.selectedTreeType + ")" : "none") +
                  " | mountain=" + (context.hasSelectedMountain ? context.selectedMountainIndex.ToString() : "none") +
                  " | river=" + (context.hasSelectedRiver ? context.selectedRiverId.ToString() : "none") +
                  " | lake=" + (context.hasSelectedLake ? context.selectedLakeId.ToString() : "none") +
                  " | ground=" + (context.hasGroundPosition ? context.groundPosition.ToString("F1") : "none"));

        return context;
    }


    // =========================================================
    // SPATIAL CONTEXT TEXT (SENT TO THE AI)
    // =========================================================

    private string BuildSpatialContext(SpatialContext context)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("XR SPATIAL CONTEXT:");
        builder.AppendLine("This information comes directly from Unity.");
        builder.AppendLine();

        // Tree (no name or ID is sent to the LLM; Unity keeps the selection)
        if (context.hasSelectedTree)
        {
            builder.AppendLine("Selected tree: yes");
            builder.AppendLine("Selected tree type: " + context.selectedTreeType);
        }
        else
        {
            builder.AppendLine("Selected tree: none");
        }

        // Ground
        builder.AppendLine(
            "Pointing at terrain: " + (context.hasGroundPosition ? "true" : "false")
        );

        // Mountain
        builder.AppendLine(
            context.hasSelectedMountain
                ? "Selected mountain: yes"
                : "Selected mountain: none"
        );

        // Water
        builder.AppendLine(context.hasSelectedRiver ? "Selected river: yes" : "Selected river: none");
        builder.AppendLine(context.hasSelectedLake ? "Selected lake: yes" : "Selected lake: none");

        builder.AppendLine();
        builder.AppendLine("SPATIAL RULES:");
        builder.AppendLine("Unity owns all object IDs and all coordinates. Never put IDs or coordinates in the JSON.");
        builder.AppendLine("For 'this tree', 'this mountain', 'this river', 'this lake' or 'it', use the matching selected object.");
        builder.AppendLine("If the referenced object is 'none' above, return unknown.");
        builder.AppendLine("For 'here', 'there', 'over there' or 'where I am pointing', set usePointedPosition to true.");
        builder.AppendLine("If 'Pointing at terrain' is false and the user says 'here' or 'there', return unknown.");
        builder.AppendLine("The pointed ground position is never used to identify a mountain.");

        return builder.ToString();
    }


    // =========================================================
    // SEND TO OLLAMA
    // =========================================================

    private IEnumerator SendToOllama(
        string userPrompt,
        SpatialContext spatialContext,
        TaskCompletionSource<bool> completionSource
    )
    {
        while (isGenerating)
            yield return null;

        isGenerating = true;
        SetButtonInteractable(false);
        SetOutput("AI is thinking...");

        string userMessage =
            "USER REQUEST:\n" + userPrompt + "\n\n" + BuildSpatialContext(spatialContext);

        OllamaRequest request = new OllamaRequest
        {
            model = demoModel,
            stream = false,
            think = !disableModelThinking,
            format = "json",
            messages = new OllamaMessage[]
            {
                new OllamaMessage { role = "system", content = BuildSystemPrompt() },
                new OllamaMessage { role = "user", content = userMessage }
            }
        };

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));

        using (UnityWebRequest webRequest = new UnityWebRequest(ollamaUrl, "POST"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(body);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            // ---------------- CONNECTION ----------------

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                SetOutput("Could not connect to Ollama.\n\n" + webRequest.error);
                FinishRequest(completionSource, false);
                yield break;
            }

            // ---------------- RESPONSE ----------------

            OllamaResponse ollamaResponse = null;

            try
            {
                ollamaResponse = JsonUtility.FromJson<OllamaResponse>(webRequest.downloadHandler.text);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                SetOutput("Invalid response from Ollama.");
                FinishRequest(completionSource, false);
                yield break;
            }

            if (ollamaResponse == null || ollamaResponse.message == null)
            {
                SetOutput("Ollama returned no message.");
                FinishRequest(completionSource, false);
                yield break;
            }

            string llmContent = ollamaResponse.message.content;
            Debug.Log("[DemoChat] LLM:\n" + llmContent);

            // ---------------- JSON ----------------

            string commandJson = ExtractJson(llmContent);

            if (string.IsNullOrEmpty(commandJson))
            {
                SetOutput("I could not understand the AI response.");
                FinishRequest(completionSource, false);
                yield break;
            }

            List<LandscapeCommand> commands;
            string parseError;

            if (!TryParseCommands(commandJson, out commands, out parseError))
            {
                SetOutput(parseError);
                FinishRequest(completionSource, false);
                yield break;
            }

            if (commands == null || commands.Count == 0)
            {
                SetOutput("The AI returned no commands.");
                FinishRequest(completionSource, false);
                yield break;
            }

            Debug.Log("[DemoChat] Number of commands = " + commands.Count);

            // ---------------- UNKNOWN / UNCLEAR ----------------

            foreach (LandscapeCommand command in commands)
            {
                PrepareCommandType(command);

                if (IsUnknownType(command.commandType))
                {
                    HandleUnknownCommand(command);
                    FinishRequest(completionSource, false);
                    yield break;
                }
            }

            // ---------------- TRANSCRIPT RULES ----------------

            ApplyTranscriptRules(commands, userPrompt);

            // ---------------- VALIDATE ----------------

            List<string> validationErrors = new List<string>();

            for (int i = 0; i < commands.Count; i++)
            {
                string validationError;

                if (!ValidateCommand(commands[i], out validationError))
                {
                    validationErrors.Add(
                        (commands.Count > 1 ? "Command " + (i + 1) + ": " : "") + validationError
                    );
                }
            }

            if (validationErrors.Count > 0)
            {
                SetOutput(string.Join("\n", validationErrors));
                FinishRequest(completionSource, false);
                yield break;
            }

            // ---------------- CONFIDENCE ----------------

            bool anyMediumConfidence = false;

            foreach (LandscapeCommand command in commands)
            {
                ConfidenceLevel level = GetConfidenceLevel(command);

                if (level == ConfidenceLevel.Low)
                {
                    string reason = string.IsNullOrWhiteSpace(command.reason)
                        ? ""
                        : "\n\n(" + command.reason + ")";

                    SetOutput(
                        "I'm not sure I understood. Did you want to " +
                        DescribeCommand(command, spatialContext) +
                        "? Please rephrase the request more specifically." +
                        reason
                    );

                    FinishRequest(completionSource, false);
                    yield break;
                }

                if (level == ConfidenceLevel.Medium)
                    anyMediumConfidence = true;
            }

            // ---------------- EXECUTE ----------------

            List<string> successfulCommands = new List<string>();
            List<string> failedCommands = new List<string>();

            for (int i = 0; i < commands.Count; i++)
            {
                LandscapeCommand command = commands[i];

                // Describe before executing: execution may clear selections.
                string description = DescribeCommand(command, spatialContext);

                Debug.Log(
                    "[DemoChat] Executing command " + (i + 1) + "/" + commands.Count + ": " + description
                );

                if (ExecuteValidatedCommand(command, spatialContext))
                    successfulCommands.Add(description);
                else
                    failedCommands.Add(description);

                // Give Unity one frame between procedural generation steps.
                yield return null;
            }

            // ---------------- RESULT ----------------

            if (commands.Count == 1)
            {
                // On failure the Process* method has already written the reason.
                if (successfulCommands.Count == 1)
                {
                    SetOutput(
                        anyMediumConfidence
                            ? "I think you wanted to " + successfulCommands[0] + ". Done."
                            : "Done: " + successfulCommands[0] + "."
                    );
                }

                FinishRequest(completionSource, successfulCommands.Count == 1);
                yield break;
            }

            StringBuilder result = new StringBuilder();

            result.AppendLine(
                successfulCommands.Count == commands.Count
                    ? "Completed " + commands.Count + " requests:"
                    : "Completed " + successfulCommands.Count + " of " + commands.Count + " requests:"
            );

            foreach (string description in successfulCommands)
                result.AppendLine("• " + description);

            if (failedCommands.Count > 0)
            {
                result.AppendLine();
                result.AppendLine("Could not complete:");

                foreach (string description in failedCommands)
                    result.AppendLine("• " + description);
            }

            if (anyMediumConfidence)
            {
                result.AppendLine();
                result.AppendLine("Some of these were my best interpretation of your request.");
            }

            SetOutput(result.ToString().TrimEnd());
            FinishRequest(completionSource, failedCommands.Count == 0);
        }
    }


    // =========================================================
    // CONFIDENCE
    // =========================================================

    private enum ConfidenceLevel
    {
        High,
        Medium,
        Low
    }

    private ConfidenceLevel GetConfidenceLevel(LandscapeCommand command)
    {
        // JsonUtility leaves a missing field at 0. Treat "not provided" as
        // medium so a model that forgets the field is not blocked entirely.
        if (command.confidence <= 0f)
        {
            Debug.LogWarning("[DemoChat] Command has no confidence value; treating as medium.");
            return ConfidenceLevel.Medium;
        }

        if (command.confidence >= highConfidenceThreshold)
            return ConfidenceLevel.High;

        if (command.confidence >= mediumConfidenceThreshold)
            return ConfidenceLevel.Medium;

        return ConfidenceLevel.Low;
    }


    // =========================================================
    // UNKNOWN COMMAND
    // =========================================================

    private bool IsUnknownType(string commandType)
    {
        return commandType == "unknown" ||
               commandType == "unclear" ||
               commandType == "ambiguous";
    }

    private void HandleUnknownCommand(LandscapeCommand command)
    {
        string message = "I could not determine what you want me to do.";

        if (command != null && !string.IsNullOrWhiteSpace(command.reason))
            message = command.reason;

        if (enableHelpAndAdvice)
            message += "\n\nSay \"What can I do here?\" for suggestions.";

        SetOutput(message);
    }


    // =========================================================
    // PARSE ONE OR MANY COMMANDS
    // =========================================================

    private bool TryParseCommands(
        string commandJson,
        out List<LandscapeCommand> commands,
        out string error
    )
    {
        commands = new List<LandscapeCommand>();
        error = "";

        if (string.IsNullOrWhiteSpace(commandJson))
        {
            error = "The AI returned empty JSON.";
            return false;
        }

        // Multi-command wrapper
        try
        {
            MultiLandscapeCommand multi = JsonUtility.FromJson<MultiLandscapeCommand>(commandJson);

            if (multi != null && multi.commands != null && multi.commands.Length > 0)
            {
                foreach (LandscapeCommand command in multi.commands)
                {
                    if (command == null)
                    {
                        error = "The AI returned an empty command inside the multi-command request.";
                        return false;
                    }

                    commands.Add(command);
                }

                return true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[DemoChat] Multi-command parsing failed: " + exception.Message);
        }

        // Single command
        try
        {
            LandscapeCommand single = JsonUtility.FromJson<LandscapeCommand>(commandJson);

            if (single == null)
            {
                error = "The AI returned an empty command.";
                return false;
            }

            commands.Add(single);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(exception);
            error = "The AI command could not be parsed.";
            return false;
        }
    }


    // =========================================================
    // COMMAND TYPE
    // =========================================================

    private static bool IsTerrainType(string type)
    {
        return type == "terrain" ||
               type == "modify_terrain" ||
               type == "terrain_modify";
    }

    private static bool IsTreeType(string type)
    {
        return type == "trees" ||
               type == "tree" ||
               type == "plant_trees" ||
               type == "remove_trees" ||
               type == "move_tree" ||
               type == "delete_tree";
    }

    private void PrepareCommandType(LandscapeCommand command)
    {
        if (command == null)
            return;

        if (!string.IsNullOrWhiteSpace(command.commandType))
        {
            command.commandType = command.commandType.Trim().ToLowerInvariant();
            return;
        }

        // JsonUtility creates empty nested objects for missing fields,
        // so check the action string rather than null.
        if (command.terrain != null && !string.IsNullOrWhiteSpace(command.terrain.action))
        {
            command.commandType = "terrain";
            return;
        }

        if (command.trees != null && !string.IsNullOrWhiteSpace(command.trees.action))
        {
            command.commandType = "trees";
            return;
        }

        command.commandType = "unknown";
    }


    // =========================================================
    // VALIDATE COMMAND
    // =========================================================

    private bool ValidateCommand(LandscapeCommand command, out string error)
    {
        error = "";

        if (command == null)
        {
            error = "The AI returned no command.";
            return false;
        }

        PrepareCommandType(command);
        string type = command.commandType;

        if (IsTerrainType(type))
        {
            if (command.terrain == null || string.IsNullOrWhiteSpace(command.terrain.action))
            {
                error = "The AI identified a terrain action but did not specify which one.";
                return false;
            }

            command.terrain.action = command.terrain.action.Trim().ToLowerInvariant();
            return true;
        }

        if (IsTreeType(type))
        {
            if (command.trees == null || string.IsNullOrWhiteSpace(command.trees.action))
            {
                error = "The AI identified a tree action but did not specify which one.";
                return false;
            }

            command.trees.action = command.trees.action.Trim().ToLowerInvariant();
            return true;
        }

        error = "I can only change the terrain (mountains, rivers, lakes) and trees.";
        return false;
    }


    // =========================================================
    // DESCRIBE COMMAND
    // =========================================================

    private string DescribeCommand(LandscapeCommand command, SpatialContext context)
    {
        if (command == null)
            return "perform the requested action";

        PrepareCommandType(command);
        string type = command.commandType;

        // ---------------- TERRAIN ----------------

        if (IsTerrainType(type))
        {
            if (command.terrain == null || string.IsNullOrWhiteSpace(command.terrain.action))
                return "modify the terrain";

            string action = command.terrain.action.Trim().ToLowerInvariant();
            int count = Mathf.Max(command.terrain.count, 1);

            switch (action)
            {
                case "mountain":
                case "create_mountain":
                    return command.terrain.usePointedPosition
                        ? "create a mountain at the pointed location"
                        : "create a mountain";

                case "mountains":
                case "create_mountains":
                    return "create " + count + " mountain(s)";

                case "river":
                case "rivers":
                case "create_river":
                case "create_rivers":
                    return "create " + count + " river(s)";

                case "lake":
                case "lakes":
                case "create_lake":
                case "create_lakes":
                    return "create " + count + " lake(s)";

                case "modify_river":
                case "modify_selected_river":
                case "river_modify":
                    return "modify the selected river";

                case "modify_lake":
                case "modify_selected_lake":
                case "lake_modify":
                    return "modify the selected lake";

                case "delete_mountain":
                case "delete_selected_mountain":
                case "remove_mountain":
                    return context != null && context.hasSelectedMountain
                        ? "delete " + context.selectedMountainName
                        : "delete the selected mountain";

                case "delete_river":
                case "delete_selected_river":
                case "remove_river":
                    return "delete the selected river";

                case "delete_lake":
                case "delete_selected_lake":
                case "remove_lake":
                    return "delete the selected lake";

                case "reset":
                case "reset_terrain":
                    return "restore the terrain to its original shape";

                case "modify_mountain":
                case "modify_selected_mountain":
                case "mountain_modify":
                    {
                        string name = context != null && context.hasSelectedMountain
                            ? context.selectedMountainName
                            : "the selected mountain";

                        if (command.terrain.heightChange > 0f)
                            return "make " + name + " taller";

                        if (command.terrain.heightChange < 0f)
                            return "make " + name + " shorter";

                        return "modify " + name;
                    }
            }

            return "modify the terrain";
        }

        // ---------------- TREES ----------------

        if (IsTreeType(type))
        {
            if (command.trees == null || string.IsNullOrWhiteSpace(command.trees.action))
                return "perform a tree operation";

            string action = command.trees.action.Trim().ToLowerInvariant();

            switch (action)
            {
                case "move_tree":
                    return context != null && context.hasSelectedTree
                        ? "move " + context.selectedTreeName + " to the pointed location"
                        : "move the selected tree";

                case "plant":
                case "plant_trees":
                case "create_forest":
                    {
                        int count = Mathf.Max(command.trees.count, 1);
                        string typeName = NormalizeName(command.trees.treeType);

                        return "plant " + count + " " + typeName + " tree(s)" +
                               (command.trees.usePointedPosition ? " at the pointed location" : "");
                    }

                case "delete_tree":
                case "delete_selected_tree":
                    return context != null && context.hasSelectedTree
                        ? "delete " + context.selectedTreeName
                        : "delete the selected tree";

                case "remove":
                case "remove_trees":
                    return "remove trees around the pointed location";

                case "remove_all_trees":
                case "clear_trees":
                    return "remove all trees";
            }

            return "perform tree action: " + action;
        }

        return "perform the requested action";
    }


    // =========================================================
    // EXECUTE COMMAND
    // =========================================================

    private bool ExecuteValidatedCommand(LandscapeCommand command, SpatialContext spatialContext)
    {
        if (command == null)
            return false;

        PrepareCommandType(command);

        if (IsTerrainType(command.commandType))
            return ProcessTerrainCommand(command, spatialContext);

        if (IsTreeType(command.commandType))
            return ProcessTreeCommand(command, spatialContext);

        SetOutput("I could not determine the requested action.");
        return false;
    }


    // =========================================================
    // TERRAIN
    // =========================================================

    private bool ProcessTerrainCommand(LandscapeCommand command, SpatialContext spatialContext)
    {
        if (terrainGenerator == null)
        {
            SetOutput("TerrainGenerator is not assigned.");
            return false;
        }

        if (command.terrain == null)
            return false;

        string action = command.terrain.action.Trim().ToLowerInvariant();
        int count = Mathf.Clamp(command.terrain.count <= 0 ? 1 : command.terrain.count, 1, 20);

        switch (action)
        {
            // ---------------- MOUNTAIN ----------------

            case "mountain":
            case "create_mountain":
                if (command.terrain.usePointedPosition)
                {
                    if (!spatialContext.hasGroundPosition)
                    {
                        SetOutput("Please point at the terrain where you want the mountain created.");
                        return false;
                    }

                    terrainGenerator.CreateMountainAtWorldPosition(spatialContext.groundPosition);
                    return true;
                }

                terrainGenerator.CreateRandomMountain();
                return true;

            case "mountains":
            case "create_mountains":
                terrainGenerator.CreateRandomMountains(count);
                return true;

            // ---------------- RIVER / LAKE CREATION ----------------

            case "river":
            case "rivers":
            case "create_river":
            case "create_rivers":
                if (!RequireWaterManager())
                    return false;

                for (int i = 0; i < count; i++)
                    waterManager.CreateRiver();

                return true;

            case "lake":
            case "lakes":
            case "create_lake":
            case "create_lakes":
                if (!RequireWaterManager())
                    return false;

                for (int i = 0; i < count; i++)
                    waterManager.CreateLake();

                return true;

            // ---------------- MODIFY RIVER ----------------

            case "modify_river":
            case "modify_selected_river":
            case "river_modify":
                {
                    if (!RequireWaterManager())
                        return false;

                    if (!spatialContext.hasSelectedRiver)
                    {
                        SetOutput("Please point at a river first.");
                        return false;
                    }

                    int riverId = spatialContext.selectedRiverId;
                    WaterManager.RiverInfo river = waterManager.GetRiverById(riverId);

                    if (river == null)
                    {
                        SetOutput("The selected river no longer exists. Please point at a river again.");
                        return false;
                    }

                    float widthChange = command.terrain.widthChange;
                    float depthChange = command.terrain.depthChange;

                    if (Mathf.Abs(widthChange) < 0.01f && Mathf.Abs(depthChange) < 0.01f)
                    {
                        SetOutput("Please say whether you want the river wider, narrower, deeper or shallower.");
                        return false;
                    }

                    bool success = waterManager.ModifyRiver(
                        riverId,
                        Mathf.Clamp(river.width + widthChange, 2f, 100f),
                        Mathf.Clamp(river.depth + depthChange, 0f, 20f)
                    );

                    if (!success)
                    {
                        SetOutput("I could not modify the selected river.");
                        return false;
                    }

                    return true;
                }

            // ---------------- MODIFY LAKE ----------------

            case "modify_lake":
            case "modify_selected_lake":
            case "lake_modify":
                {
                    if (!RequireWaterManager())
                        return false;

                    if (!spatialContext.hasSelectedLake)
                    {
                        SetOutput("Please point at a lake first.");
                        return false;
                    }

                    int lakeId = spatialContext.selectedLakeId;
                    WaterManager.LakeInfo lake = waterManager.GetLakeById(lakeId);

                    if (lake == null)
                    {
                        SetOutput("The selected lake no longer exists. Please point at a lake again.");
                        return false;
                    }

                    float widthChange = command.terrain.widthChange;
                    float lengthChange = command.terrain.lengthChange;
                    float depthChange = command.terrain.depthChange;

                    if (Mathf.Abs(widthChange) < 0.01f &&
                        Mathf.Abs(lengthChange) < 0.01f &&
                        Mathf.Abs(depthChange) < 0.01f)
                    {
                        SetOutput("Please say whether you want the lake wider, narrower, longer, shorter, deeper or shallower.");
                        return false;
                    }

                    bool success = waterManager.ModifyLake(
                        lakeId,
                        Mathf.Clamp(lake.width + widthChange, 5f, 150f),
                        Mathf.Clamp(lake.length + lengthChange, 5f, 150f),
                        Mathf.Clamp(lake.depth + depthChange, 0f, 20f)
                    );

                    if (!success)
                    {
                        SetOutput("I could not modify the selected lake.");
                        return false;
                    }

                    return true;
                }

            // ---------------- MODIFY MOUNTAIN ----------------

            case "modify_mountain":
            case "modify_selected_mountain":
            case "mountain_modify":
                {
                    if (!spatialContext.hasSelectedMountain)
                    {
                        SetOutput("Please point at a mountain first.");
                        return false;
                    }

                    int mountainId = spatialContext.selectedMountainIndex;

                    if (!terrainGenerator.HasMountain(mountainId))
                    {
                        SetOutput("The selected mountain no longer exists. Please point at a mountain again.");
                        return false;
                    }

                    float heightChange = command.terrain.heightChange;

                    if (Mathf.Abs(heightChange) < 0.01f)
                    {
                        SetOutput("Please say whether you want the selected mountain taller or shorter.");
                        return false;
                    }

                    if (!terrainGenerator.ModifyMountain(mountainId, Mathf.Clamp(heightChange, -20f, 20f)))
                    {
                        SetOutput("I could not modify the selected mountain.");
                        return false;
                    }

                    return true;
                }

            // ---------------- DELETE MOUNTAIN ----------------

            case "delete_mountain":
            case "delete_selected_mountain":
            case "remove_mountain":
                {
                    if (!spatialContext.hasSelectedMountain)
                    {
                        SetOutput("Please point at a mountain first.");
                        return false;
                    }

                    int mountainId = spatialContext.selectedMountainIndex;

                    if (!terrainGenerator.HasMountain(mountainId))
                    {
                        SetOutput("The selected mountain no longer exists. Please point at a mountain again.");
                        return false;
                    }

                    terrainGenerator.DeleteMountain(mountainId);

                    if (terrainGenerator.HasMountain(mountainId))
                    {
                        SetOutput("I could not delete the selected mountain.");
                        return false;
                    }

                    if (mountainPointingDetector != null)
                        mountainPointingDetector.ClearSelection();

                    return true;
                }

            // ---------------- DELETE RIVER ----------------

            case "delete_river":
            case "delete_selected_river":
            case "remove_river":
                {
                    if (!RequireWaterManager())
                        return false;

                    if (!spatialContext.hasSelectedRiver)
                    {
                        SetOutput("Please point at a river first.");
                        return false;
                    }

                    int riverId = spatialContext.selectedRiverId;
                    waterManager.DeleteRiver(riverId);

                    if (waterManager.GetRiverById(riverId) != null)
                    {
                        SetOutput("I could not delete the selected river.");
                        return false;
                    }

                    return true;
                }

            // ---------------- DELETE LAKE ----------------

            case "delete_lake":
            case "delete_selected_lake":
            case "remove_lake":
                {
                    if (!RequireWaterManager())
                        return false;

                    if (!spatialContext.hasSelectedLake)
                    {
                        SetOutput("Please point at a lake first.");
                        return false;
                    }

                    int lakeId = spatialContext.selectedLakeId;
                    waterManager.DeleteLake(lakeId);

                    if (waterManager.GetLakeById(lakeId) != null)
                    {
                        SetOutput("I could not delete the selected lake.");
                        return false;
                    }

                    return true;
                }

            // ---------------- RESET ----------------

            case "reset":
            case "reset_terrain":
                terrainGenerator.ResetTerrain();

                if (waterManager != null)
                    waterManager.DeleteWater();

                return true;
        }

        SetOutput("Unknown terrain action: " + action);
        return false;
    }

    private bool RequireWaterManager()
    {
        if (waterManager != null)
            return true;

        SetOutput("WaterManager is not assigned.");
        return false;
    }


    // =========================================================
    // TREES
    // =========================================================

    private bool ProcessTreeCommand(LandscapeCommand command, SpatialContext spatialContext)
    {
        if (treeManager == null)
        {
            SetOutput("TreeManager is not assigned.");
            return false;
        }

        if (command.trees == null)
            return false;

        TreeCommand tree = command.trees;
        string action = tree.action.Trim().ToLowerInvariant();

        switch (action)
        {
            // ---------------- MOVE ----------------

            case "move_tree":
                return ProcessMoveTreeCommand(spatialContext);

            // ---------------- PLANT ----------------

            case "plant":
            case "plant_trees":
            case "create_forest":
                {
                    int count = Mathf.Clamp(tree.count <= 0 ? 1 : tree.count, 1, 5000);
                    float radius = Mathf.Max(tree.radius, 1f);
                    float spacing = Mathf.Max(tree.spacing, 0.5f);
                    string requestedType = NormalizeName(tree.treeType);

                    // Default centre comes from the command (0,0 unless set);
                    // the AI is told not to provide coordinates, so TreeManager
                    // is expected to treat this as "anywhere on the terrain".
                    float centerX = tree.centerX;
                    float centerZ = tree.centerZ;

                    if (tree.usePointedPosition)
                    {
                        Vector2 terrainPosition;

                        if (!TryGetPointedTerrainPosition(
                                spatialContext,
                                "Please point at the terrain where you want the trees planted.",
                                out terrainPosition))
                        {
                            return false;
                        }

                        centerX = terrainPosition.x;
                        centerZ = terrainPosition.y;

                        Debug.Log("[DemoChat] Pointed tree creation position: " + centerX + ", " + centerZ);
                    }

                    if (requestedType.Equals("Mixed", StringComparison.OrdinalIgnoreCase))
                    {
                        treeManager.PlantMixedTrees(count, centerX, centerZ, radius, spacing);
                        return true;
                    }

                    if (treeManager.GetTreePrefab(requestedType) == null)
                    {
                        SetOutput("Tree type '" + requestedType + "' was not found in TreeManager.");
                        return false;
                    }

                    treeManager.PlantTrees(count, requestedType, centerX, centerZ, radius, spacing);
                    return true;
                }

            // ---------------- DELETE SELECTED ----------------

            case "delete_tree":
            case "delete_selected_tree":
                {
                    if (!spatialContext.hasSelectedTree)
                    {
                        SetOutput("Please point at a tree first.");
                        return false;
                    }

                    TreeSelectable selectedTree =
                        treePointingDetector != null ? treePointingDetector.GetSelectedTree() : null;

                    if (selectedTree == null)
                    {
                        SetOutput("The selected tree is no longer available. Please point at a tree again.");
                        return false;
                    }

                    if (terrainGenerator == null)
                    {
                        SetOutput("TerrainGenerator is not assigned.");
                        return false;
                    }

                    // RemoveTrees uses the same terrain coordinates as PlantTrees.
                    Vector2 terrainPosition =
                        terrainGenerator.WorldToTerrainPosition(selectedTree.transform.position);

                    treeManager.RemoveTrees(terrainPosition.x, terrainPosition.y, selectedTreeRemovalRadius);
                    return true;
                }

            // ---------------- REMOVE AREA ----------------

            case "remove":
            case "remove_trees":
                {
                    Vector2 terrainPosition;

                    if (!TryGetPointedTerrainPosition(
                            spatialContext,
                            "Please point at the area where you want the trees removed.",
                            out terrainPosition))
                    {
                        return false;
                    }

                    float radius = tree.radius > 0f ? tree.radius : defaultTreeRemovalRadius;

                    treeManager.RemoveTrees(terrainPosition.x, terrainPosition.y, Mathf.Max(radius, 1f));
                    return true;
                }

            // ---------------- REMOVE ALL ----------------

            case "remove_all_trees":
            case "clear_trees":
                treeManager.RemoveAllTrees();
                return true;
        }

        SetOutput("Unknown tree action: " + action);
        return false;
    }

    private bool TryGetPointedTerrainPosition(
        SpatialContext spatialContext,
        string missingMessage,
        out Vector2 terrainPosition
    )
    {
        terrainPosition = Vector2.zero;

        if (!spatialContext.hasGroundPosition)
        {
            SetOutput(missingMessage);
            return false;
        }

        if (terrainGenerator == null)
        {
            SetOutput("TerrainGenerator is not assigned.");
            return false;
        }

        terrainPosition = terrainGenerator.WorldToTerrainPosition(spatialContext.groundPosition);
        return true;
    }


    // =========================================================
    // MOVE TREE
    // =========================================================

    private bool ProcessMoveTreeCommand(SpatialContext spatialContext)
    {
        if (!spatialContext.hasSelectedTree)
        {
            SetOutput("Please point at a tree first.");
            return false;
        }

        if (!spatialContext.hasGroundPosition)
        {
            SetOutput("Please point at the ground where you want to move the tree.");
            return false;
        }

        if (!treeManager.MoveTreeToWorldPosition(
                spatialContext.selectedTreeIndex,
                spatialContext.groundPosition))
        {
            SetOutput("I could not move the selected tree.");
            return false;
        }

        return true;
    }


    // =========================================================
    // NORMALIZE NAME
    // =========================================================

    private string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Default";

        string result = value.Trim();
        result = Regex.Replace(result, @"\btrees?\b", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bcrops?\b", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bplants?\b", "", RegexOptions.IgnoreCase);
        result = result.Trim();

        if (string.IsNullOrWhiteSpace(result))
            return "Default";

        return char.ToUpper(result[0]) + result.Substring(1);
    }


    // =========================================================
    // SYSTEM PROMPT
    // =========================================================

    private string BuildSystemPrompt()
    {
        return
            "You are a Unity landscape assistant.\n\n" +

            "You control TWO systems:\n" +
            "1. Terrain: mountains, rivers and lakes on the EXISTING terrain\n" +
            "2. Trees\n\n" +

            "You cannot create buildings, roads, animals or anything else. " +
            "For those requests, return unknown with a reason explaining what you can do.\n\n" +

            "Return ONLY valid JSON: either ONE command, or a wrapper with a commands array.\n\n" +

            // ---------------- MULTI ----------------

            "MULTI-COMMAND RULES:\n" +
            "If the user requests multiple independent actions, return:\n" +
            "{\n" +
            "  \"commands\":[\n" +
            "    {\"commandType\":\"terrain\", ...},\n" +
            "    {\"commandType\":\"trees\", ...}\n" +
            "  ]\n" +
            "}\n" +
            "Each item is one normal command. Never nest a commands array inside a command.\n" +
            "Examples:\n" +
            "\"create a mountain and a lake\" -> two commands.\n" +
            "\"create a mountain, a lake and 20 trees\" -> three commands.\n" +
            "\"create two mountains and a river\" -> two commands (mountains with count 2, river).\n" +
            "\"plant 30 trees and create a mountain here\" -> two commands.\n\n" +

            // ---------------- TERRAIN ----------------

            "TERRAIN RULE:\n" +
            "A Unity Terrain ALREADY exists. Terrain commands MODIFY it.\n" +
            "Never create, replace or delete the terrain itself. \"delete the terrain\" -> unknown.\n\n" +

            // ---------------- IDS / POSITIONS ----------------

            "IDS AND POSITIONS:\n" +
            "Unity owns every object ID (trees, mountains, rivers, lakes) and every coordinate.\n" +
            "NEVER put IDs or coordinates in the JSON.\n" +
            "'this tree', 'this mountain', 'this river', 'this lake' and 'it' refer to the object Unity reports as selected.\n" +
            "If that object is 'none', return unknown.\n" +
            "'here', 'there', 'over there', 'where I am pointing' -> usePointedPosition = true.\n" +
            "Otherwise usePointedPosition = false.\n\n" +

            // ---------------- DISAMBIGUATION ----------------

            "DISAMBIGUATION:\n" +
            "The object the user names decides the system:\n" +
            "river, stream, creek, waterway -> terrain / river or modify_river\n" +
            "lake, pond -> terrain / lake or modify_lake\n" +
            "mountain, hill, peak -> terrain / mountain, mountains or modify_mountain\n" +
            "tree, forest -> trees\n" +
            "XR selection never changes the named object. If a tree is selected and the user says " +
            "'create a river', create a river. Selection only matters for 'this ...' or 'it'.\n\n" +

            // ---------------- TERRAIN ACTIONS ----------------

            "TERRAIN COMMANDS (commandType = \"terrain\"):\n" +
            "Fields: action, count, heightChange, widthChange, lengthChange, depthChange, usePointedPosition.\n" +
            "Actions:\n" +
            "mountain, mountains, river, rivers, lake, lakes,\n" +
            "modify_mountain, modify_river, modify_lake,\n" +
            "delete_mountain, delete_river, delete_lake, reset\n\n" +

            "Examples:\n" +
            "\"create a mountain\" -> mountain\n" +
            "\"create 3 mountains\" -> mountains, count 3\n" +
            "\"create a mountain here\" -> mountain, usePointedPosition true\n" +
            "\"create a river\" -> river, count 1\n" +
            "\"create a lake\" -> lake, count 1\n" +
            "\"make this mountain taller\" -> modify_mountain, heightChange 5\n" +
            "\"make this mountain shorter\" -> modify_mountain, heightChange -5\n" +
            "\"make this river wider\" -> modify_river, widthChange 5\n" +
            "\"make this river narrower\" -> modify_river, widthChange -5\n" +
            "\"make this river deeper\" -> modify_river, depthChange 1\n" +
            "\"make this river shallower\" -> modify_river, depthChange -1\n" +
            "\"make this lake wider\" -> modify_lake, widthChange 5\n" +
            "\"make this lake longer\" -> modify_lake, lengthChange 5\n" +
            "\"make this lake shorter\" -> modify_lake, lengthChange -5\n" +
            "\"make this lake deeper\" -> modify_lake, depthChange 1\n" +
            "\"delete this mountain\" -> delete_mountain\n" +
            "\"delete this river\" -> delete_river\n" +
            "\"delete this lake\" -> delete_lake\n" +
            "\"reset the terrain\" -> reset\n\n" +

            "Example JSON:\n" +
            "{\n" +
            "  \"commandType\":\"terrain\",\n" +
            "  \"confidence\":0.95,\n" +
            "  \"reason\":\"The user explicitly requested a river.\",\n" +
            "  \"terrain\":{\"action\":\"river\",\"count\":1,\"usePointedPosition\":false}\n" +
            "}\n\n" +

            // ---------------- TREE ACTIONS ----------------

            "TREE COMMANDS (commandType = \"trees\"):\n" +
            "Fields: action, treeType, count, radius, spacing, usePointedPosition.\n" +
            "Actions: plant_trees, remove_trees, remove_all_trees, delete_tree, move_tree\n" +
            "Tree types: Oak, Palm, Birch, Maple, Eucalyptus, Apple, mixed. There is no Pine.\n" +
            "If no type is given, use mixed.\n\n" +

            "Examples:\n" +
            "\"plant 20 oak trees\" -> plant_trees, treeType Oak, count 20\n" +
            "\"plant some palm trees here\" -> plant_trees, treeType Palm, count 10, usePointedPosition true\n" +
            "\"remove the trees here\" -> remove_trees, radius 10, usePointedPosition true\n" +
            "\"remove all trees\" -> remove_all_trees\n" +
            "\"delete this tree\" -> delete_tree\n" +
            "\"move this tree over there\" -> move_tree\n\n" +

            "Example JSON:\n" +
            "{\n" +
            "  \"commandType\":\"trees\",\n" +
            "  \"confidence\":0.95,\n" +
            "  \"reason\":\"The user requested moving the selected tree to the pointed location.\",\n" +
            "  \"trees\":{\"action\":\"move_tree\",\"treeType\":\"\"}\n" +
            "}\n\n" +

            // ---------------- CONFIDENCE ----------------

            "CONFIDENCE:\n" +
            "Every command MUST contain \"confidence\" (0.0 to 1.0) and a short \"reason\".\n" +
            "0.80-1.00 = clear request. 0.50-0.79 = probable interpretation. Below 0.50 = guess.\n" +
            "Do not inflate confidence.\n\n" +

            // ---------------- UNKNOWN ----------------

            "UNKNOWN:\n" +
            "If the request is unclear, unsupported or refers to an object that is not selected, return:\n" +
            "{\n" +
            "  \"commandType\":\"unknown\",\n" +
            "  \"confidence\":0.20,\n" +
            "  \"reason\":\"<short explanation the user will read>\"\n" +
            "}\n\n" +

            // ---------------- FINAL ----------------

            "FINAL RULES:\n" +
            "1. Return ONLY JSON.\n" +
            "2. Use a commands array for multiple independent actions.\n" +
            "3. Always include confidence and reason.\n" +
            "4. Never guess ambiguous requests; use unknown.\n" +
            "5. Never put IDs or coordinates in the JSON.\n" +
            "6. Never create, replace or delete the existing terrain.\n" +
            "7. Do not let XR selection override an explicitly named object.\n" +
            "8. Never claim Unity performed an action.\n";
    }


    // =========================================================
    // JSON EXTRACTION
    // =========================================================

    private string ExtractJson(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        // Fallback in case the model still emits reasoning.
        text = Regex.Replace(text, @"<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"```json\s*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"```\s*", "");
        text = text.Trim();

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
            return null;

        return text.Substring(start, end - start + 1);
    }


    // =========================================================
    // OUTPUT / BUTTON / FINISH
    // =========================================================

    private void SetOutput(string message)
    {
        if (outputText != null)
            outputText.text = message;

        Debug.Log("[DemoChat] " + message);
    }

    private void SetButtonInteractable(bool value)
    {
        if (sendButton != null)
            sendButton.interactable = value;
    }

    private void FinishRequest(TaskCompletionSource<bool> completionSource, bool success)
    {
        isGenerating = false;
        SetButtonInteractable(true);

        if (completionSource != null)
            completionSource.TrySetResult(success);
    }


    // =========================================================
    // DATA CLASSES
    // =========================================================

    [Serializable]
    private class SpatialContext
    {
        // Tree
        public bool hasSelectedTree;
        public int selectedTreeIndex;
        public string selectedTreeName;
        public string selectedTreeType;

        // Ground
        public bool hasGroundPosition;
        public Vector3 groundPosition;

        // Mountain
        public bool hasSelectedMountain;
        public int selectedMountainIndex;
        public string selectedMountainName;

        // Water
        public bool hasSelectedRiver;
        public int selectedRiverId;
        public bool hasSelectedLake;
        public int selectedLakeId;
    }

    [Serializable]
    private class LandscapeCommand
    {
        public string commandType;
        public float confidence;
        public string reason;
        public TerrainCommand terrain;
        public TreeCommand trees;
    }

    [Serializable]
    private class MultiLandscapeCommand
    {
        public LandscapeCommand[] commands;
    }

    [Serializable]
    private class TerrainCommand
    {
        public string action;
        public int count;

        // Positive = taller, negative = shorter.
        public float heightChange;

        // Water modification
        public float widthChange;
        public float lengthChange;
        public float depthChange;

        public bool usePointedPosition;
    }

    [Serializable]
    private class TreeCommand
    {
        public string action;
        public string treeType;
        public int count;

        // Set by Unity from the pointed position, never by the AI.
        public float centerX;
        public float centerZ;

        public float radius;
        public float spacing;

        public bool usePointedPosition;
    }

    [Serializable]
    private class OllamaRequest
    {
        public string model;
        public bool stream;
        public bool think;
        public string format;
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


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (sendButton != null)
            sendButton.onClick.RemoveListener(SendCurrentPrompt);
    }
}