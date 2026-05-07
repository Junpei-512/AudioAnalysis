using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using AudioAnalysis.Core.Interfaces;
using AudioAnalysis.Core.Models;

namespace AudioAnalysis.Api.Services;

public class TonalityAnalyzer : ITonalityAnalyzer
{
    private const int SectionDurationSec = 30;
    private readonly IKeyEstimator _keyEstimator;
    private readonly AnthropicClient? _claude;
    private readonly ILogger<TonalityAnalyzer> _logger;

    public TonalityAnalyzer(
        IKeyEstimator keyEstimator,
        ILogger<TonalityAnalyzer> logger,
        IConfiguration config)
    {
        _keyEstimator = keyEstimator;
        _logger = logger;

        string? apiKey = config["Anthropic:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
            _claude = new AnthropicClient(apiKey);
    }

    public async Task<TonalityResult> AnalyzeAsync(
        float[] samples,
        int sampleRate,
        bool includeAiComment,
        CancellationToken ct = default)
    {
        int samplesPerSection = sampleRate * SectionDurationSec;
        int sectionCount = Math.Max(1, (int)Math.Ceiling((double)samples.Length / samplesPerSection));

        var sections = new List<TonalSection>();
        var sectionKeys = new List<string>();

        for (int i = 0; i < sectionCount; i++)
        {
            int start = i * samplesPerSection;
            int length = Math.Min(samplesPerSection, samples.Length - start);
            var sectionSamples = samples.AsSpan(start, length).ToArray();

            var key = await _keyEstimator.EstimateAsync(sectionSamples, sampleRate, ct);
            sections.Add(new TonalSection
            {
                StartSec = i * SectionDurationSec,
                EndSec = Math.Min((i + 1) * SectionDurationSec, samples.Length / (double)sampleRate),
                Key = key.Display
            });
            sectionKeys.Add(key.Display);
        }

        string overallKey = sectionKeys
            .GroupBy(k => k)
            .OrderByDescending(g => g.Count())
            .First().Key;

        string? aiComment = null;
        if (includeAiComment)
            aiComment = await GetAiCommentAsync(sectionKeys, ct);

        return new TonalityResult
        {
            OverallKey = overallKey,
            Sections = sections,
            AiComment = aiComment
        };
    }

    private async Task<string?> GetAiCommentAsync(List<string> sectionKeys, CancellationToken ct)
    {
        if (_claude == null)
            return null;

        try
        {
            string keyProgression = string.Join("→", sectionKeys.Distinct());
            string prompt = $"この曲は {keyProgression} という調性の変化をたどります。音楽理論的な観点から2〜3文で解説してください。";

            var message = new MessageParameters
            {
                Model = AnthropicModels.Claude45Haiku,
                MaxTokens = 300,
                Messages =
                [
                    new Message
                    {
                        Role = RoleType.User,
                        Content = [new TextContent { Text = prompt }]
                    }
                ]
            };

            var response = await _claude.Messages.GetClaudeMessageAsync(message, ct);
            return response.Content.OfType<TextContent>().FirstOrDefault()?.Text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude API call failed; skipping AI comment");
            return null;
        }
    }
}
