using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using VitalFace.Core.Abstractions;
using VitalFace.Core.Services;
using VitalFace.Web;
using VitalFace.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
var httpBaseAddress = string.IsNullOrWhiteSpace(apiBaseUrl) ? builder.HostEnvironment.BaseAddress : apiBaseUrl;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(httpBaseAddress) });

// Same CHROM/PERCLOS estimators used server-side, compiled to WASM here for the live in-kiosk
// preview during capture — the authoritative, stored result still comes from VitalFace.Api.
builder.Services.AddSingleton<IRppgProcessor, RppgProcessor>();
builder.Services.AddSingleton<IFatigueDetector, FatigueDetector>();
builder.Services.AddSingleton<IRespiratoryRateEstimator, RespiratoryRateEstimator>();

builder.Services.AddScoped<VitalsApiClient>();
builder.Services.AddScoped<KioskInterop>();

await builder.Build().RunAsync();
