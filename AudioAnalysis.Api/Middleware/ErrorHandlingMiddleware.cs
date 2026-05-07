using System.Text.Json;
using AudioAnalysis.Core.Models;

namespace AudioAnalysis.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = 408;
            await WriteErrorAsync(context, "TIMEOUT", "リクエストがタイムアウトしました");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Unsupported format"))
        {
            context.Response.StatusCode = 400;
            await WriteErrorAsync(context, "UNSUPPORTED_FORMAT", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 422;

            // 開発環境では実際の例外メッセージを返す
            var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
            string detail = env.IsDevelopment()
                ? $"{ex.GetType().Name}: {ex.Message}"
                : "解析中にエラーが発生しました";

            await WriteErrorAsync(context, "ANALYSIS_FAILED", detail);
        }
    }

    private static Task WriteErrorAsync(HttpContext context, string code, string message)
    {
        context.Response.ContentType = "application/json";
        var error = new ErrorResponse { ErrorCode = code, Message = message };
        return context.Response.WriteAsync(JsonSerializer.Serialize(error));
    }
}
