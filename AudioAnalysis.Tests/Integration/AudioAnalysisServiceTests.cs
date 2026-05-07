using AudioAnalysis.Api.Services;
using AudioAnalysis.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AudioAnalysis.Tests.Integration;

public class AudioAnalysisServiceTests
{
    private static AudioAnalysisService BuildService()
    {
        var config = new ConfigurationBuilder().Build();
        var keyEstimator = new KeyEstimator();
        var logger = NullLogger<TonalityAnalyzer>.Instance;

        return new AudioAnalysisService(
            new BpmDetector(),
            keyEstimator,
            new FftAnalyzer(),
            new WaveformExtractor(),
            new HarmonicsAnalyzer(),
            new TonalityAnalyzer(keyEstimator, logger, config));
    }

    [Fact]
    public async Task AnalyzeAsync_MinimalWavStream_ReturnsCompleteResult()
    {
        var service = BuildService();
        var wavStream = CreateMinimalWav(sampleRate: 44100, durationSec: 3, frequency: 440);
        var request = new AudioAnalysisRequest { FftSize = 1024, WaveformPoints = 100 };

        var result = await service.AnalyzeAsync(wavStream, "test.wav", request);

        Assert.Equal("test.wav", result.FileName);
        Assert.True(result.DurationSeconds > 0);
        Assert.Equal(44100, result.SampleRate);
        Assert.True(result.Bpm.Value > 0);
        Assert.NotEmpty(result.Key.Display);
        Assert.Equal(512, result.Fft.Frequencies.Length); // fftSize/2 = 1024/2
        Assert.Equal(100, result.Waveform.Amplitudes.Length);
        Assert.NotEmpty(result.Harmonics.Partials);
        Assert.NotEmpty(result.Tonality.OverallKey);
        Assert.True(result.ProcessingTimeMs > 0);
    }

    private static Stream CreateMinimalWav(int sampleRate, int durationSec, double frequency)
    {
        int numSamples = sampleRate * durationSec;
        int dataSize = numSamples * 2; // 16-bit PCM

        var ms = new MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);

        // RIFF header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);            // chunk size
        writer.Write((short)1);     // PCM
        writer.Write((short)1);     // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // byte rate
        writer.Write((short)2);     // block align
        writer.Write((short)16);    // bits per sample

        // data chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        for (int i = 0; i < numSamples; i++)
        {
            double sample = Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            writer.Write((short)(sample * 16000));
        }

        ms.Position = 0;
        return ms;
    }
}
