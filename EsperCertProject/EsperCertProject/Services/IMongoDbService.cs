// EsperCertProject/Services/IMongoDbService.cs
using EsperCertProject.Models;
using System.Collections.Generic; // Added for List<Device>
using System.Threading.Tasks;

namespace EsperCertProject.Services
{
    public interface IMongoDbService
    {
        Task<Device?> GetDeviceByIdAsync(string esperDeviceId);
        Task UpdateDeviceCertificateAsync(string esperDeviceId, CertificateStoreInfo certInfo);

        // NEW: Add the GetExpiringCertificatesAsync signature
        Task<List<Device>> GetExpiringCertificatesAsync(int daysBeforeExpiry);

        // Optional: If you need a method to fully replace a device document
        // Task ReplaceDeviceAsync(string deviceId, Device updatedDevice);
        Task UpdateDeviceAsync(Device device);
    }
}