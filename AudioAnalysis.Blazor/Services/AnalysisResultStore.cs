using AudioAnalysis.Core.Models;

namespace AudioAnalysis.Blazor.Services;

public static class AnalysisResultStore
{
    public static AudioAnalysisResult? Current { get; set; }
}
