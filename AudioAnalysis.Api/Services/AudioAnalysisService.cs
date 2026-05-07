using System.Diagnostics;
using AudioAnalysis.Core.Interfaces;
using AudioAnalysis.Core.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport; // ManagedMpegStream: macOS/Linux 対応の純 .NET MP3 デコーダー

namespace AudioAnalysis.Api.Services;

public class AudioAnalysisService : IAudioAnalysisService
{
    private static readonly HashSet<string> AllowedMimeTypes =
        ["audio/mpeg", "audio/wav", "audio/x-wav", "audio/flac", "audio/ogg", "audio/x-flac"];

    private static readonly Dictionary<string, byte[]> FileSignatures = new()
    {
        [".mp3"] = [0xFF, 0xFB],
        [".wav"] = [0x52, 0x49, 0x46, 0x46],  // RIFF
        [".flac"] = [0x66, 0x4C, 0x61, 0x43], // fLaC
        [".ogg"] = [0x4F, 0x67, 0x67, 0x53],  // OggS
    };

    private readonly IBpmDetector _bpmDetector;
    private readonly IKeyEstimator _keyEstimator;
    private readonly IFftAnalyzer _fftAnalyzer;
    private readonly IWaveformExtractor _waveformExtractor;
    private readonly IHarmonicsAnalyzer _harmonicsAnalyzer;
    private readonly ITonalityAnalyzer _tonalityAnalyzer;

    public AudioAnalysisService(
        IBpmDetector bpmDetector,
        IKeyEstimator keyEstimator,
        IFftAnalyzer fftAnalyzer,
        IWaveformExtractor waveformExtractor,
        IHarmonicsAnalyzer harmonicsAnalyzer,
        ITonalityAnalyzer tonalityAnalyzer)
    {
        _bpmDetector = bpmDetector;
        _keyEstimator = keyEstimator;
        _fftAnalyzer = fftAnalyzer;
        _waveformExtractor = waveformExtractor;
        _harmonicsAnalyzer = harmonicsAnalyzer;
        _tonalityAnalyzer = tonalityAnalyzer;
    }

    public async Task<AudioAnalysisResult> AnalyzeAsync(
        Stream audioStream,
        string fileName,
        AudioAnalysisRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Load audio into memory for multi-pass analysis
        using var memStream = new MemoryStream();
        await audioStream.CopyToAsync(memStream, ct);
        memStream.Position = 0;

        var (samples, sampleRate) = DecodeAudio(memStream, fileName);
        double durationSeconds = samples.Length / (double)sampleRate;

        // Run all analyzers in parallel
        var bpmTask = _bpmDetector.DetectAsync(samples, sampleRate, ct);
        var keyTask = _keyEstimator.EstimateAsync(samples, sampleRate, ct);
        var fftTask = _fftAnalyzer.AnalyzeAsync(samples, sampleRate, request.FftSize, ct);
        var waveTask = _waveformExtractor.ExtractAsync(samples, request.WaveformPoints, ct);
        var harmTask = _harmonicsAnalyzer.AnalyzeAsync(samples, sampleRate, ct);
        var tonalTask = _tonalityAnalyzer.AnalyzeAsync(samples, sampleRate, request.IncludeAiComment, ct);

        await Task.WhenAll(bpmTask, keyTask, fftTask, waveTask, harmTask, tonalTask);

        sw.Stop();

        return new AudioAnalysisResult
        {
            FileName = fileName,
            DurationSeconds = Math.Round(durationSeconds, 2),
            SampleRate = sampleRate,
            Bpm = bpmTask.Result,
            Key = keyTask.Result,
            Fft = fftTask.Result,
            Waveform = waveTask.Result,
            Harmonics = harmTask.Result,
            Tonality = tonalTask.Result,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };
    }

    // WAV エンコーディングを問わず float サンプルに変換 (cross-platform)
    private static ISampleProvider ToSampleProviderCrossPlatform(WaveStream reader)
    {
        var fmt = reader.WaveFormat;

        if (fmt.Encoding == WaveFormatEncoding.IeeeFloat)
            return new WaveToSampleProvider(reader);

        if (fmt.Encoding == WaveFormatEncoding.Pcm)
            return fmt.BitsPerSample switch
            {
                8  => new Pcm8BitToSampleProvider(reader),
                16 => new Pcm16BitToSampleProvider(reader),
                24 => new Pcm24BitToSampleProvider(reader),
                32 => new Pcm32BitToSampleProvider(reader),
                _  => throw new InvalidOperationException($"Unsupported PCM bit depth: {fmt.BitsPerSample}")
            };

        // Extensible WAV (24/32-bit など) → ビット深度で変換
        if (fmt.Encoding == WaveFormatEncoding.Extensible)
        {
            // SubFormat が IEEE_FLOAT (GUID: 00000003-...) かどうか確認
            if (fmt is WaveFormatExtensible extFmt)
            {
                var ieeeGuid = new Guid("00000003-0000-0010-8000-00aa00389b71");
                if (extFmt.SubFormat == ieeeGuid)
                    return new WaveToSampleProvider(reader);
            }
            return fmt.BitsPerSample switch
            {
                16 => new Pcm16BitToSampleProvider(reader),
                24 => new Pcm24BitToSampleProvider(reader),
                32 => new Pcm32BitToSampleProvider(reader),
                _  => throw new InvalidOperationException($"Unsupported Extensible bit depth: {fmt.BitsPerSample}")
            };
        }

        // MP3 デコード済み PCM (NLayer 経由) はすでに 16-bit PCM になる
        return fmt.BitsPerSample switch
        {
            16 => new Pcm16BitToSampleProvider(reader),
            32 => new WaveToSampleProvider(reader),
            _  => new Pcm16BitToSampleProvider(reader)
        };
    }

    private static (float[] samples, int sampleRate) DecodeAudio(Stream stream, string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();

        // MP3: ManagedMpegStream (NLayer 純 .NET 実装) → macOS/Linux で動作
        // WAV: WaveFileReader で直読み
        WaveStream reader = ext switch
        {
            ".mp3" => new ManagedMpegStream(stream),
            ".wav" => new WaveFileReader(stream),
            _ => throw new InvalidOperationException($"Unsupported format: {ext}")
        };

        using (reader)
        {
            int sampleRate = reader.WaveFormat.SampleRate;

            ISampleProvider provider = ToSampleProviderCrossPlatform(reader);

            // ステレオ → モノラルへダウンミックス
            if (provider.WaveFormat.Channels > 1)
                provider = provider.ToMono();

            var buffer = new List<float>(sampleRate * 30);
            var chunk = new float[4096];
            int read;
            while ((read = provider.Read(chunk, 0, chunk.Length)) > 0)
                buffer.AddRange(chunk.AsSpan(0, read));

            return (buffer.ToArray(), sampleRate);
        }
    }
}
