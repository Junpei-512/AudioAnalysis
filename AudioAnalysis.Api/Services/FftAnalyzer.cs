using AudioAnalysis.Core.Interfaces;
using AudioAnalysis.Core.Models;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

namespace AudioAnalysis.Api.Services;

public class FftAnalyzer : IFftAnalyzer
{
    public Task<FftResult> AnalyzeAsync(float[] samples, int sampleRate, int fftSize, CancellationToken ct = default)
    {
        return Task.Run(() => Analyze(samples, sampleRate, fftSize), ct);
    }

    private FftResult Analyze(float[] samples, int sampleRate, int fftSize)
    {
        // Ensure fftSize is power of 2
        fftSize = NextPowerOfTwo(fftSize);

        // Take a representative window from the middle of the audio
        int start = Math.Max(0, (samples.Length - fftSize) / 2);
        var windowed = new Complex[fftSize];
        for (int i = 0; i < fftSize; i++)
        {
            int sampleIdx = start + i;
            double sample = sampleIdx < samples.Length ? samples[sampleIdx] : 0.0;
            // Hann window
            double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (fftSize - 1)));
            windowed[i] = new Complex(sample * window, 0);
        }

        Fourier.Forward(windowed, FourierOptions.Matlab);

        int halfSize = fftSize / 2;
        var frequencies = new double[halfSize];
        var magnitudes = new double[halfSize];
        double freqResolution = (double)sampleRate / fftSize;

        double maxMag = 0;
        int maxIdx = 0;

        for (int k = 0; k < halfSize; k++)
        {
            frequencies[k] = k * freqResolution;
            magnitudes[k] = windowed[k].Magnitude / fftSize;
            if (magnitudes[k] > maxMag)
            {
                maxMag = magnitudes[k];
                maxIdx = k;
            }
        }

        return new FftResult
        {
            Frequencies = frequencies,
            Magnitudes = magnitudes,
            DominantFrequency = frequencies[maxIdx]
        };
    }

    private static int NextPowerOfTwo(int n)
    {
        int power = 1;
        while (power < n) power <<= 1;
        return power;
    }
}
