using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
await builder.Build().RunAsync();
