using AudioAnalysis.Blazor.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<AudioAnalysis.Blazor.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API base URL — override in appsettings or environment
string apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5001";

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<AudioApiClient>();

await builder.Build().RunAsync();
