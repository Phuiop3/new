using ollama;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class DemoChat : MonoBehaviour
{
    [Header("Model")]
    [SerializeField]
    private string demoModel = "gemma3:4b";

    [Header("Output")]
    [SerializeField]
    private TMP_Text llmOutput;

    private Queue<string> buffer;
    private bool isStreaming;

    private bool bold;
    private bool italic;

    private void Awake()
    {
        Ollama.Launch();
    }

    private void Start()
    {
        buffer = new Queue<string>();
        Ollama.InitChat();
    }

    private void OnEnable()
    {
        Ollama.OnStreamFinished += StreamFinished;
    }

    private void OnDisable()
    {
        Ollama.OnStreamFinished -= StreamFinished;
    }

    private void StreamFinished()
    {
        isStreaming = false;
    }

    private void LateUpdate()
    {
        if (!isStreaming)
            return;

        while (buffer.TryDequeue(out string text))
        {
            text = text.Replace("\n\n", "\n");

            if (text.Contains("**"))
            {
                bold = !bold;
                text = text.Replace("**", bold ? "<b>" : "</b>");
            }

            if (text.Contains("*"))
            {
                italic = !italic;
                text = text.Replace("*", italic ? "<i>" : "</i>");
            }

            llmOutput.text += text;
        }
    }

    public async Task Ask(string prompt)
    {
        if (isStreaming)
            return;

        llmOutput.text = "";

        bold = false;
        italic = false;
        isStreaming = true;

        await Ollama.ChatStream(
            text => buffer.Enqueue(text),
            demoModel,
            prompt);

        isStreaming = false;
    }
}