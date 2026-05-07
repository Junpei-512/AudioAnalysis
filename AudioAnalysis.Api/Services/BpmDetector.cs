using AudioAnalysis.Core.Interfaces;
using AudioAnalysis.Core.Models;

namespace AudioAnalysis.Api.Services;

public class BpmDetector : IBpmDetector
{
    private const int FrameSize = 512;
    private const int MinBpm = 40;
    private const int MaxBpm = 220;

    public Task<BpmResult> DetectAsync(float[] samples, int sampleRate, CancellationToken ct = default)
    {
        return Task.Run(() => Detect(samples, sampleRate), ct);
    }

    private BpmResult Detect(float[] samples, int sampleRate)
    {
        // Calculate RMS energy per frame
        int frameCount = samples.Length / FrameSize;
        var energy = new double[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            double sum = 0;
            for (int j = 0; j < FrameSize; j++)
            {
                double s = samples[i * FrameSize + j];
                sum += s * s;
            }
            energy[i] = Math.Sqrt(sum / FrameSize);
        }

        // Onset strength: positive energy differences
        var onset = new double[frameCount];
        for (int i = 1; i < frameCount; i++)
        {
            double diff = energy[i] - energy[i - 1];
            onset[i] = diff > 0 ? diff : 0;
        }

        // Autocorrelation to find periodicity
        double framesPerSecond = (double)sampleRate / FrameSize;
        int minLag = (int)(framesPerSecond * 60.0 / MaxBpm);
        int maxLag = (int)(framesPerSecond * 60.0 / MinBpm);
        maxLag = Math.Min(maxLag, frameCount - 1);

        double bestCorr = double.MinValue;
        int bestLag = minLag;

        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double corr = 0;
            for (int i = 0; i < frameCount - lag; i++)
                corr += onset[i] * onset[i + lag];
            if (corr > bestCorr)
            {
                bestCorr = corr;
                bestLag = lag;
            }
        }

        double bpm = 60.0 * framesPerSecond / bestLag;

        // Normalize confidence to [0,1]
        double maxPossibleCorr = 0;
        for (int i = 0; i < frameCount; i++)
            maxPossibleCorr += onset[i] * onset[i];

        double confidence = maxPossibleCorr > 0
            ? Math.Min(bestCorr / maxPossibleCorr, 1.0)
            : 0;

        return new BpmResult
        {
            Value = Math.Round(bpm, 1),
            Confidence = Math.Round(confidence, 2)
        };
    }
}
