using BlazorApp1.Client.Pages;
using BlazorApp1.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
var app = builder.Build();
app.UseRequestLocalization(options =>
{
    var cultures = new[] { "zh-CN", "en-US", "zh-TW" };
    options.AddSupportedCultures(cultures);
    options.AddSupportedUICultures(cultures);
    options.SetDefaultCulture(cultures[0]);

    // 当Http响应时，将 当前区域信息 设置到 Response Header：Content-Language 中
    options.ApplyCurrentCultureToResponseHeaders = true;
});
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BlazorApp1.Client._Imports).Assembly);

app.Run();
