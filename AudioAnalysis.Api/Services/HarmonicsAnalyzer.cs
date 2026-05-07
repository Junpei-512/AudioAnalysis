using AudioAnalysis.Core.Interfaces;
using AudioAnalysis.Core.Models;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

namespace AudioAnalysis.Api.Services;

public class HarmonicsAnalyzer : IHarmonicsAnalyzer
{
    private const int MaxHarmonics = 4;
    private const int FftSize = 8192;

    public Task<HarmonicsResult> AnalyzeAsync(float[] samples, int sampleRate, CancellationToken ct = default)
    {
        return Task.Run(() => Analyze(samples, sampleRate), ct);
    }

    private HarmonicsResult Analyze(float[] samples, int sampleRate)
    {
        int size = Math.Min(FftSize, samples.Length);
        var windowed = new Complex[size];
        for (int i = 0; i < size; i++)
        {
            double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1)));
            windowed[i] = new Complex(samples[i] * window, 0);
        }

        Fourier.Forward(windowed, FourierOptions.Matlab);

        var magnitudes = new double[size / 2];
        for (int k = 0; k < magnitudes.Length; k++)
            magnitudes[k] = windowed[k].Magnitude / size;

        // HPS: multiply downsampled spectra to find fundamental
        var hps = (double[])magnitudes.Clone();
        for (int r = 2; r <= MaxHarmonics; r++)
        {
            for (int k = 0; k < hps.Length / r; k++)
                hps[k] *= magnitudes[k * r];
        }

        // Find peak in HPS (skip DC and very low frequencies < 50 Hz)
        int minBin = (int)Math.Ceiling(50.0 * size / sampleRate);
        int maxBin = (int)Math.Floor(2000.0 * size / sampleRate);
        maxBin = Math.Min(maxBin, hps.Length - 1);

        int fundamentalBin = minBin;
        double maxHps = hps[minBin];
        for (int k = minBin + 1; k <= maxBin; k++)
        {
            if (hps[k] > maxHps) { maxHps = hps[k]; fundamentalBin = k; }
        }

        double freqResolution = (double)sampleRate / size;
        double fundamental = fundamentalBin * freqResolution;

        // Build partials list
        var partials = new List<Partial>();
        double baseMag = magnitudes[fundamentalBin];
        for (int h = 1; h <= MaxHarmonics; h++)
        {
            int bin = (int)Math.Round(fundamentalBin * (double)h);
            if (bin >= magnitudes.Length) break;
            double mag = baseMag > 0 ? magnitudes[bin] / baseMag : 0;
            partials.Add(new Partial
            {
                Harmonic = h,
                Frequency = Math.Round(fundamental * h, 2),
                Magnitude = Math.Round(mag, 4)
            });
        }

        return new HarmonicsResult
        {
            Fundamental = Math.Round(fundamental, 2),
            Partials = partials
        };
    }
}
