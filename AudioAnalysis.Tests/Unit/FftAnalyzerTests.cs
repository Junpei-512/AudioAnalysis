using AudioAnalysis.Api.Services;
using Xunit;

namespace AudioAnalysis.Tests.Unit;

public class FftAnalyzerTests
{
    private readonly FftAnalyzer _analyzer = new();

    [Theory]
    [InlineData(440.0, 44100, 4096)]
    [InlineData(880.0, 44100, 2048)]
    public async Task AnalyzeAsync_SingleFrequency_DominantFrequencyApproximate(
        double frequency, int sampleRate, int fftSize)
    {
        var samples = GenerateSine(frequency, sampleRate, 2);
        var result = await _analyzer.AnalyzeAsync(samples, sampleRate, fftSize);

        // Dominant frequency should be within one FFT bin of the actual
        double freqResolution = (double)sampleRate / fftSize;
        Assert.InRange(result.DominantFrequency, frequency - freqResolution, frequency + freqResolution * 2);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsSizeEqualToHalfFftSize()
    {
        var samples = GenerateSine(440, 44100, 1);
        int fftSize = 4096;
        var result = await _analyzer.AnalyzeAsync(samples, 44100, fftSize);

        Assert.Equal(fftSize / 2, result.Frequencies.Length);
        Assert.Equal(fftSize / 2, result.Magnitudes.Length);
    }

    [Fact]
    public async Task AnalyzeAsync_FrequenciesAreMonotonicallyIncreasing()
    {
        var samples = GenerateSine(440, 44100, 1);
        var result = await _analyzer.AnalyzeAsync(samples, 44100, 4096);

        for (int i = 1; i < result.Frequencies.Length; i++)
            Assert.True(result.Frequencies[i] > result.Frequencies[i - 1]);
    }

    private static float[] GenerateSine(double freq, int sampleRate, int durationSec)
    {
        int n = sampleRate * durationSec;
        var s = new float[n];
        for (int i = 0; i < n; i++)
            s[i] = (float)Math.Sin(2 * Math.PI * freq * i / sampleRate);
        return s;
    }
}
