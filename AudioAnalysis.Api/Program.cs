using System.Text;
using System.Threading.RateLimiting;
using AudioAnalysis.Api.Middleware;
using AudioAnalysis.Api.Services;
using AudioAnalysis.Core.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AudioAnalysis API",
        Version = "v1",
        Description = "音楽ファイル解析 REST API (BPM・キー・FFT・波形・倍音・調性)"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// JWT (optional in development — auth is not enforced on routes by default)
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "AudioAnalysisDevelopmentKey_ChangeInProduction_32chars!!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // 開発環境: localhost の全ポートを許可
            policy
                .SetIsOriginAllowed(origin =>
                {
                    var uri = new Uri(origin);
                    return uri.Host == "localhost" || uri.Host == "127.0.0.1";
                })
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            string[] origins = (builder.Configuration["AllowedOrigins"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("perIp", limiterOpts =>
    {
        limiterOpts.PermitLimit = 10;
        limiterOpts.Window = TimeSpan.FromMinutes(1);
        limiterOpts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOpts.QueueLimit = 2;
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// DI
builder.Services.AddScoped<IBpmDetector, BpmDetector>();
builder.Services.AddScoped<IKeyEstimator, KeyEstimator>();
builder.Services.AddScoped<IFftAnalyzer, FftAnalyzer>();
builder.Services.AddScoped<IWaveformExtractor, WaveformExtractor>();
builder.Services.AddScoped<IHarmonicsAnalyzer, HarmonicsAnalyzer>();
builder.Services.AddScoped<ITonalityAnalyzer, TonalityAnalyzer>();
builder.Services.AddScoped<IAudioAnalysisService, AudioAnalysisService>();

builder.WebHost.ConfigureKestrel(opts =>
{
    opts.Limits.MaxRequestBodySize = 52_428_800;
});

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AudioAnalysis API v1"));
}

// HTTPS リダイレクトは本番のみ（開発中は HTTP で Blazor と通信するため無効化）
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("perIp");

app.Run();

public partial class Program { }
