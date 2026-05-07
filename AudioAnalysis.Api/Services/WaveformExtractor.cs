using AudioAnalysis.Core.Interfaces;
using AudioAnalysis.Core.Models;

namespace AudioAnalysis.Api.Services;

public class WaveformExtractor : IWaveformExtractor
{
    public Task<WaveformResult> ExtractAsync(float[] samples, int targetPoints, CancellationToken ct = default)
    {
        return Task.Run(() => Extract(samples, targetPoints), ct);
    }

    private WaveformResult Extract(float[] samples, int targetPoints)
    {
        int chunkSize = Math.Max(1, samples.Length / targetPoints);
        var amplitudes = new float[targetPoints];
        double sumSquares = 0;

        for (int i = 0; i < targetPoints; i++)
        {
            int start = i * chunkSize;
            int end = Math.Min(start + chunkSize, samples.Length);
            double rms = 0;
            for (int j = start; j < end; j++)
                rms += samples[j] * (double)samples[j];
            rms = Math.Sqrt(rms / (end - start));

            // Preserve sign from peak sample in chunk
            float peak = 0;
            for (int j = start; j < end; j++)
                if (Math.Abs(samples[j]) > Math.Abs(peak))
                    peak = samples[j];

            amplitudes[i] = (float)(peak >= 0 ? rms : -rms);
            sumSquares += rms * rms;
        }

        double overallRms = Math.Sqrt(sumSquares / targetPoints);

        return new WaveformResult
        {
            Amplitudes = amplitudes,
            Rms = Math.Round(overallRms, 4)
        };
    }
}
