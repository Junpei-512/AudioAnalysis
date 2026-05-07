using AudioAnalysis.Core.Interfaces;
using AudioAnalysis.Core.Models;

namespace AudioAnalysis.Api.Services;

public class KeyEstimator : IKeyEstimator
{
    // Krumhansl-Schmuckler key profiles
    private static readonly double[] MajorProfile =
        [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88];

    private static readonly double[] MinorProfile =
        [6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17];

    private static readonly string[] NoteNames =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public Task<KeyResult> EstimateAsync(float[] samples, int sampleRate, CancellationToken ct = default)
    {
        return Task.Run(() => Estimate(samples, sampleRate), ct);
    }

    private KeyResult Estimate(float[] samples, int sampleRate)
    {
        var chroma = ComputeChroma(samples, sampleRate);

        double bestCorr = double.MinValue;
        int bestRoot = 0;
        bool bestIsMajor = true;

        for (int r = 0; r < 12; r++)
        {
            double majorCorr = PearsonCorrelation(chroma, RotateProfile(MajorProfile, r));
            double minorCorr = PearsonCorrelation(chroma, RotateProfile(MinorProfile, r));

            if (majorCorr > bestCorr) { bestCorr = majorCorr; bestRoot = r; bestIsMajor = true; }
            if (minorCorr > bestCorr) { bestCorr = minorCorr; bestRoot = r; bestIsMajor = false; }
        }

        string root = NoteNames[bestRoot];
        string mode = bestIsMajor ? "major" : "minor";

        // Normalize correlation [-1,1] to confidence [0,1]
        double confidence = Math.Round((bestCorr + 1.0) / 2.0, 2);

        return new KeyResult
        {
            Root = root,
            Mode = mode,
            Display = $"{root} {mode}",
            Confidence = confidence
        };
    }

    private double[] ComputeChroma(float[] samples, int sampleRate)
    {
        var chroma = new double[12];
        int frameSize = 4096;
        int hopSize = frameSize / 2;
        int frameCount = 0;

        for (int start = 0; start + frameSize <= samples.Length; start += hopSize)
        {
            var frame = new double[frameSize];
            for (int i = 0; i < frameSize; i++)
                frame[i] = samples[start + i];

            // Apply Hann window
            for (int i = 0; i < frameSize; i++)
                frame[i] *= 0.5 * (1 - Math.Cos(2 * Math.PI * i / (frameSize - 1)));

            // Simple DFT-based chroma (efficient enough for key detection)
            for (int pitchClass = 0; pitchClass < 12; pitchClass++)
            {
                double energy = 0;
                // Sum energy from MIDI notes 21 to 108 (piano range)
                for (int midi = 21 + pitchClass; midi <= 108; midi += 12)
                {
                    double freq = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
                    int bin = (int)Math.Round(freq * frameSize / sampleRate);
                    if (bin >= 0 && bin < frameSize / 2)
                    {
                        double re = 0, im = 0;
                        for (int n = 0; n < frameSize; n++)
                        {
                            double angle = 2 * Math.PI * bin * n / frameSize;
                            re += frame[n] * Math.Cos(angle);
                            im -= frame[n] * Math.Sin(angle);
                        }
                        energy += Math.Sqrt(re * re + im * im) / frameSize;
                    }
                }
                chroma[pitchClass] += energy;
            }
            frameCount++;
        }

        if (frameCount > 0)
            for (int i = 0; i < 12; i++)
                chroma[i] /= frameCount;

        return chroma;
    }

    private static double[] RotateProfile(double[] profile, int shift)
    {
        var rotated = new double[12];
        for (int i = 0; i < 12; i++)
            rotated[i] = profile[(i + shift) % 12];
        return rotated;
    }

    private static double PearsonCorrelation(double[] x, double[] y)
    {
        double mx = x.Average(), my = y.Average();
        double num = 0, dx2 = 0, dy2 = 0;
        for (int i = 0; i < 12; i++)
        {
            double xi = x[i] - mx, yi = y[i] - my;
            num += xi * yi;
            dx2 += xi * xi;
            dy2 += yi * yi;
        }
        double denom = Math.Sqrt(dx2 * dy2);
        return denom == 0 ? 0 : num / denom;
    }
}
