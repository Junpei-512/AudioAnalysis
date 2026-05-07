using Microsoft.AspNetCore.Mvc;

namespace AudioAnalysis.Api.Models;

public class AnalyzeFormRequest
{
    public IFormFile File { get; set; } = null!;
    public int FftSize { get; set; } = 4096;
    public int WaveformPoints { get; set; } = 1000;
    public bool IncludeAiComment { get; set; } = false;
}

public class FftFormRequest
{
    public IFormFile File { get; set; } = null!;
    public int FftSize { get; set; } = 4096;
}

public class WaveformFormRequest
{
    public IFormFile File { get; set; } = null!;
    public int WaveformPoints { get; set; } = 1000;
}

public class TonalityFormRequest
{
    public IFormFile File { get; set; } = null!;
    public bool IncludeAiComment { get; set; } = false;
}

public class FileOnlyFormRequest
{
    public IFormFile File { get; set; } = null!;
}
