
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
        // OLLAMA / AI HOUSE DESIGN
        // ============================================================

        [Header("AI House Designer")]

        public DemoChat demoChat;


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
        // MAX RECORDING TIME
        // ============================================================

        [Header("Maximum Recording Time")]

        [Tooltip("Maximum amount of time the microphone can record one command.")]
        public float maxRecordingTime = 10f;

        private Coroutine _recordingTimeoutCoroutine;


        // ============================================================
        // CONVERSATION
        // ============================================================

        [Header("Conversation")]

        [Tooltip("How long to wait before returning to wake-word mode.")]
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
            // --------------------------------------------------------
            // Whisper
            // --------------------------------------------------------

            if (whisper != null)
            {
                whisper.OnNewSegment +=
                    OnNewSegment;

                whisper.OnProgress +=
                    OnProgressHandler;
            }


            // --------------------------------------------------------
            // Microphone
            // --------------------------------------------------------

            if (microphoneRecord != null)
            {
                microphoneRecord.OnRecordStop +=
                    OnRecordStop;
            }


            // --------------------------------------------------------
            // Wake word
            // --------------------------------------------------------

            if (openWakeWordManager != null)
            {
                openWakeWordManager.WakeWordDetected +=
                    OnWakeWordDetected;
            }


            // --------------------------------------------------------
            // Language
            // --------------------------------------------------------

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


            // --------------------------------------------------------
            // Translation
            // --------------------------------------------------------

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


            // --------------------------------------------------------
            // VAD
            // --------------------------------------------------------

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


            // --------------------------------------------------------
            // Button
            // --------------------------------------------------------

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
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] OpenWakeWordManager is not assigned."
                );
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
                "[MicrophoneDemo] Wake word detected: " +
                detection.Name +
                " score=" +
                detection.Probability.ToString("F3")
            );


            _conversationMode =
                true;


            StopConversationTimeout();


            SetWakeWordStatus(
                "Listening for your command..."
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
                    "[MicrophoneDemo] Microphone is already recording."
                );

                return;
            }


            _startingRecording =
                true;

            _processingSpeech =
                true;


            StopConversationTimeout();

            StopRecordingTimeout();


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


            // --------------------------------------------------------
            // START MICROPHONE
            // --------------------------------------------------------

            microphoneRecord.StartRecord();


            _startingRecording =
                false;


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Command recording started."
            );


            // --------------------------------------------------------
            // START MAXIMUM RECORDING TIMER
            // --------------------------------------------------------

            if (maxRecordingTime > 0f)
            {
                _recordingTimeoutCoroutine =
                    StartCoroutine(
                        RecordingTimeoutRoutine()
                    );
            }
        }


        // ============================================================
        // MAXIMUM RECORDING TIMER
        // ============================================================

        private IEnumerator RecordingTimeoutRoutine()
        {
            float timer =
                0f;


            while (
                timer < maxRecordingTime &&
                microphoneRecord != null &&
                microphoneRecord.IsRecording
            )
            {
                timer +=
                    Time.deltaTime;


                // Optional UI countdown

                if (timeText != null)
                {
                    float remaining =
                        Mathf.Max(
                            0f,
                            maxRecordingTime - timer
                        );

                    timeText.text =
                        "Recording: " +
                        remaining.ToString("F1") +
                        "s";
                }


                yield return null;
            }


            // --------------------------------------------------------
            // MAX TIME REACHED
            // --------------------------------------------------------

            if (
                microphoneRecord != null &&
                microphoneRecord.IsRecording
            )
            {
                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] Maximum recording time reached: " +
                    maxRecordingTime +
                    " seconds."
                );


                SetWakeWordStatus(
                    "Maximum recording time reached."
                );


                microphoneRecord.StopRecord();
            }


            _recordingTimeoutCoroutine =
                null;
        }


        // ============================================================
        // STOP RECORDING TIMER
        // ============================================================

        private void StopRecordingTimeout()
        {
            if (
                _recordingTimeoutCoroutine !=
                null
            )
            {
                StopCoroutine(
                    _recordingTimeoutCoroutine
                );


                _recordingTimeoutCoroutine =
                    null;
            }
        }


        // ============================================================
        // RECORDING STOPPED
        // ============================================================

        private async void OnRecordStop(
            AudioChunk recordedAudio)
        {
            // --------------------------------------------------------
            // IMPORTANT:
            // Stop the maximum recording timer because recording
            // has already stopped.
            // --------------------------------------------------------

            StopRecordingTimeout();


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
            // SEND EVERYTHING TO AI
            // ========================================================

            if (demoChat != null)
            {
                SetWakeWordStatus(
                    "AI is designing..."
                );


                if (buttonText != null)
                {
                    buttonText.text =
                        "Designing...";
                }


                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] Sending command to AI: " +
                    transcript
                );


                Stopwatch ollamaTimer =
                    Stopwatch.StartNew();


                await demoChat.Ask(
                    transcript
                );


                ollamaTimer.Stop();


                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] AI finished in " +
                    ollamaTimer.ElapsedMilliseconds +
                    " ms"
                );
            }
            else
            {
                UnityEngine.Debug.LogError(
                    "[MicrophoneDemo] DemoChat is not assigned."
                );
            }


            // ========================================================
            // LISTEN AGAIN
            // ========================================================

            ContinueConversation();
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
                "Listening for your next command..."
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

            StopRecordingTimeout();


            // --------------------------------------------------------
            // If microphone is still recording, stop it.
            // --------------------------------------------------------

            if (
                microphoneRecord != null &&
                microphoneRecord.IsRecording
            )
            {
                microphoneRecord.StopRecord();
            }


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
                "Waiting for wake word."
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

            StopRecordingTimeout();


            if (
                microphoneRecord != null &&
                microphoneRecord.IsRecording
            )
            {
                microphoneRecord.StopRecord();
            }


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

