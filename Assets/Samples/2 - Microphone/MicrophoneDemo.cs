using System;
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
        // OLLAMA / AI
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


        [Tooltip(
            "Maximum amount of time the microphone can record one command."
        )]
        public float maxRecordingTime = 10f;


        // ============================================================
        // CONVERSATION
        // ============================================================

        [Header("Conversation")]

        [Tooltip(
            "How long to wait for another wake word after AI finishes."
        )]
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
        // STATE MACHINE
        // ============================================================

        private enum AssistantState
        {
            WaitingForWakeWord,
            Recording,
            Transcribing,
            AIProcessing,
            Exiting
        }

        private AssistantState _state =
            AssistantState.WaitingForWakeWord;


        // ============================================================
        // INTERNAL DATA
        // ============================================================

        private string _buffer = "";

        private Coroutine _recordingTimeoutCoroutine;

        private Coroutine _conversationTimeoutCoroutine;


        // ============================================================
        // PROTECTION FLAGS
        // ============================================================

        private bool _recordStopBeingProcessed;

        private bool _destroying;


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
                button.interactable = false;
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
            if (_destroying)
                return;


            ChangeState(
                AssistantState.WaitingForWakeWord
            );


            SetWakeWordStatus(
                "Listening for \"alexa\""
            );


            StartWakeWordListening();
        }


        // ============================================================
        // STATE CHANGE
        // ============================================================

        private void ChangeState(
            AssistantState newState
        )
        {
            if (_destroying)
                return;


            AssistantState oldState =
                _state;


            _state =
                newState;


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] State: " +
                oldState +
                " -> " +
                newState
            );


            switch (newState)
            {
                // ----------------------------------------------------
                // WAITING
                // ----------------------------------------------------

                case AssistantState.WaitingForWakeWord:

                    SetWakeWordStatus(
                        "Listening for \"alexa\""
                    );

                    if (buttonText != null)
                    {
                        buttonText.text =
                            "Say \"alexa\"";
                    }

                    break;


                // ----------------------------------------------------
                // RECORDING
                // ----------------------------------------------------

                case AssistantState.Recording:

                    SetWakeWordStatus(
                        "Listening..."
                    );

                    if (buttonText != null)
                    {
                        buttonText.text =
                            "Listening...";
                    }

                    break;


                // ----------------------------------------------------
                // TRANSCRIBING
                // ----------------------------------------------------

                case AssistantState.Transcribing:

                    SetWakeWordStatus(
                        "Transcribing..."
                    );

                    if (buttonText != null)
                    {
                        buttonText.text =
                            "Transcribing...";
                    }

                    break;


                // ----------------------------------------------------
                // AI
                // ----------------------------------------------------

                case AssistantState.AIProcessing:

                    SetWakeWordStatus(
                        "AI is designing..."
                    );

                    if (buttonText != null)
                    {
                        buttonText.text =
                            "Designing...";
                    }

                    break;


                // ----------------------------------------------------
                // EXITING
                // ----------------------------------------------------

                case AssistantState.Exiting:

                    SetWakeWordStatus(
                        "Returning to wake-word mode..."
                    );

                    break;
            }
        }


        // ============================================================
        // START WAKE WORD LISTENING
        // ============================================================

        private void StartWakeWordListening()
        {
            if (_destroying)
                return;


            if (
                _state !=
                AssistantState.WaitingForWakeWord
            )
            {
                return;
            }


            if (openWakeWordManager == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] " +
                    "OpenWakeWordManager is not assigned."
                );

                return;
            }


            openWakeWordManager.StartListening();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Wake-word listening started."
            );
        }


        // ============================================================
        // WAKE WORD DETECTED
        // ============================================================

        private void OnWakeWordDetected(
            SentisModels.WakeWordDetection detection
        )
        {
            if (_destroying)
                return;


            // --------------------------------------------------------
            // CRITICAL:
            //
            // Only accept wake word while completely idle.
            //
            // This prevents wake-word detection from interrupting:
            //
            // Recording
            // Whisper
            // Ollama
            // Terrain generation
            // Tree generation
            // --------------------------------------------------------

            if (
                _state !=
                AssistantState.WaitingForWakeWord
            )
            {
                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] Wake word ignored " +
                    "because state is " +
                    _state
                );

                return;
            }


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Wake word detected: " +
                detection.Name +
                " score=" +
                detection.Probability.ToString("F3")
            );


            // --------------------------------------------------------
            // Stop wake-word listening.
            //
            // This is important.
            //
            // We do NOT want wake-word detection running while
            // the user is giving the actual command.
            // --------------------------------------------------------

            StopWakeWordListening();


            // --------------------------------------------------------
            // Start recording
            // --------------------------------------------------------

            StartCommandRecording();
        }


        // ============================================================
        // STOP WAKE WORD LISTENING
        // ============================================================

        private void StopWakeWordListening()
        {
            if (openWakeWordManager == null)
                return;


            // Depending on the version of the OpenWakeWord package,
            // StartListening() may simply remain active internally.
            //
            // We therefore primarily protect against wake-word
            // callbacks through the state machine.
            //
            // Do not call an assumed StopListening() method here
            // because different package versions expose different APIs.
        }


        // ============================================================
        // START COMMAND RECORDING
        // ============================================================

        private void StartCommandRecording()
        {
            if (_destroying)
                return;


            // --------------------------------------------------------
            // Only start from WAITING state.
            // --------------------------------------------------------

            if (
                _state !=
                AssistantState.WaitingForWakeWord
            )
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] " +
                    "StartCommandRecording ignored. State = " +
                    _state
                );

                return;
            }


            // --------------------------------------------------------
            // Microphone must exist.
            // --------------------------------------------------------

            if (microphoneRecord == null)
            {
                UnityEngine.Debug.LogError(
                    "[MicrophoneDemo] " +
                    "MicrophoneRecord is not assigned."
                );

                return;
            }


            // --------------------------------------------------------
            // Prevent duplicate recording.
            // --------------------------------------------------------

            if (microphoneRecord.IsRecording)
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] " +
                    "Microphone is already recording."
                );

                return;
            }


            // --------------------------------------------------------
            // Change state BEFORE starting microphone.
            //
            // This prevents callbacks from seeing the wrong state.
            // --------------------------------------------------------

            ChangeState(
                AssistantState.Recording
            );


            _buffer =
                "";


            StopRecordingTimeout();

            StopConversationTimeout();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Starting command recording."
            );


            // --------------------------------------------------------
            // START MICROPHONE
            // --------------------------------------------------------

            microphoneRecord.StartRecord();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Command recording started."
            );


            // --------------------------------------------------------
            // MAX RECORDING TIMER
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
            float timer = 0f;


            while (
                !_destroying &&
                _state ==
                    AssistantState.Recording &&
                microphoneRecord != null &&
                microphoneRecord.IsRecording
            )
            {
                timer +=
                    Time.deltaTime;


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


                if (
                    timer >=
                    maxRecordingTime
                )
                {
                    UnityEngine.Debug.Log(
                        "[MicrophoneDemo] " +
                        "Maximum recording time reached: " +
                        maxRecordingTime +
                        " seconds."
                    );


                    if (
                        microphoneRecord != null &&
                        microphoneRecord.IsRecording
                    )
                    {
                        microphoneRecord.StopRecord();
                    }


                    break;
                }


                yield return null;
            }


            _recordingTimeoutCoroutine =
                null;
        }


        // ============================================================
        // STOP RECORDING TIMEOUT
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
            AudioChunk recordedAudio
        )
        {
            if (_destroying)
                return;


            // --------------------------------------------------------
            // IMPORTANT:
            //
            // AudioChunk is a struct/value type in this package.
            //
            // Therefore DO NOT do:
            //
            // recordedAudio == null
            //
            // That caused your CS0019 error.
            // --------------------------------------------------------


            // --------------------------------------------------------
            // Stop timer immediately.
            // --------------------------------------------------------

            StopRecordingTimeout();


            // --------------------------------------------------------
            // Only process OnRecordStop if we were actually recording.
            //
            // This prevents duplicate callbacks from being processed.
            // --------------------------------------------------------

            if (
                _state !=
                AssistantState.Recording
            )
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] " +
                    "OnRecordStop ignored because state is " +
                    _state
                );

                return;
            }


            // --------------------------------------------------------
            // Prevent duplicate async processing.
            // --------------------------------------------------------

            if (_recordStopBeingProcessed)
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] " +
                    "Duplicate OnRecordStop ignored."
                );

                return;
            }


            _recordStopBeingProcessed =
                true;


            // --------------------------------------------------------
            // Change state BEFORE doing anything asynchronous.
            // --------------------------------------------------------

            ChangeState(
                AssistantState.Transcribing
            );


            _buffer =
                "";


            // ========================================================
            // BASIC AUDIO VALIDATION
            // ========================================================

            if (
                recordedAudio.Data == null ||
                recordedAudio.Data.Length == 0
            )
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] " +
                    "Recorded audio contains no data."
                );


                _recordStopBeingProcessed =
                    false;


                ReturnToWakeWordMode();


                return;
            }


            // --------------------------------------------------------
            // Very short recordings are usually accidental.
            // --------------------------------------------------------

            float audioLength =
                recordedAudio.Length;


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Recorded audio length: " +
                audioLength.ToString("F2") +
                " seconds."
            );


            if (audioLength < 0.25f)
            {
                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] " +
                    "Recording too short. Ignoring."
                );


                if (timeText != null)
                {
                    timeText.text =
                        "Recording too short.";
                }


                _recordStopBeingProcessed =
                    false;


                ReturnToWakeWordMode();


                return;
            }


            // ========================================================
            // WHISPER
            // ========================================================

            if (whisper == null)
            {
                UnityEngine.Debug.LogError(
                    "[MicrophoneDemo] " +
                    "WhisperManager is not assigned."
                );


                _recordStopBeingProcessed =
                    false;


                ReturnToWakeWordMode();


                return;
            }


            Stopwatch whisperTimer =
                Stopwatch.StartNew();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Whisper starting..."
            );


            Whisper.WhisperResult res = null;


            try
            {
                res =
                    await whisper.GetTextAsync(
                        recordedAudio.Data,
                        recordedAudio.Frequency,
                        recordedAudio.Channels
                    );
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError(
                    "[MicrophoneDemo] Whisper exception:\n" +
                    exception
                );


                _recordStopBeingProcessed =
                    false;


                ReturnToWakeWordMode();


                return;
            }


            whisperTimer.Stop();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Whisper finished in " +
                whisperTimer.ElapsedMilliseconds +
                " ms"
            );


            // ========================================================
            // WHISPER RESULT VALIDATION
            // ========================================================

            if (res == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[MicrophoneDemo] " +
                    "Whisper returned null."
                );


                _recordStopBeingProcessed =
                    false;


                ReturnToWakeWordMode();


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
                res.Result;


            if (transcript == null)
            {
                transcript =
                    "";
            }


            transcript =
                transcript.Trim();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] Whisper result: [" +
                transcript +
                "]"
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
                    "[MicrophoneDemo] " +
                    "Empty transcript. AI will NOT be called."
                );


                if (outputText != null)
                {
                    outputText.text =
                        "I didn't hear a command.";
                }


                _recordStopBeingProcessed =
                    false;


                ReturnToWakeWordMode();


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
                    "\n\nLanguage: " +
                    res.Language;
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
            // AI
            // ========================================================

            if (demoChat == null)
            {
                UnityEngine.Debug.LogError(
                    "[MicrophoneDemo] " +
                    "DemoChat is not assigned."
                );


                _recordStopBeingProcessed =
                    false;


                ReturnToWakeWordMode();


                return;
            }


            // --------------------------------------------------------
            // CRITICAL:
            //
            // The microphone is NOT restarted here.
            //
            // We remain in AIProcessing until DemoChat.Ask()
            // completely finishes.
            // --------------------------------------------------------

            ChangeState(
                AssistantState.AIProcessing
            );


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] " +
                "Sending command to AI: " +
                transcript
            );


            Stopwatch ollamaTimer =
                Stopwatch.StartNew();


            try
            {
                await demoChat.Ask(
                    transcript
                );
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError(
                    "[MicrophoneDemo] " +
                    "DemoChat exception:\n" +
                    exception
                );
            }


            ollamaTimer.Stop();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] AI finished in " +
                ollamaTimer.ElapsedMilliseconds +
                " ms"
            );


            // ========================================================
            // AI COMPLETELY FINISHED
            // ========================================================

            _recordStopBeingProcessed =
                false;


            // --------------------------------------------------------
            // ONLY NOW return to wake-word mode.
            //
            // This means:
            //
            // Terrain generation finished
            // OR
            // Tree generation finished
            // OR
            // AI error finished
            //
            // BEFORE microphone starts again.
            // --------------------------------------------------------

            ReturnToWakeWordMode();
        }


        // ============================================================
        // RETURN TO WAKE WORD MODE
        // ============================================================

        private void ReturnToWakeWordMode()
        {
            if (_destroying)
                return;


            StopRecordingTimeout();


            StopConversationTimeout();


            // --------------------------------------------------------
            // Safety:
            //
            // If the microphone is somehow still recording,
            // stop it before returning to wake-word mode.
            // --------------------------------------------------------

            if (
                microphoneRecord != null &&
                microphoneRecord.IsRecording
            )
            {
                UnityEngine.Debug.Log(
                    "[MicrophoneDemo] " +
                    "Stopping microphone before wake-word mode."
                );


                microphoneRecord.StopRecord();


                // ----------------------------------------------------
                // IMPORTANT:
                //
                // Do NOT immediately start recording here.
                //
                // OnRecordStop may be invoked by StopRecord().
                //
                // We simply change state and let the callback finish.
                // ----------------------------------------------------
            }


            ChangeState(
                AssistantState.WaitingForWakeWord
            );


            // --------------------------------------------------------
            // Start listening for wake word.
            //
            // There is NO command recording here.
            // --------------------------------------------------------

            StartWakeWordListening();


            // --------------------------------------------------------
            // Optional conversation timeout.
            //
            // This does NOT start the microphone.
            //
            // It only determines whether the assistant remains
            // in conversation mode.
            // --------------------------------------------------------

            StartConversationTimeout();


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] " +
                "AI completely finished. " +
                "Ready for next wake word."
            );
        }


        // ============================================================
        // CONVERSATION TIMEOUT
        // ============================================================

        private void StartConversationTimeout()
        {
            StopConversationTimeout();


            if (
                conversationTimeout <= 0f
            )
            {
                return;
            }


            if (
                _state !=
                AssistantState.WaitingForWakeWord
            )
            {
                return;
            }


            _conversationTimeoutCoroutine =
                StartCoroutine(
                    ConversationTimeoutRoutine()
                );
        }


        // ============================================================
        // STOP CONVERSATION TIMEOUT
        // ============================================================

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


        // ============================================================
        // CONVERSATION TIMEOUT ROUTINE
        // ============================================================

        private IEnumerator ConversationTimeoutRoutine()
        {
            float timer =
                0f;


            while (
                !_destroying &&
                _state ==
                    AssistantState.WaitingForWakeWord
            )
            {
                timer +=
                    Time.deltaTime;


                if (
                    timer >=
                    conversationTimeout
                )
                {
                    break;
                }


                yield return null;
            }


            _conversationTimeoutCoroutine =
                null;


            if (_destroying)
                yield break;


            if (
                _state !=
                AssistantState.WaitingForWakeWord
            )
            {
                yield break;
            }


            UnityEngine.Debug.Log(
                "[MicrophoneDemo] " +
                "Conversation timeout."
            );


            // --------------------------------------------------------
            // We remain safe.
            //
            // The next wake word can start a new command.
            // --------------------------------------------------------

            ChangeState(
                AssistantState.WaitingForWakeWord
            );


            SetWakeWordStatus(
                "Listening for \"alexa\""
            );


            if (buttonText != null)
            {
                buttonText.text =
                    "Say \"alexa\"";
            }
        }


        // ============================================================
        // VAD
        // ============================================================

        private void OnVadChanged(
            bool value
        )
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
            int index
        )
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
            bool translate
        )
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
            int progress
        )
        {
            // --------------------------------------------------------
            // Only show Whisper progress while transcribing.
            // --------------------------------------------------------

            if (
                _state !=
                AssistantState.Transcribing
            )
            {
                return;
            }


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
            WhisperSegment segment
        )
        {
            if (!streamSegments)
                return;


            if (outputText == null)
                return;


            // --------------------------------------------------------
            // Only update streaming transcript while Whisper is
            // actually transcribing.
            // --------------------------------------------------------

            if (
                _state !=
                AssistantState.Transcribing
            )
            {
                return;
            }


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
            string status
        )
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
            _destroying =
                true;


            // --------------------------------------------------------
            // Stop coroutines FIRST.
            // --------------------------------------------------------

            StopRecordingTimeout();

            StopConversationTimeout();


            // --------------------------------------------------------
            // Stop microphone.
            // --------------------------------------------------------

            if (
                microphoneRecord != null &&
                microphoneRecord.IsRecording
            )
            {
                microphoneRecord.StopRecord();
            }


            // --------------------------------------------------------
            // Whisper events
            // --------------------------------------------------------

            if (whisper != null)
            {
                whisper.OnNewSegment -=
                    OnNewSegment;


                whisper.OnProgress -=
                    OnProgressHandler;
            }


            // --------------------------------------------------------
            // Microphone event
            // --------------------------------------------------------

            if (microphoneRecord != null)
            {
                microphoneRecord.OnRecordStop -=
                    OnRecordStop;
            }


            // --------------------------------------------------------
            // Wake word event
            // --------------------------------------------------------

            if (openWakeWordManager != null)
            {
                openWakeWordManager.WakeWordDetected -=
                    OnWakeWordDetected;
            }


            // --------------------------------------------------------
            // UI events
            // --------------------------------------------------------

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