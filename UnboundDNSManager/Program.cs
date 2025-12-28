using UnboundDNSManager.Components;
using UnboundDNSManager.Models;
using UnboundDNSManager.Services;

if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("This application is only intended to run on Linux.");
    Environment.Exit(1); // Exit with a non-zero exit code
}
else
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Unbound settings from appsettings.json
    builder.Services.Configure<UnboundConnectionOptions>(
        builder.Configuration.GetSection("Unbound"));

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<UnboundHealthCheck>("unbound");

    // Register Unbound services
    builder.Services.AddSingleton<IUnboundService, UnboundService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
    }
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.MapHealthChecks("/health");

    app.Run();
}