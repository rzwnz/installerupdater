using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using InstallerService.Configuration;
using InstallerService.Database;
using InstallerService.Models;
using InstallerService.Services;

namespace InstallerService;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "InstallerUpdater", "logs", "installer-service-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        try
        {
            var migrateOnly = args.Any(a => string.Equals(a, "--migrate", StringComparison.OrdinalIgnoreCase));
            if (migrateOnly)
            {
                Log.Information("Running InstallerService in migration-only mode");
                return await RunMigrationOnlyAsync(args);
            }

            Log.Information("Starting InstallerService host");
            await CreateHostBuilder(args).Build().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "InstallerService terminated unexpectedly");
            return 1;
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
                options.ServiceName = "InstallerService";
            })
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Bind configuration
                services.Configure<InstallerServiceOptions>(
                    context.Configuration.GetSection(InstallerServiceOptions.SectionName));

                // Registry manager: use Windows implementation on Windows, InMemory otherwise
                if (OperatingSystem.IsWindows())
                {
                    services.AddSingleton<IRegistryManager, WindowsRegistryManager>();
                }
                else
                {
                    services.AddSingleton<IRegistryManager, InMemoryRegistryManager>();
                }

                // File system service
                services.AddSingleton<IFileSystemService, FileSystemService>();

                // SQLite migrator for local database
                services.AddSingleton<IDatabaseMigrator>(sp =>
                {
                    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InstallerServiceOptions>>().Value;
                    var provider = options.DatabaseProvider?.Trim().ToLowerInvariant() ?? InstallerServiceOptions.DatabaseProviderSqlite;

                    if (provider == InstallerServiceOptions.DatabaseProviderPostgres)
                    {
                        if (string.IsNullOrWhiteSpace(options.PostgresConnectionString))
                        {
                            throw new InvalidOperationException("PostgreSQL provider selected but PostgresConnectionString is empty.");
                        }

                        var postgresLogger = sp.GetRequiredService<ILogger<PostgresMigrator>>();
                        var postgresMigrationsDir = Path.Combine(options.WorkingDirectory, "migrations", "postgres");
                        return new PostgresMigrator(options.PostgresConnectionString, postgresMigrationsDir, postgresLogger);
                    }

                    var sqliteLogger = sp.GetRequiredService<ILogger<SqliteMigrator>>();
                    var sqliteDbPath = options.LocalDbPath;
                    var sqliteMigrationsDir = Path.Combine(
                        Path.GetDirectoryName(sqliteDbPath) ?? ".",
                        "migrations", "sqlite");
                    return new SqliteMigrator($"Data Source={sqliteDbPath}", sqliteMigrationsDir, sqliteLogger);
                });

                // Tomcat HTTP client
                services.AddHttpClient<ITomcatClient, TomcatClient>();

                // Main worker
                services.AddHostedService<InstallerWorker>();
            });

    private static async Task<int> RunMigrationOnlyAsync(string[] args)
    {
        using var host = CreateHostBuilder(args).Build();
        using var scope = host.Services.CreateScope();

        var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MigrationRunner");

        var applied = await migrator.MigrateAsync(CancellationToken.None);
        var currentVersion = await migrator.GetCurrentVersionAsync(CancellationToken.None);

        logger.LogInformation("Migration-only mode completed. Applied {Count} migration(s). Current version: {Version}",
            applied.Count,
            currentVersion ?? "unknown");

        return 0;
    }
}
