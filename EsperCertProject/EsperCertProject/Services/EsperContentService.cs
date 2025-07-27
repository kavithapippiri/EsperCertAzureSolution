using EsperCertProject.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EsperCertProject.Services
{
    public class EsperContentService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EsperContentService> _logger;
        private readonly EsperSettings _esperSettings;

        // Certificate destination path, configurable if you want to move to config later
        public const string CertificateDestinationPath = "/storage/emulated/0/Android/data/io.shoonya.shoonyadpc/files/Downloads/";

        public EsperContentService(HttpClient httpClient, ILogger<EsperContentService> logger, IOptions<EsperSettings> esperSettings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _esperSettings = esperSettings.Value;

            // Apply Bearer only once
            if (_httpClient.DefaultRequestHeaders.Authorization == null)
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _esperSettings.ApiKey);

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Uploads a .pfx certificate file to Esper Content API for the given device.
        /// </summary>
        public async Task<(string contentId, string contentName)> UploadCertificateAsync(string deviceId, byte[] pfxBytes)
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
                            // 'id' is returned as number, cast to string
                            long idValue = idElement.GetInt64();
                            string contentId = idValue.ToString();

                            string retrievedContentName = nameElement.GetString() ?? pfxFilename;

                            if (string.IsNullOrEmpty(retrievedContentName))
                            {
                                _logger.LogWarning($"Content name not found, using fallback filename: {pfxFilename}");
                                retrievedContentName = pfxFilename;
                            }

                            return (contentId, retrievedContentName);
                        }
                        else
                        {
                            _logger.LogError("Esper API success response missing 'id' or 'name': " + responseContent);
                            throw new Exception("Failed to parse content ID or name from Esper API response.");
                        }
                    }
                }
                else
                {
                    _logger.LogError("Esper API upload error: " + responseContent);
                    throw new HttpRequestException($"Esper content upload failed with status {response.StatusCode}. Details: {responseContent}");
                }
            }
        }

        /// <summary>
        /// Pushes uploaded content (certificate) to a device using Esper SYNC_CONTENT command.
        /// </summary>
        public async Task PushToDeviceAsync(string deviceId, string contentId, string contentName)
        {
            _logger.LogInformation($"Attempting to push content ID {contentId} (Name: {contentName}) to device {deviceId}");

            // Use your Esper BaseUrl, or direct to /commands/v0/commands/
            string pushUrl = $"{_esperSettings.BaseUrl.Replace("/api/v0/", "/api/commands/v0/commands/")}";
            // If above doesn't work, fallback to explicit:
            // string pushUrl = "https://kyndryl-api.esper.cloud/api/commands/v0/commands/";

            _logger.LogInformation($"Pushing to Esper URL: {pushUrl}");

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
                devices = new[] { deviceId },
                device_type = "all"
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            _logger.LogInformation($"Esper push payload: {jsonPayload}");

            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(pushUrl, httpContent);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                _logger.LogInformation($"Esper content push successful for device {deviceId}. Response: {responseContent}");
            else
            {
                _logger.LogError($"Esper API response content for push error: {responseContent}");
                throw new HttpRequestException($"Esper content push failed with status {response.StatusCode}. Details: {responseContent}");
            }
        }

        /// <summary>
        /// Configure the device's Wi-Fi settings using the uploaded certificate (for EAP-TLS).
        /// </summary>
        public async Task ConfigureWifiAPAsync(string deviceId, string certificateFullFileName)
        {
            _logger.LogInformation($"Attempting to configure WiFi AP for device {deviceId} with certificate {certificateFullFileName}");

            string configureWifiUrl = $"{_esperSettings.BaseUrl.Replace("/api/v0/", "/api/commands/v0/commands/")}";
            // string configureWifiUrl = "https://kyndryl-api.esper.cloud/api/commands/v0/commands/";

            _logger.LogInformation($"Configuring WiFi AP at Esper URL: {configureWifiUrl}");

            var payload = new
            {
                command_type = "DEVICE",
                command = "ADD_WIFI_AP",
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
                            // File path on device
                            certificate_file_path = CertificateDestinationPath + certificateFullFileName,
                            certificate_file_password = _esperSettings.CertificatePassword,
                            hidden = false
                        }
                    }
                },
                devices = new[] { deviceId },
                device_type = "all"
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            _logger.LogInformation($"Esper WiFi AP payload: {jsonPayload}");

            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(configureWifiUrl, httpContent);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                _logger.LogInformation($"Esper WiFi AP configuration successful for device {deviceId}. Response: {responseContent}");
            else
            {
                _logger.LogError($"Esper API response content for WiFi AP config error: {responseContent}");
                throw new HttpRequestException($"Esper WiFi AP configuration failed with status {response.StatusCode}. Details: {responseContent}");
            }
        }
    }
}
