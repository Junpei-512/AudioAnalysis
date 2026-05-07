using AudioAnalysis.Api.Services;
using Xunit;

namespace AudioAnalysis.Tests.Unit;

public class BpmDetectorTests
{
    private readonly BpmDetector _detector = new();

    [Fact]
    public async Task DetectAsync_SineWaveBeat_ReturnsApproximateBpm()
    {
        // Arrange: 120 BPM at 44100 Hz = beat every 22050 samples
        int sampleRate = 44100;
        float targetBpm = 120f;
        int samplesPerBeat = (int)(sampleRate * 60f / targetBpm);
        int totalSamples = sampleRate * 10; // 10 seconds

        var samples = new float[totalSamples];
        for (int beat = 0; beat * samplesPerBeat < totalSamples; beat++)
        {
            int start = beat * samplesPerBeat;
            for (int i = 0; i < 512 && start + i < totalSamples; i++)
                samples[start + i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);
        }

        // Act
        var result = await _detector.DetectAsync(samples, sampleRate);

        // Assert: within ±15 BPM (energy-diff method is approximate)
        Assert.InRange(result.Value, targetBpm - 15, targetBpm + 15);
        Assert.InRange(result.Confidence, 0.0, 1.0);
    }

    [Fact]
    public async Task DetectAsync_Silence_ReturnsValueInExtendedRange()
    {
        // Silence has no detectable beat; algorithm returns a best-guess value
        var samples = new float[44100];
        var result = await _detector.DetectAsync(samples, 44100);

        Assert.True(result.Value > 0, "BPM should be positive");
        Assert.InRange(result.Confidence, 0.0, 1.0);
    }
}
