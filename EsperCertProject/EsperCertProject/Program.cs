// ============================================================================
// Updated Program.cs - Azure + Local fallback logic with Serilog
// ============================================================================
using EsperCertProject.Models;
using EsperCertProject.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;
using System;

var builder = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        IConfiguration configuration = hostContext.Configuration;
        var azureWebJobsStorage = configuration["AzureWebJobsStorage"];
        if (!string.IsNullOrWhiteSpace(azureWebJobsStorage))
        {
            var visiblePart = azureWebJobsStorage.Length > 50
                ? azureWebJobsStorage.Substring(0, 50) + "..."
                : azureWebJobsStorage;
            Log.Information($"AzureWebJobsStorage is configured: {visiblePart}");
        }
        else
        {
            Log.Information("AzureWebJobsStorage is NOT set!");
        }
        // Fallback configuration from local.settings.json
        var localConfig = new ConfigurationBuilder()
            .AddJsonFile("local.settings.json", optional: true)
            .Build();

        string GetConfigValue(string key)
        {
            return configuration[key] ?? localConfig[$"Values:{key}"];
        }

        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Strongly-typed config bindings
        services.Configure<EsperSettings>(configuration.GetSection("Esper"));
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDb"));
        services.Configure<CertificateOptions>(configuration.GetSection("Certificate"));
        services.Configure<CertSrvSettings>(configuration.GetSection("CertSrv"));

        // MongoDB Connection
        services.AddSingleton<IMongoClient>(sp =>
        {
            var connectionString = GetConfigValue("MongoDb__ConnectionString");
            Log.Information("MongoDb__ConnectionString resolved to: {Conn}", connectionString ?? "<null>");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("MongoDB connection string missing in both Azure and local.settings.json.");

            return new MongoClient(connectionString);
        });

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var databaseName = GetConfigValue("MongoDb__DatabaseName");
            Log.Information("MongoDb__DatabaseName resolved to: {Db}", databaseName ?? "<null>");

            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("MongoDB database name missing in both Azure and local.settings.json.");

            return sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName);
        });

        services.AddSingleton<IMongoCollection<Device>>(sp =>
        {
            var collectionName = GetConfigValue("MongoDb__CollectionName");
            Log.Information("MongoDb__CollectionName resolved to: {Collection}", collectionName ?? "<null>");

            if (string.IsNullOrWhiteSpace(collectionName))
                throw new InvalidOperationException("MongoDB collection name missing in both Azure and local.settings.json.");

            return sp.GetRequiredService<IMongoDatabase>().GetCollection<Device>(collectionName);
        });

        services.AddSingleton<IMongoCollection<BsonDocument>>(sp =>
        {
            var databaseName = GetConfigValue("MongoDb__DatabaseName");
            var collectionName = GetConfigValue("MongoDb__CollectionName");

            if (string.IsNullOrWhiteSpace(databaseName) || string.IsNullOrWhiteSpace(collectionName))
                throw new InvalidOperationException("MongoDb__DatabaseName or CollectionName missing in both Azure and local.settings.json.");

            return sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName).GetCollection<BsonDocument>(collectionName);
        });

        services.AddSingleton<IMongoDbService, MongoDbService>();

        // HttpClient and Certificate-related services
        services.AddHttpClient<EsperContentService>();
        services.AddScoped<ICertSrvService, CertSrvService>();
        services.AddTransient<ICertificateProcessor, CertificateProcessor>();
        services.AddSingleton(sp => new CertificateService(sp.GetRequiredService<ILogger<CertificateService>>()));

        // IConfiguration injection
        services.AddSingleton<IConfiguration>(configuration);

        // Serilog Logging
        services.AddLogging(logging =>
        {
              logging.AddSerilog();
        });
    });

// ==== SERILOG SETUP ====
var configurationRoot = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddJsonFile("local.settings.json", optional: true)
    .Build();

string logPath = configurationRoot["Serilog__FilePath"] ?? "D:\\home\\LogFiles\\EsperCertProject.log";

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configurationRoot)
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting EsperCertProject host...");
    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
