using IscrizioneManager.Core.Services;
using IscrizioneManager.Web;
using IscrizioneManager.Web.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<Microsoft.AspNetCore.Components.Web.HeadOutlet>("head::after");

builder.Services.AddMudServices(config =>
{
  config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
  config.SnackbarConfiguration.ShowCloseIcon = true;
  config.SnackbarConfiguration.VisibleStateDuration = 4000;
});

builder.Services.AddSingleton<LocalStorageService>();
builder.Services.AddSingleton<AppState>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ModuloIscrizioneViewModel>();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var host = builder.Build();

await host.Services.GetRequiredService<AuthService>().TryRestoreSessionAsync();

await host.RunAsync();
