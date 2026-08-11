using System.Diagnostics;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Whisper.Utils;
using Button = UnityEngine.UI.Button;
using Toggle = UnityEngine.UI.Toggle;

namespace Whisper.Samples
{
    public class MicrophoneDemo : MonoBehaviour
    {
        // ============================================================
        // WHISPER
        // ============================================================

        [Header("Whisper")]

        public WhisperManager whisper;

        public MicrophoneRecord microphoneRecord;


        // ============================================================
        // OLLAMA
        // ============================================================

        [Header("Ollama")]

        public DemoChat demoChat;


        // ============================================================
        // FLOOR PLAN
        // ============================================================

        [Header("Floor Plan")]

        public FloorPlanDemo floorPlanDemo;


        // ============================================================
        // WAKE WORD
        // ============================================================

        [Header("OpenWakeWord")]

        public OpenWakeWordManager openWakeWordManager;


        // ============================================================
        // RECORDING
        // ============================================================

        [Header("Recording")]

        public bool streamSegments = true;

        public bool printLanguage = true;


        // ============================================================
        // CONVERSATION
        // ============================================================

        [Header("Conversation")]

        public float conversationTimeout = 8f;


        // ============================================================
        // UI
        // ============================================================

        [Header("UI")]

        public Button button;

        public Text buttonText;

        public Text outputText;

        public Text timeText;

        public Text wakeWordStatusText;

        public Dropdown languageDropdown;

        public Toggle translateToggle;

        public Toggle vadToggle;

        public ScrollRect scroll;


        // ============================================================
        // INTERNAL STATE
        // ============================================================

        private string _buffer;

        private bool _processingSpeech;

        private bool _conversationMode;

        private bool _startingRecording;

        private Coroutine _conversationTimeoutCoroutine;


        // ============================================================
        // AWAKE
        // ============================================================

        private void Awake()
        {
            if (whisper != null)
            {
                whisper.OnNewSegment +=
                    OnNewSegment;

                whisper.OnProgress +=
                    OnProgressHandler;
            }


            if (microphoneRecord != null)
            {
                microphoneRecord.OnRecordStop +=
                    OnRecordStop;
            }


            if (openWakeWordManager != null)
            {
                openWakeWordManager.WakeWordDetected +=
                    OnWakeWordDetected;
            }


            if (
                languageDropdown != null &&
                whisper != null
            )
            {
                int index =
                    languageDropdown.options.FindIndex(
                        op =>
                            op.text ==
                            whisper.language
                    );


                if (index >= 0)
                {
                    languageDropdown.value =
                        index;
                }


                languageDropdown.onValueChanged.AddListener(
                    OnLanguageChanged
                );
            }


            if (
                translateToggle != null &&
                whisper != null
            )
            {
                translateToggle.isOn =
                    whisper.translateToEnglish;


                translateToggle.onValueChanged.AddListener(
                    OnTranslateChanged
                );
            }


            if (
                vadToggle != null &&
                microphoneRecord != null
            )
            {
                vadToggle.isOn =
                    microphoneRecord.vadStop;


                vadToggle.onValueChanged.AddListener(
                    OnVadChanged
                );
            }


            if (button != null)
            {
                button.interactable =
                    false;
            }


            if (buttonText != null)
            {
                buttonText.text =
                    "Say \"alexa\"";
            }
        }


        // ============================================================
        // START
        // ============================================================

        private void Start()
        {
            SetWakeWordStatus(
                "Listening for \"alexa\""
            );


            if (openWakeWordManager != null)
            {
                openWakeWordManager.StartListening();
            }
        }


        // ============================================================
        // WAKE WORD DETECTED
        // ============================================================

        private void OnWakeWordDetected(
            SentisModels.WakeWordDetection detection)
        {
            if (_conversationMode)
                return;


            if (
                _processingSpeech ||
                _startingRecording
            )
                return;


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Alexa detected: " +
                detection.Name +
                " score=" +
                detection.Probability.ToString("F3")
            );


            _conversationMode =
                true;


            StopConversationTimeout();


            SetWakeWordStatus(
                "Listening for your question..."
            );


            if (buttonText != null)
            {
                buttonText.text =
                    "Listening...";
            }


            StartCommandRecording();
        }


        // ============================================================
        // START COMMAND RECORDING
        // ============================================================

        private void StartCommandRecording()
        {
            if (!_conversationMode)
                return;


            if (microphoneRecord == null)
            {
                UnityEngine.Debug.LogError(
                    "[MicrophoneDemo] MicrophoneRecord is not assigned."
                );


                ExitConversationMode();

                return;
            }


            if (
                _processingSpeech ||
                _startingRecording
            )
                return;


            if (microphoneRecord.IsRecording)
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] MicrophoneRecord is already recording."
                );

                return;
            }


            _startingRecording =
                true;

            _processingSpeech =
                true;


            StopConversationTimeout();


            SetWakeWordStatus(
                "Listening..."
            );


            if (buttonText != null)
            {
                buttonText.text =
                    "Listening...";
            }


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Starting command recording."
            );


            microphoneRecord.StartRecord();


            _startingRecording =
                false;


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Command recording started."
            );
        }


        // ============================================================
        // RECORDING STOPPED
        // ============================================================

        private async void OnRecordStop(
            AudioChunk recordedAudio)
        {
            if (!_processingSpeech)
                return;


            _processingSpeech =
                false;


            _buffer =
                "";


            SetWakeWordStatus(
                "Transcribing..."
            );


            if (buttonText != null)
            {
                buttonText.text =
                    "Transcribing...";
            }


            if (whisper == null)
            {
                UnityEngine.Debug.LogError(
                    "[MicrophoneDemo] WhisperManager is not assigned."
                );


                ExitConversationMode();

                return;
            }


            // ========================================================
            // WHISPER TIMING
            // ========================================================

            Stopwatch whisperTimer =
                Stopwatch.StartNew();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Whisper starting..."
            );


            var res =
                await whisper.GetTextAsync(
                    recordedAudio.Data,
                    recordedAudio.Frequency,
                    recordedAudio.Channels
                );


            whisperTimer.Stop();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Whisper finished in " +
                whisperTimer.ElapsedMilliseconds +
                " ms"
            );


            // ========================================================
            // NULL RESULT
            // ========================================================

            if (res == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] Whisper returned null."
                );


                ContinueConversation();

                return;
            }


            // ========================================================
            // PERFORMANCE
            // ========================================================

            long time =
                whisperTimer.ElapsedMilliseconds;


            float rate =
                0f;


            if (time > 0)
            {
                rate =
                    recordedAudio.Length /
                    (time * 0.001f);
            }


            if (timeText != null)
            {
                timeText.text =
                    $"Whisper: {time} ms\n" +
                    $"Rate: {rate:F1}x";
            }


            // ========================================================
            // TRANSCRIPT
            // ========================================================

            string transcript =
                res.Result.Trim();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Whisper result: " +
                transcript
            );


            // ========================================================
            // EMPTY SPEECH
            // ========================================================

            if (
                string.IsNullOrWhiteSpace(
                    transcript
                )
            )
            {
                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] Empty transcript."
                );


                ContinueConversation();

                return;
            }


            // ========================================================
            // DISPLAY TRANSCRIPT
            // ========================================================

            string displayText =
                transcript;


            if (printLanguage)
            {
                displayText +=
                    $"\n\nLanguage: {res.Language}";
            }


            if (outputText != null)
            {
                outputText.text =
                    displayText;
            }


            UiUtils.ScrollDown(
                scroll
            );


            // ========================================================
            // COMMAND ROUTING
            // ========================================================

            // --------------------------------------------------------
            // FLOOR PLAN COMMAND
            // --------------------------------------------------------

            if (
                IsHouseGenerationCommand(
                    transcript
                )
            )
            {
                if (floorPlanDemo != null)
                {
                    SetWakeWordStatus(
                        "Generating house from floor plan..."
                    );


                    if (buttonText != null)
                    {
                        buttonText.text =
                            "Generating house...";
                    }


                    UnityEngine.Debug.Log(
                        "[MicrophoneDemo] Floor plan command detected: " +
                        transcript
                    );


                    // IMPORTANT:
                    // Do not send this command to DemoChat.
                    //
                    // FloorPlanDemo itself sends the image
                    // to Qwen3-VL.

                    floorPlanDemo.GenerateHouse();


                    // ------------------------------------------------
                    // WAIT FOR HOUSE GENERATION
                    // ------------------------------------------------
                    //
                    // Do NOT immediately call ContinueConversation().
                    //
                    // Qwen3-VL may take 50+ seconds.

                    StartCoroutine(
                        WaitForHouseGeneration()
                    );


                    return;
                }
                else
                {
                    UnityEngine.Debug.LogError(
                        "[MicrophoneDemo] FloorPlanDemo is not assigned."
                    );


                    SetWakeWordStatus(
                        "FloorPlanDemo is not assigned."
                    );


                    ContinueConversation();

                    return;
                }
            }


            // --------------------------------------------------------
            // NORMAL OLLAMA QUESTION
            // --------------------------------------------------------

            if (demoChat != null)
            {
                SetWakeWordStatus(
                    "Thinking..."
                );


                if (buttonText != null)
                {
                    buttonText.text =
                        "Thinking...";
                }


                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] Sending to Ollama NOW: " +
                    transcript
                );


                Stopwatch ollamaTimer =
                    Stopwatch.StartNew();


                await demoChat.Ask(
                    transcript
                );


                ollamaTimer.Stop();


                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] Ollama finished in " +
                    ollamaTimer.ElapsedMilliseconds +
                    " ms"
                );
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] DemoChat is not assigned."
                );
            }


            // ========================================================
            // LISTEN AGAIN
            // ========================================================

            ContinueConversation();
        }


        // ============================================================
        // HOUSE GENERATION WAIT
        // ============================================================

        private IEnumerator WaitForHouseGeneration()
        {
            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Waiting for floor-plan generation..."
            );


            while (
                floorPlanDemo != null &&
                floorPlanDemo.IsGenerating
            )
            {
                yield return null;
            }


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Floor-plan generation finished."
            );


            if (!_conversationMode)
                yield break;


            SetWakeWordStatus(
                "House generated. Listening again..."
            );


            if (buttonText != null)
            {
                buttonText.text =
                    "Listening...";
            }


            ContinueConversation();
        }


        // ============================================================
        // HOUSE COMMAND DETECTION
        // ============================================================

        private bool IsHouseGenerationCommand(
            string transcript)
        {
            if (
                string.IsNullOrWhiteSpace(
                    transcript
                )
            )
            {
                return false;
            }


            string command =
                transcript
                    .ToLower()
                    .Trim();


            return
                command.Contains(
                    "create the house"
                ) ||

                command.Contains(
                    "create house"
                ) ||

                command.Contains(
                    "generate the house"
                ) ||

                command.Contains(
                    "generate house"
                ) ||

                command.Contains(
                    "build the house"
                ) ||

                command.Contains(
                    "build house"
                ) ||

                command.Contains(
                    "generate this floor plan"
                ) ||

                command.Contains(
                    "create this floor plan"
                ) ||

                command.Contains(
                    "turn this floor plan into a house"
                ) ||

                command.Contains(
                    "make the house"
                );
        }


        // ============================================================
        // CONTINUE CONVERSATION
        // ============================================================

        private void ContinueConversation()
        {
            if (!_conversationMode)
                return;


            StopConversationTimeout();


            SetWakeWordStatus(
                "Listening for your next question..."
            );


            if (buttonText != null)
            {
                buttonText.text =
                    "Listening...";
            }


            StartCommandRecording();


            StartConversationTimeout();
        }


        // ============================================================
        // CONVERSATION TIMEOUT
        // ============================================================

        private void StartConversationTimeout()
        {
            StopConversationTimeout();


            if (!_conversationMode)
                return;


            _conversationTimeoutCoroutine =
                StartCoroutine(
                    ConversationTimeoutRoutine()
                );
        }


        private void StopConversationTimeout()
        {
            if (
                _conversationTimeoutCoroutine !=
                null
            )
            {
                StopCoroutine(
                    _conversationTimeoutCoroutine
                );


                _conversationTimeoutCoroutine =
                    null;
            }
        }


        private IEnumerator ConversationTimeoutRoutine()
        {
            // --------------------------------------------------------
            // Wait while recording / processing.
            // --------------------------------------------------------

            while (
                _processingSpeech ||
                _startingRecording
            )
            {
                yield return null;
            }


            // --------------------------------------------------------
            // Wait for another command.
            // --------------------------------------------------------

            float timer =
                0f;


            while (
                timer <
                conversationTimeout
            )
            {
                if (!_conversationMode)
                    yield break;


                if (
                    _processingSpeech ||
                    _startingRecording
                )
                {
                    yield break;
                }


                timer +=
                    Time.deltaTime;


                yield return null;
            }


            // --------------------------------------------------------
            // Exit conversation.
            // --------------------------------------------------------

            if (
                !_processingSpeech &&
                !_startingRecording &&
                _conversationMode
            )
            {
                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] Conversation timeout."
                );


                ExitConversationMode();
            }
        }


        // ============================================================
        // EXIT CONVERSATION
        // ============================================================

        private void ExitConversationMode()
        {
            StopConversationTimeout();


            _conversationMode =
                false;


            _processingSpeech =
                false;


            _startingRecording =
                false;


            SetWakeWordStatus(
                "Listening for \"alexa\""
            );


            if (buttonText != null)
            {
                buttonText.text =
                    "Say \"alexa\"";
            }


            if (openWakeWordManager != null)
            {
                openWakeWordManager.StartListening();
            }


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Conversation ended. " +
                "Waiting for Alexa."
            );
        }


        // ============================================================
        // VAD
        // ============================================================

        private void OnVadChanged(
            bool value)
        {
            if (microphoneRecord != null)
            {
                microphoneRecord.vadStop =
                    value;
            }
        }


        // ============================================================
        // LANGUAGE
        // ============================================================

        private void OnLanguageChanged(
            int index)
        {
            if (whisper == null)
                return;


            if (languageDropdown == null)
                return;


            if (
                index < 0 ||
                index >=
                languageDropdown.options.Count
            )
            {
                return;
            }


            whisper.language =
                languageDropdown
                    .options[index]
                    .text;
        }


        // ============================================================
        // TRANSLATION
        // ============================================================

        private void OnTranslateChanged(
            bool translate)
        {
            if (whisper != null)
            {
                whisper.translateToEnglish =
                    translate;
            }
        }


        // ============================================================
        // WHISPER PROGRESS
        // ============================================================

        private void OnProgressHandler(
            int progress)
        {
            if (timeText != null)
            {
                timeText.text =
                    $"Whisper Progress: {progress}%";
            }
        }


        // ============================================================
        // WHISPER SEGMENTS
        // ============================================================

        private void OnNewSegment(
            WhisperSegment segment)
        {
            if (!streamSegments)
                return;


            if (outputText == null)
                return;


            _buffer +=
                segment.Text;


            outputText.text =
                _buffer + "...";


            UiUtils.ScrollDown(
                scroll
            );
        }


        // ============================================================
        // STATUS
        // ============================================================

        private void SetWakeWordStatus(
            string status)
        {
            if (wakeWordStatusText != null)
            {
                wakeWordStatusText.text =
                    status;
            }


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] " +
                status
            );
        }


        // ============================================================
        // CLEANUP
        // ============================================================

        private void OnDestroy()
        {
            StopConversationTimeout();


            if (whisper != null)
            {
                whisper.OnNewSegment -=
                    OnNewSegment;


                whisper.OnProgress -=
                    OnProgressHandler;
            }


            if (microphoneRecord != null)
            {
                microphoneRecord.OnRecordStop -=
                    OnRecordStop;
            }


            if (openWakeWordManager != null)
            {
                openWakeWordManager.WakeWordDetected -=
                    OnWakeWordDetected;
            }


            if (languageDropdown != null)
            {
                languageDropdown.onValueChanged.RemoveListener(
                    OnLanguageChanged
                );
            }


            if (translateToggle != null)
            {
                translateToggle.onValueChanged.RemoveListener(
                    OnTranslateChanged
                );
            }


            if (vadToggle != null)
            {
                vadToggle.onValueChanged.RemoveListener(
                    OnVadChanged
                );
            }
        }
    }
}