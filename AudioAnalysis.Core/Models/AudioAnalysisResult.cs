namespace AudioAnalysis.Core.Models;

public class AudioAnalysisResult
{
    public string FileName { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public int SampleRate { get; set; }
    public BpmResult Bpm { get; set; } = new();
    public KeyResult Key { get; set; } = new();
    public FftResult Fft { get; set; } = new();
    public WaveformResult Waveform { get; set; } = new();
    public HarmonicsResult Harmonics { get; set; } = new();
    public TonalityResult Tonality { get; set; } = new();
    public long ProcessingTimeMs { get; set; }
}

public class BpmResult
{
    public double Value { get; set; }
    public double Confidence { get; set; }
}

public class KeyResult
{
    public string Root { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class FftResult
{
    public double[] Frequencies { get; set; } = [];
    public double[] Magnitudes { get; set; } = [];
    public double DominantFrequency { get; set; }
}

public class WaveformResult
{
    public float[] Amplitudes { get; set; } = [];
    public double Rms { get; set; }
}

public class HarmonicsResult
{
    public double Fundamental { get; set; }
    public List<Partial> Partials { get; set; } = [];
}

public class Partial
{
    public int Harmonic { get; set; }
    public double Frequency { get; set; }
    public double Magnitude { get; set; }
}

public class TonalityResult
{
    public string OverallKey { get; set; } = string.Empty;
    public List<TonalSection> Sections { get; set; } = [];
    public string? AiComment { get; set; }
}

public class TonalSection
{
    public double StartSec { get; set; }
    public double EndSec { get; set; }
    public string Key { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
