using System.Net.Http.Headers;
using AudioAnalysis.Core.Models;
using System.Text.Json;

namespace AudioAnalysis.Blazor.Services;

public class AudioApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AudioApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<AudioAnalysisResult> AnalyzeAsync(
        Stream fileStream,
        string fileName,
        int fftSize = 4096,
        int waveformPoints = 1000,
        bool includeAiComment = false,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileName));
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(fftSize.ToString()), "fftSize");
        content.Add(new StringContent(waveformPoints.ToString()), "waveformPoints");
        content.Add(new StringContent(includeAiComment.ToString().ToLower()), "includeAiComment");

        var response = await _http.PostAsync("api/audio/analyze", content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<AudioAnalysisResult>(json, JsonOpts)
               ?? throw new InvalidOperationException("Empty response from API");
    }

    private static string GetMimeType(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };
    }
}
