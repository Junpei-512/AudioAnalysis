using AudioAnalysis.Api.Models;
using AudioAnalysis.Api.Services;
using AudioAnalysis.Core.Interfaces;
using AudioAnalysis.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AudioAnalysis.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AudioController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions =
        [".mp3", ".wav", ".flac", ".ogg"];

    private const long MaxFileSizeBytes = 52_428_800; // 50 MB

    private readonly IAudioAnalysisService _service;
    private readonly IAiCommentService _aiComment;
    private readonly ILogger<AudioController> _logger;

    public AudioController(
        IAudioAnalysisService service,
        IAiCommentService aiComment,
        ILogger<AudioController> logger)
    {
        _service = service;
        _aiComment = aiComment;
        _logger = logger;
    }

    /// <summary>AI による調性解説を生成（別途呼び出し可能）</summary>
    [HttpPost("ai-comment")]
    public async Task<IActionResult> GetAiComment(
        [FromBody] AiCommentRequest request,
        CancellationToken ct)
    {
        if (!_aiComment.IsAvailable)
        {
            return Ok(new AiCommentResponse
            {
                Available = false,
                UnavailableReason = "Anthropic API キーが設定されていません。appsettings.json の Anthropic:ApiKey を設定してください。"
            });
        }

        string? comment = await _aiComment.GenerateAsync(
            request.OverallKey, request.SectionKeys, ct);

        return Ok(new AiCommentResponse
        {
            Available = true,
            Comment = comment
        });
    }

    /// <summary>全解析一括エンドポイント</summary>
    [HttpPost("analyze")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Analyze(
        [FromForm] AnalyzeFormRequest form,
        CancellationToken ct = default)
    {
        var validation = ValidateFile(form.File);
        if (validation != null) return validation;

        var request = new AudioAnalysisRequest
        {
            FftSize = form.FftSize,
            WaveformPoints = form.WaveformPoints,
            IncludeAiComment = form.IncludeAiComment
        };

        await using var stream = form.File.OpenReadStream();
        var result = await _service.AnalyzeAsync(stream, form.File.FileName, request, ct);
        return Ok(result);
    }

    /// <summary>BPM検出</summary>
    [HttpPost("bpm")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GetBpm([FromForm] FileOnlyFormRequest form, CancellationToken ct)
    {
        var validation = ValidateFile(form.File);
        if (validation != null) return validation;

        await using var stream = form.File.OpenReadStream();
        var result = await _service.AnalyzeAsync(stream, form.File.FileName, new AudioAnalysisRequest(), ct);
        return Ok(result.Bpm);
    }

    /// <summary>キー推定</summary>
    [HttpPost("key")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GetKey([FromForm] FileOnlyFormRequest form, CancellationToken ct)
    {
        var validation = ValidateFile(form.File);
        if (validation != null) return validation;

        await using var stream = form.File.OpenReadStream();
        var result = await _service.AnalyzeAsync(stream, form.File.FileName, new AudioAnalysisRequest(), ct);
        return Ok(result.Key);
    }

    /// <summary>FFTスペクトル</summary>
    [HttpPost("fft")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GetFft([FromForm] FftFormRequest form, CancellationToken ct = default)
    {
        var validation = ValidateFile(form.File);
        if (validation != null) return validation;

        var request = new AudioAnalysisRequest { FftSize = form.FftSize };
        await using var stream = form.File.OpenReadStream();
        var result = await _service.AnalyzeAsync(stream, form.File.FileName, request, ct);
        return Ok(result.Fft);
    }

    /// <summary>波形データ</summary>
    [HttpPost("waveform")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GetWaveform([FromForm] WaveformFormRequest form, CancellationToken ct = default)
    {
        var validation = ValidateFile(form.File);
        if (validation != null) return validation;

        var request = new AudioAnalysisRequest { WaveformPoints = form.WaveformPoints };
        await using var stream = form.File.OpenReadStream();
        var result = await _service.AnalyzeAsync(stream, form.File.FileName, request, ct);
        return Ok(result.Waveform);
    }

    /// <summary>倍音解析</summary>
    [HttpPost("harmonics")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GetHarmonics([FromForm] FileOnlyFormRequest form, CancellationToken ct)
    {
        var validation = ValidateFile(form.File);
        if (validation != null) return validation;

        await using var stream = form.File.OpenReadStream();
        var result = await _service.AnalyzeAsync(stream, form.File.FileName, new AudioAnalysisRequest(), ct);
        return Ok(result.Harmonics);
    }

    /// <summary>曲全体の調性解析</summary>
    [HttpPost("tonality")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GetTonality([FromForm] TonalityFormRequest form, CancellationToken ct = default)
    {
        var validation = ValidateFile(form.File);
        if (validation != null) return validation;

        var request = new AudioAnalysisRequest { IncludeAiComment = form.IncludeAiComment };
        await using var stream = form.File.OpenReadStream();
        var result = await _service.AnalyzeAsync(stream, form.File.FileName, request, ct);
        return Ok(result.Tonality);
    }

    private IActionResult? ValidateFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ErrorResponse { ErrorCode = "NO_FILE", Message = "ファイルが指定されていません" });

        if (file.Length > MaxFileSizeBytes)
            return StatusCode(413, new ErrorResponse { ErrorCode = "FILE_TOO_LARGE", Message = "ファイルサイズは50MB以下にしてください" });

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "UNSUPPORTED_FORMAT",
                Message = $"対応フォーマット: {string.Join(", ", AllowedExtensions)}"
            });

        return null;
    }
}
