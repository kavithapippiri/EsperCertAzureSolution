// Program.cs
using ManualDesktopForm.Models;
using ManualDesktopForm.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; // Required for IOptions
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using MongoDB.Driver; // Add this for MongoDB types

namespace ManualDesktopForm
{
    internal static class Program
    {
        private static ILogger _logger;

        [STAThread]
        static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            _logger = host.Services.GetRequiredService<ILogger<Object>>();

            var autoModeArgs = host.Services.GetRequiredService<AutoModeArgs>();

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();

            using (var serviceScope = host.Services.CreateScope())
            {
                var services = serviceScope.ServiceProvider;
                try
                {
                    var form1 = services.GetRequiredService<Form1>();

                    if (autoModeArgs.Auto)
                    {
                        form1.Load += (s, e) =>
                        {
                            form1.Hide();
                            Task.Run(() => form1.RunAutoFlowAsync()).ContinueWith(t =>
                            {
                                if (t.Exception != null)
                                {
                                    _logger.LogError(t.Exception, "Error during auto flow execution.");
                                }
                                // You might want to shut down the application after auto flow completes or fails
                                Application.Exit();
                            });
                        };
                    }

                    Application.Run(form1);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred at application startup.");
                    MessageBox.Show($"Application Startup Error: {ex.Message}\nCheck logs for details.", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Load configuration from appsettings.json
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Configuration bindings
                    services.Configure<EsperSettings>(hostContext.Configuration.GetSection("EsperSettings"));
                    services.AddSingleton(resolver => resolver.GetRequiredService<IOptions<EsperSettings>>().Value);

                    // Add MongoDbSettings binding - CORRECTED: Use "MongoSettings" section name to match appsettings.json
                    services.Configure<MongoDBSettings>(hostContext.Configuration.GetSection("MongoSettings")); // Assuming MongoDbSettings is the model class name

                    // MongoDB Services: Full setup for IMongoClient, IMongoDatabase, and IMongoCollection<Device>
                    services.AddSingleton<IMongoClient>(serviceProvider =>
                    {
                        var mongoDbSection = hostContext.Configuration.GetSection("MongoSettings"); // Use "MongoSettings"
                        var settings = mongoDbSection.Get<MongoDBSettings>(); // Assuming MongoDbSettings
                        if (settings == null || string.IsNullOrEmpty(settings.ConnectionString))
                        {
                            throw new InvalidOperationException("MongoDB ConnectionString is missing or invalid in appsettings.json under 'MongoSettings' section.");
                        }
                        return new MongoClient(settings.ConnectionString);
                    });

                    services.AddSingleton<IMongoDatabase>(serviceProvider =>
                    {
                        var mongoDbSection = hostContext.Configuration.GetSection("MongoSettings"); // Use "MongoSettings"
                        var settings = mongoDbSection.Get<MongoDBSettings>(); // Assuming MongoDbSettings
                        var client = serviceProvider.GetRequiredService<IMongoClient>();
                        if (settings == null || string.IsNullOrEmpty(settings.DatabaseName))
                        {
                            throw new InvalidOperationException("MongoDB DatabaseName is not configured in appsettings.json under 'MongoSettings' section.");
                        }
                        return client.GetDatabase(settings.DatabaseName);
                    });

                    services.AddSingleton<IMongoCollection<Device>>(serviceProvider =>
                    {
                        var mongoDbSection = hostContext.Configuration.GetSection("MongoSettings"); // Use "MongoSettings"
                        var settings = mongoDbSection.Get<MongoDBSettings>(); // Assuming MongoDbSettings
                        var database = serviceProvider.GetRequiredService<IMongoDatabase>();
                        if (settings == null || string.IsNullOrEmpty(settings.CollectionName))
                        {
                            throw new InvalidOperationException("MongoDB CollectionName is not configured for Devices in appsettings.json under 'MongoSettings' section.");
                        }
                        return database.GetCollection<Device>(settings.CollectionName);
                    });

                    // Application Services
                    services.AddSingleton<IMongoDbService, MongoDbService>();
                    services.AddSingleton<DeviceService>();
                    services.AddSingleton<QueueService>();

                    // Esper API Services: Use AddHttpClient for services that depend on HttpClient
                    services.AddHttpClient<EsperDeviceApiService>();
                    services.AddSingleton<EsperDeviceSyncService>();

                    // Certificate Services:
                    services.AddHttpClient<EsperContentService>();

                    services.AddSingleton<CertificateService>(serviceProvider =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<CertificateService>>();
                        return new CertificateService(logger);
                    });

                    services.AddHttpClient<ICertSrvService, CertSrvService>();

                    // Certificate Processor: Changed to Transient as it's a processor (no long-lived state)
                    services.AddTransient<ICertificateProcessor, CertificateProcessor>();

                    // Register AutoModeArgs
                    services.AddSingleton(new AutoModeArgs(args));

                    // Register the main form
                    services.AddTransient<Form1>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    logging.AddConsole();
                    logging.AddDebug();
                    // Logging filters (adjust "ManualDesktopForm" namespace if your root namespace is different)
                    logging.AddFilter("Microsoft", LogLevel.Warning);
                    logging.AddFilter("System", LogLevel.Warning);
                    logging.AddFilter("ManualDesktopForm", LogLevel.Information);
                    logging.AddFilter("MongoDB", LogLevel.Warning);
                });
    }
}