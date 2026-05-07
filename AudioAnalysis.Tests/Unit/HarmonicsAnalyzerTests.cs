using AudioAnalysis.Api.Services;
using Xunit;

namespace AudioAnalysis.Tests.Unit;

public class HarmonicsAnalyzerTests
{
    private readonly HarmonicsAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_SawtoothWave_FundamentalIsApproximately440()
    {
        // Sawtooth has rich harmonics — HPS works well on it
        int sampleRate = 44100;
        float freq = 440f;
        int n = sampleRate * 2;
        var samples = new float[n];
        for (int i = 0; i < n; i++)
        {
            double phase = (i * freq / sampleRate) % 1.0;
            samples[i] = (float)(2.0 * phase - 1.0); // sawtooth [-1,1]
        }

        var result = await _analyzer.AnalyzeAsync(samples, sampleRate);

        // HPS has ~1 bin resolution: 44100/8192 ≈ 5.4 Hz per bin → ±30 Hz tolerance
        Assert.InRange(result.Fundamental, 400, 480);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsAtLeastOnePartial()
    {
        int sampleRate = 44100;
        var samples = new float[sampleRate];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);

        var result = await _analyzer.AnalyzeAsync(samples, sampleRate);

        Assert.NotEmpty(result.Partials);
        Assert.Equal(1, result.Partials[0].Harmonic);
    }

    [Fact]
    public async Task AnalyzeAsync_FirstPartialMagnitudeIsOne()
    {
        int sampleRate = 44100;
        var samples = new float[sampleRate * 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);

        var result = await _analyzer.AnalyzeAsync(samples, sampleRate);

        Assert.Equal(1.0, result.Partials[0].Magnitude, precision: 4);
    }
}
