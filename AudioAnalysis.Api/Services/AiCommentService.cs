using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

namespace AudioAnalysis.Api.Services;

public interface IAiCommentService
{
    bool IsAvailable { get; }
    Task<string?> GenerateAsync(string overallKey, IReadOnlyList<string> sectionKeys, CancellationToken ct = default);
}

public class AiCommentService : IAiCommentService
{
    private readonly AnthropicClient? _claude;
    private readonly ILogger<AiCommentService> _logger;

    public bool IsAvailable => _claude != null;

    public AiCommentService(IConfiguration config, ILogger<AiCommentService> logger)
    {
        _logger = logger;
        string? key = config["Anthropic:ApiKey"];
        if (!string.IsNullOrEmpty(key))
            _claude = new AnthropicClient(key);
    }

    public async Task<string?> GenerateAsync(
        string overallKey,
        IReadOnlyList<string> sectionKeys,
        CancellationToken ct = default)
    {
        if (_claude == null) return null;

        try
        {
            string progression = string.Join("→", sectionKeys.Distinct());
            string prompt =
                $"この曲の全体的なキーは「{overallKey}」で、" +
                $"セクション別には {progression} という調性の変化をたどります。" +
                "音楽理論的な観点から2〜3文で解説してください。日本語で回答してください。";

            var req = new MessageParameters
            {
                Model = AnthropicModels.Claude45Haiku,
                MaxTokens = 400,
                Messages =
                [
                    new Message
                    {
                        Role = RoleType.User,
                        Content = [new TextContent { Text = prompt }]
                    }
                ]
            };

            var res = await _claude.Messages.GetClaudeMessageAsync(req, ct);
            return res.Content.OfType<TextContent>().FirstOrDefault()?.Text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude API call failed");
            return null;
        }
    }
}
