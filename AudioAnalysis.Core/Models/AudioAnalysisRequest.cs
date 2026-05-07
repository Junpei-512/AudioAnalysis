namespace AudioAnalysis.Core.Models;

public class AudioAnalysisRequest
{
    public int FftSize { get; set; } = 4096;
    public int WaveformPoints { get; set; } = 1000;
    public bool IncludeAiComment { get; set; } = false;
}
