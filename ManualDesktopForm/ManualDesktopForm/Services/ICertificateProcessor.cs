// ============================================================================
// ICertificateProcessor.cs - Certificate Processing Interface
// ============================================================================
using ManualDesktopForm.Models;
using System.Threading.Tasks;

namespace ManualDesktopForm.Services
{
    public interface ICertificateProcessor
    {
        Task ProcessDeviceAsync(string deviceId, bool useSelfSignedCert);
        Task ProcessDeviceRenewalAsync(Device device);
        Task<GeneratedCertificateResult> GenerateSelfSignedCertificateAsync(string deviceId);
    }
}