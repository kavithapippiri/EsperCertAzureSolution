// EsperCertProject/Services/IMongoDbService.cs
using ManualDesktopForm.Models;
using MongoDB.Driver;
using System.Collections.Generic; // Added for List<Device>
using System.Threading.Tasks;

namespace ManualDesktopForm.Services
{
    public interface IMongoDbService
    {
        IMongoCollection<T> GetCollection<T>(string name);

        Task<Device?> GetDeviceByIdAsync(string esperDeviceId);
        Task UpdateDeviceCertificateAsync(string esperDeviceId, CertificateStoreInfo certInfo);

        // NEW: Add the GetExpiringCertificatesAsync signature
        Task<List<Device>> GetExpiringCertificatesAsync(int daysBeforeExpiry);

        // Optional: If you need a method to fully replace a device document
        // Task ReplaceDeviceAsync(string deviceId, Device updatedDevice);
        Task UpdateDeviceAsync(Device device);
    }
}