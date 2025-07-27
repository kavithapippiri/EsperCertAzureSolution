// ============================================================================
// Updated GenerateAndUploadCert.cs - Simplified Azure Function
// ============================================================================
using EsperCertProject.Models;
using EsperCertProject.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace EsperCertProject.Functions
{
    public class GenerateAndUploadCert
    {
        private readonly ILogger<GenerateAndUploadCert> _logger;
        private readonly ICertificateProcessor _certificateProcessor;
        private readonly IConfiguration _configuration;

        public GenerateAndUploadCert(
            ILogger<GenerateAndUploadCert> logger,
            ICertificateProcessor certificateProcessor,
            IConfiguration configuration)
        {
            _logger = logger;
            _certificateProcessor = certificateProcessor;
            _configuration = configuration;
        }

        [Function("GenerateAndUploadCert")]
        public async Task Run([QueueTrigger("esper-device-certificate-queue", Connection = "AzureWebJobsStorage")] string message)
        {
            Console.WriteLine("Function entered");

            _logger.LogInformation($"Processing certificate queue message: {message}");

            try
            {
                var deviceRequests = ParseQueueMessage(message);
                await ProcessDeviceRequests(deviceRequests);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Constructor exception: " + ex.ToString());

                _logger.LogError(ex, "Error processing certificate queue message");
                throw;
            }
        }

        private DeviceRequestInfo ParseQueueMessage(string message)
        {
            var deviceIds = new List<string>();
            bool useSelfSignedCert = _configuration.GetValue<bool>("Certificate:UseSelfSigned");

            try
            {
                using JsonDocument doc = JsonDocument.Parse(message);

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    // Parse device IDs array
                    if (doc.RootElement.TryGetProperty("deviceIds", out var idsArray) &&
                        idsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var idElem in idsArray.EnumerateArray())
                        {
                            var deviceId = idElem.GetString();
                            if (!string.IsNullOrWhiteSpace(deviceId))
                                deviceIds.Add(deviceId);
                        }
                    }

                    // Parse self-signed flag
                    if (doc.RootElement.TryGetProperty("useSelfSigned", out var selfSignedFlag))
                    {
                        useSelfSignedCert = selfSignedFlag.ValueKind == JsonValueKind.True;
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.String)
                {
                    var deviceId = doc.RootElement.GetString();
                    if (!string.IsNullOrWhiteSpace(deviceId))
                        deviceIds.Add(deviceId);
                }
            }
            catch (JsonException)
            {
                _logger.LogWarning("Treating message as plain device ID string");
                if (!string.IsNullOrWhiteSpace(message))
                    deviceIds.Add(message);
            }

            return new DeviceRequestInfo
            {
                DeviceIds = deviceIds,
                UseSelfSignedCert = useSelfSignedCert
            };
        }

        private async Task ProcessDeviceRequests(DeviceRequestInfo requestInfo)
        {
            var processedCount = 0;
            var errorCount = 0;

            foreach (var deviceId in requestInfo.DeviceIds)
            {
                try
                {
                    await _certificateProcessor.ProcessDeviceAsync(deviceId, requestInfo.UseSelfSignedCert);
                    processedCount++;
                    _logger.LogInformation($"Successfully processed certificate for device: {deviceId}");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, $"Error processing certificate for device: {deviceId}");
                }
            }

            _logger.LogInformation($"Certificate processing completed. Processed: {processedCount}, Errors: {errorCount}, Total: {requestInfo.DeviceIds.Count}");
        }

        private class DeviceRequestInfo
        {
            public List<string> DeviceIds { get; set; } = new();
            public bool UseSelfSignedCert { get; set; }
        }
    }
}
