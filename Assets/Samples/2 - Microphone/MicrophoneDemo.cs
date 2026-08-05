using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Whisper.Utils;
using Button = UnityEngine.UI.Button;
using Toggle = UnityEngine.UI.Toggle;

namespace Whisper.Samples
{
    public class MicrophoneDemo : MonoBehaviour
    {
        public WhisperManager whisper;
        public MicrophoneRecord microphoneRecord;

        // Drag DemoChat here
        public DemoChat demoChat;

        public bool streamSegments = true;
        public bool printLanguage = true;

        [Header("UI")]
        public Button button;
        public Text buttonText;
        public Text outputText;
        public Text timeText;
        public Dropdown languageDropdown;
        public Toggle translateToggle;
        public Toggle vadToggle;
        public ScrollRect scroll;

        private string _buffer;

        private void Awake()
        {
            whisper.OnNewSegment += OnNewSegment;
            whisper.OnProgress += OnProgressHandler;

            microphoneRecord.OnRecordStop += OnRecordStop;

            button.onClick.AddListener(OnButtonPressed);

            languageDropdown.value = languageDropdown.options.FindIndex(
                op => op.text == whisper.language);

            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

            translateToggle.isOn = whisper.translateToEnglish;
            translateToggle.onValueChanged.AddListener(OnTranslateChanged);

            vadToggle.isOn = microphoneRecord.vadStop;
            vadToggle.onValueChanged.AddListener(OnVadChanged);
        }

        private void OnVadChanged(bool value)
        {
            microphoneRecord.vadStop = value;
        }

        private void OnButtonPressed()
        {
            if (!microphoneRecord.IsRecording)
            {
                microphoneRecord.StartRecord();
                buttonText.text = "Stop";
            }
            else
            {
                microphoneRecord.StopRecord();
                buttonText.text = "Record";
            }
        }

        private async void OnRecordStop(AudioChunk recordedAudio)
        {
            buttonText.text = "Record";
            _buffer = "";

            Stopwatch sw = new Stopwatch();
            sw.Start();

            var res = await whisper.GetTextAsync(
                recordedAudio.Data,
                recordedAudio.Frequency,
                recordedAudio.Channels);

            if (res == null)
                return;

            var time = sw.ElapsedMilliseconds;
            var rate = recordedAudio.Length / (time * 0.001f);

            if (timeText != null)
            {
                timeText.text =
                    $"Time: {time} ms\nRate: {rate:F1}x";
            }

            string transcript = res.Result;

            if (printLanguage)
            {
                transcript += $"\n\nLanguage: {res.Language}";
            }

            if (outputText != null)
            {
                outputText.text = transcript;
            }

            UiUtils.ScrollDown(scroll);

            // Send ONLY the transcript to Ollama
            if (demoChat != null)
            {
                await demoChat.Ask(res.Result);
            }
        }

        private void OnLanguageChanged(int index)
        {
            whisper.language = languageDropdown.options[index].text;
        }

        private void OnTranslateChanged(bool translate)
        {
            whisper.translateToEnglish = translate;
        }

        private void OnProgressHandler(int progress)
        {
            if (timeText != null)
            {
                timeText.text = $"Progress: {progress}%";
            }
        }

        private void OnNewSegment(WhisperSegment segment)
        {
            if (!streamSegments)
                return;

            if (outputText == null)
                return;

            _buffer += segment.Text;
            outputText.text = _buffer + "...";

            UiUtils.ScrollDown(scroll);
        }
    }
}