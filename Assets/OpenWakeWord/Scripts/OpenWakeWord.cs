using System;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;

namespace SentisModels
{
public sealed class OpenWakeWord : IDisposable
{
    // Vanilla openWakeWord streaming pipeline (mirrors dscripka/openWakeWord): chunk -> mel frames -> 96-d feature -> score. No stride/decimation/RMS gate.
    const int ChunkSamples = 1280;
    const int MelHopSamples = 160;
    const int MelContextSamples = MelHopSamples * 3;          // 480: lookback fed to the mel model, yields no extra frames
    const int MelInputSamples = ChunkSamples + MelContextSamples;
    const int MelBins = 32;
    const int MelWindowFrames = 76;
    const int EmbeddingSize = 96;
    const int TargetSampleRate = 16000;
    const int MaxRawSamples = TargetSampleRate * 10;
    const int MaxMelFrames = 10 * 97;                         // 970, matches reference melspectrogram_max_len
    const int MaxFeatureFrames = 120;                         // ~10 s of features, matches reference feature_buffer_max_len
    const float DefaultDetectionThreshold = 0.5f;
    const float DebounceSeconds = 2f;
    // Skip firing until the mel and feature buffers are fully real, avoiding startup false positives.
    const int WarmupChunks = 12;
    const string WakeWordName = "alexa";
    const string DefaultMelSpectrogramModelFile = "melspectrogram_fp16.sentis";
    const string DefaultEmbeddingModelFile = "embedding_model_fp16.sentis";
    const string DefaultWakeWordModelFile = "WakeWord/alexa_v0.1_fp16.sentis";

    readonly float m_DetectionThreshold;
    readonly bool m_LogScores;

    public event Action<WakeWordDetection> Detected;
    public float LastScore { get; private set; }
    public float LastRms { get; private set; }
    public float LastInferenceMilliseconds { get; private set; }
    // Latches true once the mel/feature buffers prime past WarmupChunks the first time. One-shot:
    // ResetDetector does NOT clear it, so the UI can keep the wake button disabled until the detector is live.
    public bool IsWarmedUp { get; private set; }

    Worker m_MelWorker;
    Worker m_EmbeddingWorker;
    Worker m_WakeWordWorker;

    readonly FloatRingBuffer m_PendingSamples = new FloatRingBuffer(MaxRawSamples);
    readonly FloatRingBuffer m_RawSamples = new FloatRingBuffer(MaxRawSamples);
    readonly float[] m_ChunkSamples = new float[ChunkSamples];
    readonly float[] m_MelWindowInput = new float[MelWindowFrames * MelBins];

    float[] m_MelBuffer = new float[MaxMelFrames * MelBins];
    float[] m_FeatureBuffer = new float[MaxFeatureFrames * EmbeddingSize];
    float[] m_MelInputSamples = Array.Empty<float>();
    float[] m_WakeWordInput = Array.Empty<float>();
    int m_MelStartFrame;
    int m_MelFrameCount;
    int m_FeatureStartFrame;
    int m_FeatureFrameCount;
    int m_WakeWordInputFrames = 16;
    int m_ChunksSinceReset;
    float m_LastDetectionTime = -999f;
    bool m_IsListening = true;

    public OpenWakeWord(float detectionThreshold = DefaultDetectionThreshold, bool logScores = false)
    {
        m_DetectionThreshold = detectionThreshold;
        m_LogScores = logScores;
    }

    public bool Load(
        string modelRoot,
        string melSpectrogramModelFile = DefaultMelSpectrogramModelFile,
        string embeddingModelFile = DefaultEmbeddingModelFile,
        string wakeWordModelFile = DefaultWakeWordModelFile)
    {
        DisposeWorkers();

        Model melModel;
        Model embeddingModel;
        Model wakeWordModel;
        try
        {
            melModel = ModelLoader.Load(Path.Combine(modelRoot, melSpectrogramModelFile));
            embeddingModel = ModelLoader.Load(Path.Combine(modelRoot, embeddingModelFile));
            wakeWordModel = ModelLoader.Load(Path.Combine(modelRoot, wakeWordModelFile));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }

        m_WakeWordInputFrames = InferWakeWordInputFrames(wakeWordModel, m_WakeWordInputFrames);
        var wakeWordScoreModel = BuildWakeWordScoreModel(wakeWordModel);

        // Adreno GPUCompute pins score at ~0 on-device; CPU works on Android, GPUCompute works in Editor. Runtime check keeps Editor on GPU regardless of build target.
        var backend = Application.platform == RuntimePlatform.Android ? BackendType.CPU : BackendType.GPUCompute;
        m_MelWorker = new Worker(melModel, backend);
        m_EmbeddingWorker = new Worker(embeddingModel, backend);
        m_WakeWordWorker = new Worker(wakeWordScoreModel, backend);

        m_IsListening = true;
        ResetDetector();
        return true;
    }

    static Model BuildWakeWordScoreModel(Model wakeWordModel)
    {
        var graph = new FunctionalGraph();
        var input = graph.AddInput(wakeWordModel, 0);
        var outputs = Functional.Forward(wakeWordModel, input);
        var score = Functional.ReduceMax(outputs[0].Ravel(), 0);
        return graph.Compile(score);
    }

    static int InferWakeWordInputFrames(Model model, int fallback)
    {
        var dynamicShape = model.inputs[0].shape;
        if (!dynamicShape.IsStatic())
            return fallback;

        var shape = dynamicShape.ToTensorShape();
        return shape.rank >= 3 ? Mathf.Max(1, shape[1]) : fallback;
    }

    // Drain one chunk per call; call this regularly (e.g. once per frame) while listening.
    public void Pump()
    {
        if (!m_IsListening || m_PendingSamples.Count < ChunkSamples)
            return;

        m_PendingSamples.CopyTo(0, m_ChunkSamples, 0, ChunkSamples);
        m_PendingSamples.RemoveFromStart(ChunkSamples);

        var rms = CalculateRms(m_ChunkSamples);
        // openWakeWord feeds int16-range audio to the mel model; Unity mic samples are [-1, 1], so scale up.
        for (var i = 0; i < ChunkSamples; i++)
            m_ChunkSamples[i] *= short.MaxValue;
        m_RawSamples.AddRange(m_ChunkSamples, ChunkSamples);

        ProcessAudioChunk(rms);
    }

    public void SetListening(bool listening)
    {
        m_IsListening = listening;
    }

    public void ResetDetector()
    {
        // Mel buffer starts as ones (reference: np.ones((76, 32))); features start empty until 16 real ones accumulate.
        Array.Clear(m_MelBuffer, 0, m_MelBuffer.Length);
        Array.Clear(m_FeatureBuffer, 0, m_FeatureBuffer.Length);
        var onesLength = MelWindowFrames * MelBins;
        for (var i = 0; i < onesLength; i++)
            m_MelBuffer[i] = 1f;

        m_PendingSamples.Clear();
        m_RawSamples.Clear();
        m_MelStartFrame = 0;
        m_MelFrameCount = MelWindowFrames;
        m_FeatureStartFrame = 0;
        m_FeatureFrameCount = 0;
        m_ChunksSinceReset = 0;
        m_LastDetectionTime = -999f;
    }

    public void PushSamples(float[] samples, int length)
    {
        if (m_IsListening)
            m_PendingSamples.AddRange(samples, length);
    }

    // Runs the three Sentis passes synchronously in one frame; async readback yielded ~3 frames per chunk and added wake latency.
    void ProcessAudioChunk(float rms)
    {
        var inferenceWatch = System.Diagnostics.Stopwatch.StartNew();
        AppendMelFrames();
        AppendEmbedding();

        m_ChunksSinceReset++;
        if (!IsWarmedUp && m_ChunksSinceReset >= WarmupChunks)
            IsWarmedUp = true;

        var score = 0f;
        if (m_FeatureFrameCount >= m_WakeWordInputFrames)
            score = PredictWakeWord();
        inferenceWatch.Stop();

        var inferenceMs = (float)inferenceWatch.Elapsed.TotalMilliseconds;
        LastScore = score;
        LastRms = rms;
        LastInferenceMilliseconds = inferenceMs;

        if (m_LogScores)
            Debug.Log($"[OpenWakeWord] {WakeWordName}: {score:0.000}, rms {rms:0.0000}, inference {inferenceMs:0.0} ms");

        if (m_ChunksSinceReset < WarmupChunks)
            return;

        if (score < m_DetectionThreshold)
            return;

        if (Time.unscaledTime - m_LastDetectionTime < DebounceSeconds)
            return;

        m_LastDetectionTime = Time.unscaledTime;
        Detected?.Invoke(new WakeWordDetection(WakeWordName, score, inferenceMs));
    }

    void AppendMelFrames()
    {
        var sampleCount = Mathf.Min(MelInputSamples, m_RawSamples.Count);
        if (m_MelInputSamples.Length != sampleCount)
            m_MelInputSamples = new float[sampleCount];
        m_RawSamples.CopyLatest(sampleCount, m_MelInputSamples, 0);

        using var input = new Tensor<float>(new TensorShape(1, sampleCount), m_MelInputSamples);
        m_MelWorker.Schedule(input);

        var output = m_MelWorker.PeekOutput() as Tensor<float>;
        using var cpuOutput = output.ReadbackAndClone();
        var frameCount = cpuOutput.shape.length / MelBins;

        for (var frame = 0; frame < frameCount; frame++)
            AppendMelFrame(cpuOutput, frame * MelBins);
    }

    void AppendEmbedding()
    {
        CopyLatestFrames(m_MelBuffer, m_MelStartFrame, m_MelFrameCount, MaxMelFrames, MelBins, MelWindowFrames, m_MelWindowInput);

        using var input = new Tensor<float>(new TensorShape(1, MelWindowFrames, MelBins, 1), m_MelWindowInput);
        m_EmbeddingWorker.Schedule(input);

        var output = m_EmbeddingWorker.PeekOutput() as Tensor<float>;
        using var cpuOutput = output.ReadbackAndClone();

        AppendFeatureFrame(cpuOutput, cpuOutput.shape.length - EmbeddingSize);
    }

    float PredictWakeWord()
    {
        var featureLength = m_WakeWordInputFrames * EmbeddingSize;
        if (m_WakeWordInput.Length != featureLength)
            m_WakeWordInput = new float[featureLength];

        CopyLatestFrames(m_FeatureBuffer, m_FeatureStartFrame, m_FeatureFrameCount, MaxFeatureFrames, EmbeddingSize, m_WakeWordInputFrames, m_WakeWordInput);

        using var input = new Tensor<float>(new TensorShape(1, m_WakeWordInputFrames, EmbeddingSize), m_WakeWordInput);
        m_WakeWordWorker.Schedule(input);

        var output = m_WakeWordWorker.PeekOutput() as Tensor<float>;
        using var cpuOutput = output.ReadbackAndClone();
        return cpuOutput[0];
    }

    void AppendMelFrame(Tensor<float> source, int sourceOffset)
    {
        var targetOffset = ReserveNextRingFrame(ref m_MelStartFrame, ref m_MelFrameCount, MaxMelFrames) * MelBins;
        // Reference melspec transform: spec / 10 + 2.
        for (var bin = 0; bin < MelBins; bin++)
            m_MelBuffer[targetOffset + bin] = source[sourceOffset + bin] / 10f + 2f;
    }

    void AppendFeatureFrame(Tensor<float> source, int sourceOffset)
    {
        var targetOffset = ReserveNextRingFrame(ref m_FeatureStartFrame, ref m_FeatureFrameCount, MaxFeatureFrames) * EmbeddingSize;
        for (var i = 0; i < EmbeddingSize; i++)
            m_FeatureBuffer[targetOffset + i] = source[sourceOffset + i];
    }

    static int ReserveNextRingFrame(ref int startFrame, ref int frameCount, int capacity)
    {
        if (frameCount < capacity)
            return frameCount++;

        var targetFrame = startFrame;
        startFrame = (startFrame + 1) % capacity;
        return targetFrame;
    }

    static void CopyLatestFrames(float[] source, int startFrame, int frameCount, int capacity, int columns, int framesToCopy, float[] destination)
    {
        var physicalStartFrame = startFrame + frameCount - framesToCopy;
        if (physicalStartFrame >= capacity)
            physicalStartFrame -= capacity;

        var firstFrames = Mathf.Min(framesToCopy, capacity - physicalStartFrame);
        var firstLength = firstFrames * columns;
        Array.Copy(source, physicalStartFrame * columns, destination, 0, firstLength);

        var remainingFrames = framesToCopy - firstFrames;
        if (remainingFrames > 0)
            Array.Copy(source, 0, destination, firstLength, remainingFrames * columns);
    }

    static float CalculateRms(float[] samples)
    {
        var sum = 0f;
        for (var i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        return Mathf.Sqrt(sum / samples.Length);
    }

    public void Dispose()
    {
        m_IsListening = false;
        DisposeWorkers();
    }

    void DisposeWorkers()
    {
        m_MelWorker?.Dispose();
        m_EmbeddingWorker?.Dispose();
        m_WakeWordWorker?.Dispose();
        m_MelWorker = null;
        m_EmbeddingWorker = null;
        m_WakeWordWorker = null;
    }

    sealed class FloatRingBuffer
    {
        readonly float[] m_Buffer;
        int m_Start;
        int m_Count;

        public FloatRingBuffer(int capacity)
        {
            m_Buffer = new float[capacity];
        }

        public int Count => m_Count;
        public int Capacity => m_Buffer.Length;

        public float this[int index] => m_Buffer[PhysicalIndex(index)];

        public void Clear()
        {
            m_Start = 0;
            m_Count = 0;
        }

        public void Add(float value)
        {
            if (m_Count == m_Buffer.Length)
            {
                m_Buffer[m_Start] = value;
                m_Start = (m_Start + 1) % m_Buffer.Length;
                return;
            }

            m_Buffer[PhysicalIndex(m_Count)] = value;
            m_Count++;
        }

        public void AddRange(float[] source, int length)
        {
            AddRange(source, 0, length);
        }

        public void AddRange(float[] source, int sourceIndex, int length)
        {
            if (length >= m_Buffer.Length)
            {
                Array.Copy(source, sourceIndex + length - m_Buffer.Length, m_Buffer, 0, m_Buffer.Length);
                m_Start = 0;
                m_Count = m_Buffer.Length;
                return;
            }

            var overflow = m_Count + length - m_Buffer.Length;
            if (overflow > 0)
                RemoveFromStart(overflow);

            CopyInto(source, sourceIndex, PhysicalIndex(m_Count), length);
            m_Count += length;
        }

        public void CopyTo(int sourceIndex, float[] destination, int destinationIndex, int length)
        {
            var physicalIndex = PhysicalIndex(sourceIndex);
            var firstLength = Mathf.Min(length, m_Buffer.Length - physicalIndex);
            Array.Copy(m_Buffer, physicalIndex, destination, destinationIndex, firstLength);

            var remaining = length - firstLength;
            if (remaining > 0)
                Array.Copy(m_Buffer, 0, destination, destinationIndex + firstLength, remaining);
        }

        public void CopyLatest(int length, float[] destination, int destinationIndex)
        {
            CopyTo(m_Count - length, destination, destinationIndex, length);
        }

        public void RemoveFromStart(int length)
        {
            m_Start = PhysicalIndex(length);
            m_Count -= length;
        }

        int PhysicalIndex(int logicalIndex)
        {
            var index = m_Start + logicalIndex;
            return index >= m_Buffer.Length ? index - m_Buffer.Length : index;
        }

        void CopyInto(float[] source, int sourceIndex, int destinationIndex, int length)
        {
            var firstLength = Mathf.Min(length, m_Buffer.Length - destinationIndex);
            Array.Copy(source, sourceIndex, m_Buffer, destinationIndex, firstLength);

            var remaining = length - firstLength;
            if (remaining > 0)
                Array.Copy(source, sourceIndex + firstLength, m_Buffer, 0, remaining);
        }
    }
}

public readonly struct WakeWordDetection
{
    public readonly string Name;
    public readonly float Probability;
    public readonly float InferenceMilliseconds;

    public WakeWordDetection(string name, float probability, float inferenceMilliseconds)
    {
        Name = name;
        Probability = probability;
        InferenceMilliseconds = inferenceMilliseconds;
    }
}
}
