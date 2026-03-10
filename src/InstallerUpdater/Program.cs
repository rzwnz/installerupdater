using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using InstallerUpdater.Models;
using InstallerUpdater.Services;

namespace InstallerUpdater;

public static class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "InstallerUpdater", "logs", "updater-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        try
        {
            Log.Information("Starting InstallerUpdater host");
            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "InstallerUpdater terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "InstallerUpdater";
            })
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.Configure<UpdaterOptions>(
                    context.Configuration.GetSection(UpdaterOptions.SectionName));

                services.AddHttpClient<IUpdateServerClient, UpdateServerClient>();
                services.AddSingleton<IInstalledVersionProvider, RegistryInstalledVersionProvider>();
                services.AddSingleton<IVersionComparer, VersionComparer>();
                services.AddSingleton<IUpdateApplier, UpdateApplier>();
                services.AddHostedService<UpdateWorker>();
            });
}
