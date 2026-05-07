using AudioAnalysis.Core.Models;

namespace AudioAnalysis.Core.Interfaces;

public interface IAudioAnalysisService
{
    Task<AudioAnalysisResult> AnalyzeAsync(
        Stream audioStream,
        string fileName,
        AudioAnalysisRequest request,
        CancellationToken ct = default);
}

public interface IBpmDetector
{
    Task<BpmResult> DetectAsync(float[] samples, int sampleRate, CancellationToken ct = default);
}

public interface IKeyEstimator
{
    Task<KeyResult> EstimateAsync(float[] samples, int sampleRate, CancellationToken ct = default);
}

public interface IFftAnalyzer
{
    Task<FftResult> AnalyzeAsync(float[] samples, int sampleRate, int fftSize, CancellationToken ct = default);
}

public interface IWaveformExtractor
{
    Task<WaveformResult> ExtractAsync(float[] samples, int targetPoints, CancellationToken ct = default);
}

public interface IHarmonicsAnalyzer
{
    Task<HarmonicsResult> AnalyzeAsync(float[] samples, int sampleRate, CancellationToken ct = default);
}

public interface ITonalityAnalyzer
{
    Task<TonalityResult> AnalyzeAsync(
        float[] samples,
        int sampleRate,
        bool includeAiComment,
        CancellationToken ct = default);
}
