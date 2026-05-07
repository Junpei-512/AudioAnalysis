using AudioAnalysis.Api.Services;
using Xunit;

namespace AudioAnalysis.Tests.Unit;

public class KeyEstimatorTests
{
    private readonly KeyEstimator _estimator = new();

    [Fact]
    public async Task EstimateAsync_ConfidenceIsNormalized()
    {
        var samples = GenerateSineWave(440.0, 44100, 3);
        var result = await _estimator.EstimateAsync(samples, 44100);

        Assert.InRange(result.Confidence, 0.0, 1.0);
        Assert.NotEmpty(result.Root);
        Assert.True(result.Mode == "major" || result.Mode == "minor");
        Assert.Equal($"{result.Root} {result.Mode}", result.Display);
    }

    [Fact]
    public async Task EstimateAsync_ShortSample_DoesNotThrow()
    {
        var samples = new float[1024];
        var result = await _estimator.EstimateAsync(samples, 44100);

        Assert.NotNull(result);
        Assert.InRange(result.Confidence, 0.0, 1.0);
    }

    private static float[] GenerateSineWave(double frequency, int sampleRate, int durationSec)
    {
        int totalSamples = sampleRate * durationSec;
        var samples = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
        return samples;
    }
}
