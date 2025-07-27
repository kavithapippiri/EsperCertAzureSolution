using ManualDesktopForm.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace ManualDesktopForm.Services
{
    public class DeviceService
    {
        private readonly IMongoCollection<Device> _deviceCollection;

        public DeviceService()
        {
            var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

            var mongoSettings = config.GetSection("MongoSettings").Get<MongoDBSettings>()
                ?? throw new InvalidOperationException("MongoSettings section missing in appsettings.json");

            var client = new MongoClient(mongoSettings.ConnectionString);
            var database = client.GetDatabase(mongoSettings.DatabaseName);
            _deviceCollection = database.GetCollection<Device>(mongoSettings.CollectionName);
        }
        public async Task AddDeviceAsync(Device device)
        {
            await _deviceCollection.InsertOneAsync(device);
        }

        public async Task UpdateDeviceAsync(Device device)
        {
            var filter = Builders<Device>.Filter.Eq(d => d.DeviceId, device.DeviceId);
            // You might want to update specific fields, not replace the whole document
            var update = Builders<Device>.Update
                .Set(d => d.DeviceName, device.DeviceName)
                .Set(d => d.SerialNumber, device.SerialNumber)
                .Set(d => d.DeviceModel, device.DeviceModel)
                .Set(d => d.Status, device.Status)
                .Set(d => d.UpdatedOn, DateTime.UtcNow); // Update timestamp
                                                         // Add other fields you want to sync from Esper

            await _deviceCollection.UpdateOneAsync(filter, update);
           // _logger.LogInformation($"Device {device.DeviceId} updated in MongoDB.");
        }
        public async Task<List<Device>> GetDevicesAsync()
        {
            // Uncomment this block to use DUMMY DEVICES
            
          /*  return new List<Device>
            {
                new Device { DeviceId = "1", DeviceName = "Device A", SerialNumber = "SN123", Status = "Pending", CertificatePath = "" },
                new Device { DeviceId = "2", DeviceName = "Device B", SerialNumber = "SN456", Status = "Issued", CertificatePath = "certs/deviceb.pfx" },
            }; */
            

            // Comment this block if using dummy data
            return await _deviceCollection.Find(_ => true).ToListAsync();
        }
    }
}
