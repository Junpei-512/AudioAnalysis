namespace AudioAnalysis.Core.Models;

public class AiCommentRequest
{
    public string OverallKey { get; set; } = string.Empty;
    public List<string> SectionKeys { get; set; } = [];
}

public class AiCommentResponse
{
    public bool Available { get; set; }
    public string? Comment { get; set; }
    public string? UnavailableReason { get; set; }
}

public class TonalityFeedback
{
    public string DetectedKey { get; set; } = string.Empty;
    public string CorrectedKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
