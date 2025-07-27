// EsperCertProject/Services/MongoDbService.cs
using EsperCertProject.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic; // Added for List<Device>
using System.Threading.Tasks;

namespace EsperCertProject.Services
{
    public class MongoDbService : IMongoDbService
    {
        private readonly IMongoCollection<Device> _devicesCollection;
        private readonly ILogger<MongoDbService> _logger;

        public MongoDbService(
            IOptions<MongoDbSettings> mongoDbSettings,
            ILogger<MongoDbService> logger)
        {
            _logger = logger;
            var settings = mongoDbSettings.Value;

            if (string.IsNullOrEmpty(settings.ConnectionString) || string.IsNullOrEmpty(settings.DatabaseName) || string.IsNullOrEmpty(settings.CollectionName))
            {
                _logger.LogError("MongoDB settings are not fully configured. ConnectionString, DatabaseName, and CollectionName are required.");
                throw new ArgumentNullException("MongoDB settings are incomplete.");
            }

            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);
            _devicesCollection = database.GetCollection<Device>(settings.CollectionName);
            _logger.LogInformation("MongoDB Service initialized for database '{DatabaseName}' and collection '{CollectionName}'.", settings.DatabaseName, settings.CollectionName);
        }

        public async Task<Device?> GetDeviceByIdAsync(string esperDeviceId)
        {
            _logger.LogInformation("Querying MongoDB for device with Esper ID: {EsperDeviceId}", esperDeviceId);
            var filter = Builders<Device>.Filter.Eq(d => d.DeviceId, esperDeviceId);
            _logger.LogInformation($"[MongoDB] Generated filter definition: {filter.ToString()}");

            var device = await _devicesCollection.Find(filter).FirstOrDefaultAsync();
            if (device != null)
            {
                _logger.LogInformation("Found device with Esper ID: {EsperDeviceId}", esperDeviceId);
            }
            else
            {
                _logger.LogInformation("No device found with Esper ID: {EsperDeviceId}", esperDeviceId);
            }
            return device;
        }

        public async Task UpdateDeviceCertificateAsync(string esperDeviceId, CertificateStoreInfo certInfo)
        {
            _logger.LogInformation("Updating embedded certificate info for device with Esper ID: {EsperDeviceId}", esperDeviceId);
            var filter = Builders<Device>.Filter.Eq(d => d.DeviceId, esperDeviceId);
            var update = Builders<Device>.Update.Set(d => d.Certificate, certInfo); // Set the entire Certificate sub-document

            var result = await _devicesCollection.UpdateOneAsync(filter, update);

            if (result.ModifiedCount == 1)
            {
                _logger.LogInformation("Successfully updated embedded certificate info for device: {EsperDeviceId}.", esperDeviceId);
            }
            else if (result.MatchedCount == 0)
            {
                _logger.LogWarning("No device found with Esper ID '{EsperDeviceId}' to update certificate. Ensure device is enrolled first.", esperDeviceId);
            }
            else
            {
                _logger.LogInformation("Certificate info for device '{EsperDeviceId}' was already up to date (no modifications needed).", esperDeviceId);
            }
        }

        // NEW: Implementation for getting expiring certificates
        public async Task<List<Device>> GetExpiringCertificatesAsync(int daysBeforeExpiry)
        {
            _logger.LogInformation("Getting devices with certificates expiring in the next {Days} days.", daysBeforeExpiry);
            var cutoff = DateTime.UtcNow.AddDays(daysBeforeExpiry);

            // Filter for documents where 'Certificate' exists and its 'ExpiryDate' is less than the cutoff.
            // Using Builders.Filter.Exists ensures 'Certificate' field is present.
            var filter = Builders<Device>.Filter.And(
                Builders<Device>.Filter.Exists(d => d.Certificate), // Ensure the Certificate sub-document exists
                Builders<Device>.Filter.Where(d => d.Certificate != null && d.Certificate.ExpiryDate < cutoff)
            );

            var expiringDevices = await _devicesCollection.Find(filter).ToListAsync();
            _logger.LogInformation("Found {Count} devices with expiring certificates.", expiringDevices.Count);
            return expiringDevices;
        }

        public async Task UpdateDeviceAsync(Device device)
        {
            _logger.LogInformation($"Updating device with ID: {device.DeviceId}");
            // Replace the existing document with the updated device object
            await _devicesCollection.ReplaceOneAsync(d => d.DeviceId == device.DeviceId, device);
            _logger.LogInformation($"Device {device.DeviceId} updated successfully.");
        }

        // Optional: If you need a method to fully replace a device document.
        // Use with caution, as it replaces the entire document.
        /*
        public async Task ReplaceDeviceAsync(string deviceId, Device updatedDevice)
        {
            _logger.LogInformation("Replacing full device document for device: {DeviceId}.", deviceId);
            var filter = Builders<Device>.Filter.Eq(d => d.DeviceId, deviceId);
            var result = await _devicesCollection.ReplaceOneAsync(filter, updatedDevice);

            if (result.ModifiedCount == 1)
            {
                _logger.LogInformation("Successfully replaced device document for: {DeviceId}.", deviceId);
            }
            else if (result.MatchedCount == 0)
            {
                _logger.LogWarning("No device found with Esper ID '{DeviceId}' to replace.", deviceId);
            }
        }
        */
    }
}