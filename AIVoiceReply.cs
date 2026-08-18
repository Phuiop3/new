using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ============================================================
// AI TEXT TO SPEECH
// ============================================================
//
// Converts AI text into spoken audio.
//
// FLOW:
//
// DemoChat
//    |
//    | AI reply text
//    v
// AITextToSpeech
//    |
//    | HTTP POST
//    v
// TTS SERVICE
//    |
//    | WAV/OGG audio
//    v
// Unity AudioSource
//    |
//    v
// Quest 3 / PC speakers
//
// IMPORTANT
// ----------
// The TTS service must:
//
// 1. Accept HTTP POST
// 2. Receive JSON:
//
//      {
//          "text": "Hello"
//      }
//
// 3. Return an audio file.
//
// Recommended response:
// WAV
//
// Example:
//
// POST http://localhost:5000/tts
//
// Request:
// {
//     "text": "I created the hills for you."
// }
//
// Response:
// audio/wav
//
// ============================================================

public class AITextToSpeech : MonoBehaviour
{
    // =========================================================
    // TTS SETTINGS
    // =========================================================

    [Header("TTS Service")]

    [SerializeField]
    private string ttsUrl =
        "http://localhost:5000/tts";

    [SerializeField]
    private float requestTimeout =
        60f;


    // =========================================================
    // AUDIO
    // =========================================================

    [Header("Audio")]

    [SerializeField]
    private AudioSource audioSource;


    [SerializeField]
    private bool stopPreviousSpeech =
        true;


    [SerializeField]
    private bool speakAutomatically =
        true;


    // =========================================================
    // VOLUME
    // =========================================================

    [Header("Voice Volume")]

    [Range(0f, 1f)]
    [SerializeField]
    private float volume =
        1f;


    // =========================================================
    // STATE
    // =========================================================

    private Coroutine currentSpeechCoroutine;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        EnsureAudioSource();
    }


    // =========================================================
    // ENSURE AUDIO SOURCE
    // =========================================================

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource =
            GetComponent<AudioSource>();


        if (audioSource == null)
        {
            audioSource =
                gameObject.AddComponent<AudioSource>();
        }


        audioSource.playOnAwake =
            false;


        audioSource.loop =
            false;


        audioSource.spatialBlend =
            0f;


        audioSource.volume =
            volume;
    }


    // =========================================================
    // PUBLIC SPEAK
    // =========================================================
    //
    // DemoChat calls:
    //
    // AITextToSpeech.Speak("I created the hills.");
    //
    // =========================================================

    public void Speak(string text)
    {
        if (!speakAutomatically)
        {
            return;
        }


        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }


        EnsureAudioSource();


        if (stopPreviousSpeech)
        {
            StopSpeaking();
        }


        currentSpeechCoroutine =
            StartCoroutine(
                SpeakCoroutine(
                    text.Trim()
                )
            );
    }


    // =========================================================
    // SPEAK COROUTINE
    // =========================================================

    private IEnumerator SpeakCoroutine(
        string text
    )
    {
        Debug.Log(
            "[AITextToSpeech] Speaking: " +
            text
        );


        // -----------------------------------------------------
        // REQUEST JSON
        // -----------------------------------------------------

        TTSRequest request =
            new TTSRequest
            {
                text = text
            };


        string json =
            JsonUtility.ToJson(request);


        byte[] body =
            Encoding.UTF8.GetBytes(
                json
            );


        // -----------------------------------------------------
        // HTTP POST
        // -----------------------------------------------------

        using (
            UnityWebRequest webRequest =
                new UnityWebRequest(
                    ttsUrl,
                    "POST"
                )
        )
        {
            webRequest.uploadHandler =
                new UploadHandlerRaw(
                    body
                );


            webRequest.downloadHandler =
                new DownloadHandlerAudioClip(
                    ttsUrl,
                    AudioType.WAV
                );


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
            // ERROR
            // -------------------------------------------------

            if (
                webRequest.result !=
                UnityWebRequest.Result.Success
            )
            {
                Debug.LogError(
                    "[AITextToSpeech] TTS request failed: " +
                    webRequest.error
                );


                Debug.LogError(
                    "[AITextToSpeech] URL: " +
                    ttsUrl
                );


                currentSpeechCoroutine =
                    null;


                yield break;
            }


            // -------------------------------------------------
            // AUDIO CLIP
            // -------------------------------------------------

            AudioClip clip =
                DownloadHandlerAudioClip.GetContent(
                    webRequest
                );


            if (clip == null)
            {
                Debug.LogError(
                    "[AITextToSpeech] " +
                    "TTS service returned no AudioClip."
                );


                currentSpeechCoroutine =
                    null;


                yield break;
            }


            // -------------------------------------------------
            // PLAY
            // -------------------------------------------------

            EnsureAudioSource();


            audioSource.Stop();


            audioSource.clip =
                clip;


            audioSource.volume =
                volume;


            audioSource.Play();


            Debug.Log(
                "[AITextToSpeech] Audio playing."
            );


            // -------------------------------------------------
            // WAIT FOR AUDIO
            // -------------------------------------------------

            while (
                audioSource != null &&
                audioSource.isPlaying
            )
            {
                yield return null;
            }


            // -------------------------------------------------
            // CLEANUP
            // -------------------------------------------------

            if (audioSource != null)
            {
                audioSource.clip =
                    null;
            }


            Destroy(
                clip
            );


            currentSpeechCoroutine =
                null;
        }
    }


    // =========================================================
    // STOP SPEAKING
    // =========================================================

    public void StopSpeaking()
    {
        if (
            currentSpeechCoroutine !=
            null
        )
        {
            StopCoroutine(
                currentSpeechCoroutine
            );


            currentSpeechCoroutine =
                null;
        }


        EnsureAudioSource();


        if (audioSource != null)
        {
            audioSource.Stop();

            audioSource.clip =
                null;
        }
    }


    // =========================================================
    // IS SPEAKING
    // =========================================================

    public bool IsSpeaking()
    {
        if (audioSource == null)
        {
            return false;
        }


        return audioSource.isPlaying;
    }


    // =========================================================
    // SET VOLUME
    // =========================================================

    public void SetVolume(
        float newVolume
    )
    {
        volume =
            Mathf.Clamp01(
                newVolume
            );


        EnsureAudioSource();


        if (audioSource != null)
        {
            audioSource.volume =
                volume;
        }
    }


    // =========================================================
    // TEST SPEECH
    // =========================================================

    [ContextMenu("Test AI Voice")]
    private void TestVoice()
    {
        Speak(
            "Hello. I am your AI landscape assistant."
        );
    }


    // =========================================================
    // TTS REQUEST
    // =========================================================

    [Serializable]
    private class TTSRequest
    {
        public string text;
    }
}