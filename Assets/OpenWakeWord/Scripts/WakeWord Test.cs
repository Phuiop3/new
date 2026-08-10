
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SentisModels;

namespace Whisper.Samples
{
    /// <summary>
    /// Owns the Unity microphone while OpenWakeWord is listening.
    ///
    /// Flow:
    /// Microphone -> resample to 16 kHz -> OpenWakeWord.PushSamples()
    ///
    /// When the wake word is detected, the microphone is released so
    /// MicrophoneRecord can take ownership for Whisper recording.
    /// </summary>
    public class OpenWakeWordManager : MonoBehaviour
    {
        [Header("OpenWakeWord Models")]
        public string modelFolder = "OpenWakeWord";

        [Range(0.1f, 1.0f)]
        public float detectionThreshold = 0.5f;

        public bool logScores = false;

        [Header("Microphone")]
        public int microphoneClipLengthSeconds = 10;

        [Header("Debug")]
        public bool logMicrophone = false;

        public OpenWakeWord Detector => _detector;

        public bool IsListening { get; private set; }

        public bool IsReady { get; private set; }

        public string MicrophoneName { get; private set; }

        public event Action<WakeWordDetection> WakeWordDetected;

        private OpenWakeWord _detector;

        private AudioClip _microphoneClip;

        private int _microphoneFrequency;

        private int _lastMicrophonePosition;

        private bool _microphoneRunning;

        // Temporary source samples read from Unity microphone.
        private float[] _sourceReadBuffer;

        // Source samples waiting to be resampled.
        private readonly List<float> _sourceSamples =
            new List<float>(48000);

        // Resampling state.
        private double _resamplePosition;

        private const int TargetSampleRate = 16000;

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!IsReady)
                return;

            if (!IsListening)
                return;

            if (!_microphoneRunning)
                return;

            ReadMicrophoneSamples();

            _detector.Pump();
        }

        public bool Initialize()
        {
            if (IsReady)
                return true;

            _detector = new OpenWakeWord(
                detectionThreshold,
                logScores);

            _detector.Detected += OnDetectorDetected;

            string modelRoot = Path.Combine(
                Application.streamingAssetsPath,
                modelFolder);

            Debug.Log(
                $"[OpenWakeWordManager] Loading models from:\n{modelRoot}");

            bool loaded = _detector.Load(
                modelRoot,
                "melspectrogram_fp16.sentis",
                "embedding_model_fp16.sentis",
                "WakeWord/alexa_v0.1_fp16.sentis");

            if (!loaded)
            {
                Debug.LogError(
                    "[OpenWakeWordManager] Failed to load OpenWakeWord models.");

                _detector.Detected -= OnDetectorDetected;
                _detector.Dispose();
                _detector = null;

                return false;
            }

            IsReady = true;

            Debug.Log(
                "[OpenWakeWordManager] OpenWakeWord initialized.");

            StartListening();

            return true;
        }

        // ============================================================
        // START LISTENING
        // ============================================================

        public void StartListening()
        {
            if (!IsReady)
            {
                Debug.LogWarning(
                    "[OpenWakeWordManager] Cannot start listening before initialization.");

                return;
            }

            if (IsListening)
                return;

            StartMicrophone();

            if (!_microphoneRunning)
            {
                Debug.LogError(
                    "[OpenWakeWordManager] Could not start microphone.");

                return;
            }

            _sourceSamples.Clear();
            _resamplePosition = 0.0;
            _lastMicrophonePosition = Microphone.GetPosition(MicrophoneName);

            _detector.ResetDetector();
            _detector.SetListening(true);

            IsListening = true;

            Debug.Log(
                "[OpenWakeWordManager] Listening for \"alexa\".");
        }

        // ============================================================
        // STOP LISTENING
        // ============================================================

        public void StopListening()
        {
            if (!IsListening && !_microphoneRunning)
                return;

            IsListening = false;

            if (_detector != null)
            {
                _detector.SetListening(false);
            }

            StopMicrophone();

            _sourceSamples.Clear();
            _resamplePosition = 0.0;

            Debug.Log(
                "[OpenWakeWordManager] Wake-word microphone stopped.");
        }

        // ============================================================
        // MICROPHONE
        // ============================================================

        private void StartMicrophone()
        {
            if (_microphoneRunning)
                return;

            if (Microphone.devices == null ||
                Microphone.devices.Length == 0)
            {
                Debug.LogError(
                    "[OpenWakeWordManager] No microphone was found.");

                return;
            }

            // Use the default microphone.
            MicrophoneName = Microphone.devices[0];

            Debug.Log(
                $"[OpenWakeWordManager] Starting microphone: {MicrophoneName}");

            // Unity will choose the microphone's supported frequency.
            int minFrequency;
            int maxFrequency;

            Microphone.GetDeviceCaps(
                MicrophoneName,
                out minFrequency,
                out maxFrequency);

            _microphoneFrequency = maxFrequency;

            if (_microphoneFrequency <= 0)
            {
                // Fallback.
                _microphoneFrequency = 48000;
            }

            _microphoneClip = Microphone.Start(
                MicrophoneName,
                true,
                microphoneClipLengthSeconds,
                _microphoneFrequency);

            if (_microphoneClip == null)
            {
                Debug.LogError(
                    "[OpenWakeWordManager] Microphone.Start returned null.");

                return;
            }

            // Allocate a reasonably sized read buffer.
            //
            // 20 ms at the microphone frequency.
            int bufferSamples =
                Mathf.Max(
                    320,
                    Mathf.CeilToInt(
                        _microphoneFrequency * 0.02f));

            _sourceReadBuffer =
                new float[bufferSamples];

            _microphoneRunning = true;

            if (logMicrophone)
            {
                Debug.Log(
                    $"[OpenWakeWordManager] Microphone frequency: " +
                    $"{_microphoneFrequency} Hz");
            }
        }

        private void StopMicrophone()
        {
            if (!_microphoneRunning)
                return;

            if (!string.IsNullOrEmpty(MicrophoneName))
            {
                if (Microphone.IsRecording(MicrophoneName))
                {
                    Microphone.End(MicrophoneName);
                }
            }

            _microphoneRunning = false;

            _microphoneClip = null;
        }

        // ============================================================
        // READ MICROPHONE
        // ============================================================

        private void ReadMicrophoneSamples()
        {
            if (_microphoneClip == null)
                return;

            int currentPosition =
                Microphone.GetPosition(MicrophoneName);

            if (currentPosition < 0)
                return;

            int clipSamples =
                _microphoneClip.samples;

            if (clipSamples <= 0)
                return;

            // First frame after microphone starts.
            if (_lastMicrophonePosition < 0)
            {
                _lastMicrophonePosition = currentPosition;
                return;
            }

            int samplesAvailable;

            if (currentPosition >= _lastMicrophonePosition)
            {
                samplesAvailable =
                    currentPosition - _lastMicrophonePosition;
            }
            else
            {
                // Microphone circular buffer wrapped.
                samplesAvailable =
                    (clipSamples - _lastMicrophonePosition)
                    + currentPosition;
            }

            if (samplesAvailable <= 0)
                return;

            // Prevent a giant read if the application was paused.
            samplesAvailable =
                Mathf.Min(
                    samplesAvailable,
                    clipSamples);

            int remaining =
                samplesAvailable;

            int readPosition =
                _lastMicrophonePosition;

            while (remaining > 0)
            {
                int contiguousSamples =
                    Mathf.Min(
                        remaining,
                        clipSamples - readPosition);

                if (_sourceReadBuffer == null ||
                    _sourceReadBuffer.Length < contiguousSamples)
                {
                    _sourceReadBuffer =
                        new float[
                            Mathf.Max(
                                contiguousSamples,
                                320)];
                }

                // AudioClip.GetData uses the clip's sample offset.
                //
                // This implementation assumes the microphone is mono.
                // Unity microphone clips normally provide one channel.
                bool success =
                    _microphoneClip.GetData(
                        _sourceReadBuffer,
                        readPosition);

                if (!success)
                {
                    Debug.LogWarning(
                        "[OpenWakeWordManager] AudioClip.GetData failed.");

                    return;
                }

                for (int i = 0; i < contiguousSamples; i++)
                {
                    _sourceSamples.Add(
                        _sourceReadBuffer[i]);
                }

                readPosition += contiguousSamples;

                if (readPosition >= clipSamples)
                    readPosition = 0;

                remaining -= contiguousSamples;
            }

            _lastMicrophonePosition = currentPosition;

            ResampleTo16k();
        }

        // ============================================================
        // RESAMPLING
        // ============================================================

        private void ResampleTo16k()
        {
            if (_sourceSamples.Count < 2)
                return;

            if (_microphoneFrequency == TargetSampleRate)
            {
                // No resampling required.
                const int MaxOutputSamples = 4096;

                int count =
                    Mathf.Min(
                        _sourceSamples.Count,
                        MaxOutputSamples);

                float[] output =
                    new float[count];

                for (int i = 0; i < count; i++)
                {
                    output[i] =
                        _sourceSamples[i];
                }

                _sourceSamples.RemoveRange(0, count);

                _detector.PushSamples(
                    output,
                    output.Length);

                return;
            }

            double sourceStep =
                (double)_microphoneFrequency
                / TargetSampleRate;

            // Need two source samples for interpolation.
            while (_resamplePosition + 1.0 <
                   _sourceSamples.Count)
            {
                int index =
                    (int)_resamplePosition;

                double fraction =
                    _resamplePosition - index;

                float sampleA =
                    _sourceSamples[index];

                float sampleB =
                    _sourceSamples[index + 1];

                float interpolated =
                    Mathf.Lerp(
                        sampleA,
                        sampleB,
                        (float)fraction);

                _detector.PushSamples(
                    new[] { interpolated },
                    1);

                _resamplePosition += sourceStep;
            }

            // Keep the portion of the source buffer that has not
            // yet been consumed.
            int removeCount =
                Mathf.Max(
                    0,
                    (int)_resamplePosition - 1);

            if (removeCount > 0)
            {
                _sourceSamples.RemoveRange(
                    0,
                    removeCount);

                _resamplePosition -= removeCount;
            }
        }

        // ============================================================
        // WAKE WORD
        // ============================================================

        private void OnDetectorDetected(
            WakeWordDetection detection)
        {
            Debug.Log(
                $"[OpenWakeWordManager] Wake word detected: " +
                $"{detection.Name} " +
                $"score={detection.Probability:F3}");

            // Immediately stop the wake-word microphone.
            //
            // MicrophoneRecord will then be able to take ownership.
            StopListening();

            WakeWordDetected?.Invoke(detection);
        }

        // ============================================================
        // CLEANUP
        // ============================================================

        private void OnDestroy()
        {
            StopListening();

            if (_detector != null)
            {
                _detector.Detected -= OnDetectorDetected;
                _detector.Dispose();
                _detector = null;
            }
        }
    }
}

