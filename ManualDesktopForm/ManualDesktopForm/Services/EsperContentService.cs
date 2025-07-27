// EsperCertProject/Services/EsperContentService.cs
using ManualDesktopForm.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ManualDesktopForm.Services
{
    public class EsperContentService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EsperContentService> _logger;
        private readonly EsperSettings _esperSettings;

        // Fixed destination path as per Esper documentation
        public const string CertificateDestinationPath = "/storage/emulated/0/Android/data/io.shoonya.shoonyadpc/files/Downloads/";

        public EsperContentService(HttpClient httpClient, ILogger<EsperContentService> logger, IOptions<EsperSettings> esperSettings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _esperSettings = esperSettings.Value;

            if (_httpClient.DefaultRequestHeaders.Authorization == null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _esperSettings.ApiKey);
            }
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<(string contentId, string contentName)> UploadCertificateAsync(string deviceId, byte[] pfxBytes) // Return type for contentId is string
        {
            _logger.LogInformation($"Attempting to upload certificate for device {deviceId}");

            string uploadUrl = $"{_esperSettings.BaseUrl}enterprise/{_esperSettings.EnterpriseId}/content/upload/";

            _logger.LogInformation($"Uploading to Esper URL: {uploadUrl}");

            using (var content = new MultipartFormDataContent())
            {
                string pfxFilename = $"device_certificate_{deviceId}.p12";
                content.Add(new ByteArrayContent(pfxBytes), "key", pfxFilename);

                string generatedContentName = $"DeviceCertificate_{deviceId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                content.Add(new StringContent(generatedContentName), "content_name");

                content.Add(new StringContent("certificate"), "content_type");

                HttpResponseMessage response = await _httpClient.PostAsync(uploadUrl, content);

                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Esper content upload successful for device {deviceId}. Response: {responseContent}");

                    using (JsonDocument doc = JsonDocument.Parse(responseContent))
                    {
                        if (doc.RootElement.TryGetProperty("id", out JsonElement idElement) &&
                            doc.RootElement.TryGetProperty("name", out JsonElement nameElement))
                        {
                            // --- CORRECTED: Get contentId as long and then convert to string ---
                            // The API returns 'id' as a number, so we must read it as a number first
                            // and then convert it to a string for consistency with your method signatures.
                            long idValue = idElement.GetInt64();
                            string contentId = idValue.ToString(); // Convert the long to string
                                                                   // --- END CORRECTED ---

                            string retrievedContentName = nameElement.GetString() ?? pfxFilename;

                            // Ensure retrievedContentName is not null for your PushToDeviceAsync call later
                            if (string.IsNullOrEmpty(retrievedContentName))
                            {
                                _logger.LogWarning($"Content name not found in response, falling back to generated filename: {pfxFilename}");
                                retrievedContentName = pfxFilename;
                            }

                            return (contentId, retrievedContentName); // Now returning string for contentId
                        }
                        else
                        {
                            _logger.LogError($"Esper API success response did not contain expected 'id' or 'name'. Response: {responseContent}");
                            throw new Exception("Failed to parse content ID or name from Esper API response.");
                        }
                    }
                }
                else
                {
                    _logger.LogError($"Esper API response content for upload error: {responseContent}");
                    // Include full response content in the exception for better debugging
                    throw new HttpRequestException($"Esper content upload failed with status {response.StatusCode}. Message: {response.ReasonPhrase}. Details: {responseContent}");
                }
            }
        }

        public async Task PushToDeviceAsync(string deviceId, String contentId, string contentName)
        {
            _logger.LogInformation($"Attempting to push content ID {contentId} (Name: {contentName}) to device {deviceId}");

            // Step 2 from document: Correct API endpoint for commands
            string pushUrl = $"https://kyndryl-api.esper.cloud/api/commands/v0/commands/";

            _logger.LogInformation($"Pushing to Esper URL: {pushUrl}");

            // Step 2 from document: Correct payload for SYNC_CONTENT
            var payload = new
            {
                command_type = "DEVICE",
                command = "SYNC_CONTENT",
                command_args = new
                {
                    kind = "DOWNLOAD_CONTENT",
                    content_id = contentId,
                    content_destination_path = CertificateDestinationPath + contentName,
                    content_destination_type = "external"
                },
                devices = new[] {
                    deviceId
                },
                device_type = "all"
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            _logger.LogInformation($"Esper push payload: {jsonPayload}");

            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(pushUrl, httpContent);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Esper content push successful for device {deviceId}. Response: {responseContent}");
            }
            else
            {
                _logger.LogError($"Esper API response content for push error: {responseContent}");
                throw new HttpRequestException($"Esper content push failed with status {response.StatusCode}. Message: {response.ReasonPhrase}. Details: {responseContent}");
            }
        }

        // --- NEW METHOD FOR STEP 3: Configure Wifi AP ---
        public async Task ConfigureWifiAPAsync(string deviceId, string certificateFullFileName)
        {
            _logger.LogInformation($"Attempting to configure WiFi AP for device {deviceId} with certificate {certificateFullFileName}");

            // Step 3 from document: Same API endpoint for commands as Step 2
            string configureWifiUrl = $"https://kyndryl-api.esper.cloud/api/commands/v0/commands/";

            _logger.LogInformation($"Configuring WiFi AP to Esper URL: {configureWifiUrl}");

            // Step 3 from document: Request Body for ADD_WIFI_AP
            var payload = new
            {
                command_type = "DEVICE",
                command = "ADD_WIFI_AP", // Specific command for WiFi AP
                command_args = new
                {
                    wifi_access_points = new[]
                    {
                        new
                        {
                            wifi_ssid = _esperSettings.WifiSsid,
                            wifi_security_type = "EAP",
                            wifi_eap_method = "TLS",
                            identity = _esperSettings.WifiIdentity,
                            domain = _esperSettings.WifiDomain,
                            // Full path to the certificate on the device
                            certificate_file_path = CertificateDestinationPath + certificateFullFileName+"/"+certificateFullFileName,
                            certificate_file_password = _esperSettings.CertificatePassword,
                            hidden = false
                        }
                    }
                },
                devices = new[] {
                    deviceId
                },
                device_type = "all"
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            _logger.LogInformation($"Esper WiFi AP payload: {jsonPayload}");

            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(configureWifiUrl, httpContent);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Esper WiFi AP configuration successful for device {deviceId}. Response: {responseContent}");
            }
            else
            {
                _logger.LogError($"Esper API response content for WiFi AP config error: {responseContent}");
                throw new HttpRequestException($"Esper WiFi AP configuration failed with status {response.StatusCode}. Message: {response.ReasonPhrase}. Details: {responseContent}");
            }
        }
    }
}