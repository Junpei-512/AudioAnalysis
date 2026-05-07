using AudioAnalysis.Api.Services;
using Xunit;

namespace AudioAnalysis.Tests.Unit;

public class WaveformExtractorTests
{
    private readonly WaveformExtractor _extractor = new();

    [Theory]
    [InlineData(1000)]
    [InlineData(500)]
    [InlineData(100)]
    public async Task ExtractAsync_ReturnsExactTargetPoints(int targetPoints)
    {
        var samples = new float[44100];
        var result = await _extractor.ExtractAsync(samples, targetPoints);

        Assert.Equal(targetPoints, result.Amplitudes.Length);
    }

    [Fact]
    public async Task ExtractAsync_SilentInput_RmsIsZero()
    {
        var samples = new float[44100];
        var result = await _extractor.ExtractAsync(samples, 1000);

        Assert.Equal(0.0, result.Rms);
    }

    [Fact]
    public async Task ExtractAsync_FullAmplitudeSine_RmsApproximately0707()
    {
        int n = 44100;
        var samples = new float[n];
        for (int i = 0; i < n; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * 440.0 * i / 44100);

        var result = await _extractor.ExtractAsync(samples, 1000);

        // RMS of a unit sine wave ≈ 1/√2 ≈ 0.707
        Assert.InRange(result.Rms, 0.65, 0.75);
    }
}
