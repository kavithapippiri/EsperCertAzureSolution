// ManualDesktopForm.Services/EsperDeviceSyncService.cs
using ManualDesktopForm.Models; // Ensure EsperSettings, MongoDbSettings, and Device are defined here
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ManualDesktopForm.Services
{
    public class EsperDeviceSyncService
    {
        private readonly HttpClient _httpClient;
        private readonly IMongoCollection<Device> _devicesCollection; // Use your strongly-typed Device model
        private readonly EsperSettings _esperSettings;
        private readonly ILogger<EsperDeviceSyncService> _logger;

        public EsperDeviceSyncService(
            HttpClient httpClient,
            IOptions<EsperSettings> esperSettings,
            IOptions<MongoDBSettings> mongoDbSettings,
            ILogger<EsperDeviceSyncService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _esperSettings = esperSettings.Value; // Get the raw EsperSettings object

            // Configure HttpClient with Esper API details
            _httpClient.BaseAddress = new Uri(_esperSettings.BaseUrl ?? throw new ArgumentNullException("Esper:BaseUrl not found in configuration."));
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _esperSettings.ApiKey); // Use BearerToken as per Program.cs
            _logger.LogInformation("EsperDeviceSyncService: HttpClient configured with Base URL: {EsperBaseUrl}", _esperSettings.BaseUrl);

            // Configure MongoDB
            var mongoConfig = mongoDbSettings.Value;
            if (string.IsNullOrEmpty(mongoConfig.ConnectionString) || string.IsNullOrEmpty(mongoConfig.DatabaseName) || string.IsNullOrEmpty(mongoConfig.CollectionName))
            {
                _logger.LogError("MongoDB settings are not fully configured. ConnectionString, DatabaseName, and DevicesCollectionName are required.");
                throw new ArgumentNullException("MongoDB settings are incomplete for EsperDeviceSyncService.");
            }
            var client = new MongoClient(mongoConfig.ConnectionString);
            var db = client.GetDatabase(mongoConfig.DatabaseName);
            // Use your strongly-typed Device model for the collection
            _devicesCollection = db.GetCollection<Device>(mongoConfig.CollectionName);
            _logger.LogInformation("EsperDeviceSyncService: MongoDB Database: '{DatabaseName}', Collection: '{CollectionName}'", mongoConfig.DatabaseName, mongoConfig.CollectionName);
        }

        public async Task<int> SyncDevicesFromEsperAsync()
        {
            _logger.LogInformation("Fetching device list from Esper API...");

            var url = $"enterprise/{_esperSettings.EnterpriseId}/device/"; // Use EnterpriseId from settings

            using var resp = await _httpClient.GetAsync(url);

            if (!resp.IsSuccessStatusCode)
            {
                var errorContent = await resp.Content.ReadAsStringAsync();
                _logger.LogError("Esper API error fetching devices: {StatusCode} - {ErrorContent}", resp.StatusCode, errorContent);
                throw new HttpRequestException($"Esper API request failed: {resp.StatusCode} - {errorContent}");
            }

            var json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<DeviceListResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (list?.Results == null || list.Results.Count == 0)
            {
                _logger.LogInformation("No devices returned from Esper API.");
                return 0;
            }

            var devicesProcessed = 0;
            foreach (var d in list.Results)
            {
                // Map DeviceDto to your internal Device model
                var device = new Device
                {
                    // Assuming your Device model has these properties
                    DeviceId = d.Id, // Esper's 'id' is our DeviceId
                    DeviceName = d.DeviceName,
                    DeviceModel = new DeviceModelInfo { Name = d.DeviceModel }, // wrap string in DeviceModelInfo
                    Status = d.Status,
                    SerialNumber = d.HardwareInfo?.SerialNumber,
                    EnrolledOn = d.CreatedOn, // Esper's created_on
                    UpdatedOn = d.UpdatedOn, // Esper's updated_on
                    // Initialize other properties that ManualDesktopForm uses, if they aren't set by upsert.
                    // For example, if ProcessingStatus is custom to your app, initialize it for new devices.
                    ProcessingStatus = "Not Processed", // Default status for newly synced devices
                    LastProcessingMessage = "Synchronized from Esper."
                };

                // Use ReplaceOneAsync for upserting with strongly-typed model
                var filter = Builders<Device>.Filter.Eq(x => x.DeviceId, device.DeviceId);
                var options = new ReplaceOptions { IsUpsert = true };

                try
                {
                    var result = await _devicesCollection.ReplaceOneAsync(filter, device, options);
                    if (result.IsAcknowledged)
                    {
                        devicesProcessed++;
                        _logger.LogDebug("Device {DeviceId} upserted to MongoDB. Matched: {Matched}, Modified: {Modified}, UpsertedId: {UpsertedId}",
                            device.DeviceId, result.MatchedCount, result.ModifiedCount, result.UpsertedId);
                    }
                    else
                    {
                        _logger.LogWarning("Device {DeviceId} upsert failed acknowledgment.", device.DeviceId);
                    }
                }
                catch (MongoWriteException writeEx)
                {
                    _logger.LogError(writeEx, "Error upserting device {DeviceId} to MongoDB: {Message}", device.DeviceId, writeEx.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing device {DeviceId}: {Message}", device.DeviceId, ex.Message);
                }
            }

            _logger.LogInformation("{Count} devices processed and stored in MongoDB.", devicesProcessed);
            return devicesProcessed;
        }

        // Keep the private DTOs from your EnrollDeviceFunction here
        private class DeviceListResponse
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }
            [JsonPropertyName("next")]
            public string? Next { get; set; }
            [JsonPropertyName("previous")]
            public string? Previous { get; set; }
            [JsonPropertyName("results")]
            public List<DeviceDto> Results { get; set; } = new List<DeviceDto>();
        }

        private class DeviceDto
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            [JsonPropertyName("device_name")]
            public string? DeviceName { get; set; }
            [JsonPropertyName("device_model")]
            public string? DeviceModel { get; set; }
            [JsonPropertyName("status")]
            public int Status { get; set; }
            [JsonPropertyName("hardwareInfo")]
            public HardwareInfoDto? HardwareInfo { get; set; }
            [JsonPropertyName("created_on")]
            public DateTime CreatedOn { get; set; }
            [JsonPropertyName("updated_on")]
            public DateTime UpdatedOn { get; set; }
        }

        private class HardwareInfoDto
        {
            [JsonPropertyName("serialNumber")]
            public string? SerialNumber { get; set; }
        }
    }
}