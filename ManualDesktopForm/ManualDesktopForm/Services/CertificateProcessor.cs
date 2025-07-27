// ManualDesktopForm/Services/CertificateProcessor.cs
using ManualDesktopForm.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ManualDesktopForm.Services
{
    public class CertificateProcessor : ICertificateProcessor
    {
        private readonly ILogger<CertificateProcessor> _logger;
        private readonly IMongoDbService _mongoDbService;
        private readonly EsperContentService _esperContentService;
        private readonly CertificateService _certificateService;
        private readonly ICertSrvService _certSrvService;
        private readonly IConfiguration _configuration;

        public CertificateProcessor(
            ILogger<CertificateProcessor> logger,
            IMongoDbService mongoDbService,
            EsperContentService esperContentService,
            CertificateService certificateService,
            ICertSrvService certSrvService,
            IConfiguration configuration)
        {
            _logger = logger;
            _mongoDbService = mongoDbService;
            _esperContentService = esperContentService;
            _certificateService = certificateService;
            _certSrvService = certSrvService;
            _configuration = configuration;
        }

        public async Task ProcessDeviceAsync(string deviceId, bool useSelfSignedCert)
        {
            _logger.LogInformation($"Processing certificate for device: {deviceId}");

            var device = await _mongoDbService.GetDeviceByIdAsync(deviceId);
            if (device == null)
            {
                _logger.LogWarning($"Device '{deviceId}' not found in database");
                // Optionally, you could update a global status or a different log for unidentifiable devices
                return;
            }

            // Set initial status to "Processing"
            device.ProcessingStatus = "Processing";
            device.LastProcessingMessage = $"Initiating certificate processing for device {deviceId}.";
            device.LastProcessedOn = DateTime.UtcNow;
            await _mongoDbService.UpdateDeviceAsync(device);

            try
            {
                // Check if certificate needs renewal or creation
                bool needsCertificate = device.Certificate?.ExpiryDate == null ||
                                       (device.Certificate.ExpiryDate - DateTime.UtcNow).TotalDays <= 30;

                if (!needsCertificate)
                {
                    string skipMessage = $"Device {deviceId} certificate is still valid, skipping.";
                    _logger.LogInformation(skipMessage);
                    device.ProcessingStatus = "Skipped";
                    device.LastProcessingMessage = skipMessage;
                    device.LastProcessedOn = DateTime.UtcNow;
                    await _mongoDbService.UpdateDeviceAsync(device); // Update status in DB
                    return;
                }

                if (useSelfSignedCert)
                {
                    await ProcessSelfSignedCertificate(device);
                }
                else
                {
                    await ProcessCertSrvCertificate(device);
                }

                string successMessage = $"Successfully processed certificate for device: {device.DeviceId}.";
                _logger.LogInformation(successMessage);
                device.ProcessingStatus = "Completed";
                device.LastProcessingMessage = successMessage;
                device.LastProcessedOn = DateTime.UtcNow;
                await _mongoDbService.UpdateDeviceAsync(device); // Final success update

            }
            catch (Exception ex)
            {
                string errorMessage = $"Error processing certificate for device {deviceId}: {ex.Message}";
                _logger.LogError(ex, errorMessage);

                device.ProcessingStatus = "Failed";
                device.LastProcessingMessage = errorMessage;
                device.LastProcessedOn = DateTime.UtcNow;
                await _mongoDbService.UpdateDeviceAsync(device); // Update status in DB on error
                throw; // Re-throw to ensure Azure Function captures the error
            }
        }

        public async Task ProcessDeviceRenewalAsync(Device device)
        {
            _logger.LogInformation($"Processing certificate renewal for device: {device.DeviceId}");

            // Set initial status to "Processing Renewal"
            device.ProcessingStatus = "Processing Renewal";
            device.LastProcessingMessage = $"Initiating certificate renewal for device {device.DeviceId}.";
            device.LastProcessedOn = DateTime.UtcNow;
            await _mongoDbService.UpdateDeviceAsync(device);

            try
            {
                if (device.Certificate?.ExpiryDate != null &&
                    (device.Certificate.ExpiryDate - DateTime.UtcNow).TotalDays > 30)
                {
                    string skipRenewalMessage = $"Certificate for device {device.DeviceId} not expiring soon, skipping renewal.";
                    _logger.LogInformation(skipRenewalMessage);
                    device.ProcessingStatus = "Skipped"; // Or "Valid"
                    device.LastProcessingMessage = skipRenewalMessage;
                    device.LastProcessedOn = DateTime.UtcNow;
                    await _mongoDbService.UpdateDeviceAsync(device); // Update status in DB
                    return;
                }

                // For renewal, always use the same method as original certificate (assuming configuration determines it)
                bool useSelfSigned = _configuration.GetValue<bool>("Certificate:UseSelfSigned");
                await ProcessDeviceAsync(device.DeviceId, useSelfSigned); // This call will update status
                                                                          // ProcessDeviceAsync handles its own status updates, no need to duplicate final success here.
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error processing certificate renewal for device {device.DeviceId}: {ex.Message}";
                _logger.LogError(ex, errorMessage);
                device.ProcessingStatus = "Failed";
                device.LastProcessingMessage = errorMessage;
                device.LastProcessedOn = DateTime.UtcNow;
                await _mongoDbService.UpdateDeviceAsync(device); // Update status in DB on error
                throw; // Re-throw to ensure Azure Function captures the error
            }
        }

        public async Task<GeneratedCertificateResult> GenerateSelfSignedCertificateAsync(string deviceId)
        {
            // Note: This method only generates the cert, it doesn't upload or configure it.
            // It's meant for scenarios where only generation is needed, e.g., for testing or specific flows.
            return _certificateService.GeneratePfxSelfSignedCertificate(deviceId);
        }

        private async Task ProcessSelfSignedCertificate(Device device)
        {
            _logger.LogInformation($"Generating self-signed certificate for device: {device.DeviceId}");
            // Status already set to "Processing" in ProcessDeviceAsync
            try
            {
                var certResult = _certificateService.GeneratePfxSelfSignedCertificate(device.DeviceId);
                await UploadAndConfigureCertificate(device, certResult.PfxBytes, certResult);

                // Final status update for success handled in ProcessDeviceAsync's try block
            }
            catch (Exception ex)
            {
                // Exceptions re-thrown and handled by the outer ProcessDeviceAsync catch block
                throw;
            }
        }

        private async Task ProcessCertSrvCertificate(Device device)
        {
            _logger.LogInformation($"Generating certificate via CertSrv for device: {device.DeviceId}");
            // Status already set to "Processing" in ProcessDeviceAsync
            try
            {
                var templateName = _configuration["CertSrv:TemplateName"] ?? "Web Server";

                // Generate CSR
                var csrResult = _certificateService.GenerateCsrAndPrivateKey(device.DeviceId);

                // Submit to CertSrv
                var issuedCertificate = await _certSrvService.SubmitCsrAndRetrieveCertificateAsync(csrResult.CsrPem, templateName);

                // Combine certificate with private key
                var pfxBytes = _certificateService.CombineCertificateAndPrivateKey(
                    issuedCertificate,
                    csrResult.PrivateKey,
                    _configuration["Esper:CertificatePassword"] ?? "whatever",
                    "Enterprise Client Certificate");

                var certInfo = new GeneratedCertificateResult
                {
                    PfxBytes = pfxBytes,
                    Thumbprint = issuedCertificate.Thumbprint,
                    CommonName = issuedCertificate.GetNameInfo(X509NameType.SimpleName, false),
                    CreatedDate = issuedCertificate.NotBefore,
                    ExpiryDate = issuedCertificate.NotAfter
                };

                await UploadAndConfigureCertificate(device, pfxBytes, certInfo);

                // Final status update for success handled in ProcessDeviceAsync's try block
            }
            catch (Exception ex)
            {
                // Exceptions re-thrown and handled by the outer ProcessDeviceAsync catch block
                throw;
            }
        }

        private async Task UploadAndConfigureCertificate(Device device, byte[] pfxBytes, GeneratedCertificateResult certInfo)
        {
            _logger.LogInformation($"Uploading and configuring certificate for device: {device.DeviceId}");
            // Upload to Esper
            var (contentId, contentName) = await _esperContentService.UploadCertificateAsync(device.DeviceId, pfxBytes);

            // Push to device
            await _esperContentService.PushToDeviceAsync(device.DeviceId, contentId, contentName);

            // Configure WiFi
            await _esperContentService.ConfigureWifiAPAsync(device.DeviceId, contentName);

            // Update device in MongoDB with new certificate details
            device.Certificate = new CertificateStoreInfo
            {
                EsperContentId = contentId,
                EsperContentName = contentName,
                DeviceFilePath = EsperContentService.CertificateDestinationPath + contentName,
                LastConfigured = DateTime.UtcNow,
                Thumbprint = certInfo.Thumbprint,
                CommonName = certInfo.CommonName,
                CreatedDate = certInfo.CreatedDate,
                ExpiryDate = certInfo.ExpiryDate,
                LastUpdated = DateTime.UtcNow,
                RawBytes = pfxBytes,
                Base64Raw = Convert.ToBase64String(pfxBytes)
            };

            // *** IMPORTANT: Persist the updated device object to the database ***
            await _mongoDbService.UpdateDeviceAsync(device);
            _logger.LogInformation($"Updated device {device.DeviceId} in MongoDB with new certificate details.");

            // ProcessingStatus and LastProcessingMessage are handled by the caller (ProcessDeviceAsync/ProcessDeviceRenewalAsync)
        }
    }
}