// ============================================================================
// ICertificateProcessor.cs - Certificate Processing Interface
// ============================================================================
using EsperCertProject.Models;
using System.Threading.Tasks;

namespace EsperCertProject.Services
{
    public interface ICertificateProcessor
    {
        Task ProcessDeviceAsync(string deviceId, bool useSelfSignedCert);
        Task ProcessDeviceRenewalAsync(Device device);
        Task<GeneratedCertificateResult> GenerateSelfSignedCertificateAsync(string deviceId);
    }
}