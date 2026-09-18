using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using KineMotion.Web;
using KineMotion.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("Missing ApiBaseUrl configuration in wwwroot/appsettings*.json.");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<KineMotionApiClient>();
builder.Services.AddScoped<RehabHubClient>();
builder.Services.AddScoped<PoseGameInterop>();

await builder.Build().RunAsync();
