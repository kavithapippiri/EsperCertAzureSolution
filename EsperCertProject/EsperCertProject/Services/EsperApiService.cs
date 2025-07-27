using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace EsperCertProject.Services
{
    public class EsperApiService
    {
        private readonly HttpClient _http;
        private readonly string _tenant;
        private readonly string _enterpriseId;
        private readonly string _apiKey;

        public EsperApiService(string tenant, string enterpriseId, string apiKey, HttpClient? httpClient = null)
        {
            _tenant = tenant;
            _enterpriseId = enterpriseId;
            _apiKey = apiKey;
            _http = httpClient ?? new HttpClient();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        /// <summary>
        /// Uploads a PKCS12 certificate bundle to the Esper tenant.
        /// </summary>
        /// <param name="filePath">Full path to the .p12/.pfx file</param>
        /// <returns>The parsed upload response</returns>
        public async Task<UploadResponse> UploadCertificateAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Certificate file not found", filePath);

            using var form = new MultipartFormDataContent();
            using var fs = File.OpenRead(filePath);
            var content = new StreamContent(fs);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-pkcs12");
            form.Add(content, "key", Path.GetFileName(filePath));

            var url = $"https://{_tenant}-api.esper.cloud/api/v0/enterprise/{_enterpriseId}/content/upload/";
            var resp = await _http.PostAsync(url, form);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UploadResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }

        /// <summary>
        /// Instructs devices to download the previously uploaded certificate.
        /// </summary>
        /// <param name="contentId">ID returned by UploadCertificateAsync</param>
        /// <param name="deviceIds">One or more device UUIDs</param>
        /// <param name="destinationPath">Filesystem path on the device</param>
        public async Task TransferCertificateAsync(
            long contentId,
            string[] deviceIds,
            string destinationPath = "/storage/emulated/0/Android/data/io.shoonya.shoonyadpc/files/Downloads/")
        {
            var payload = new
            {
                command_type = "DEVICE",
                command = "SYNC_CONTENT",
                command_args = new
                {
                    kind = "DOWNLOAD_CONTENT",
                    content_id = contentId,
                    content_destination_path = destinationPath,
                    content_destination_type = "external",
                    ui_content_name = (string?)null
                },
                devices = deviceIds,
                device_type = "all"
            };

            var url = $"https://{_tenant}-api.esper.cloud/api/v0/commands/enterprise/{_enterpriseId}/commands/";
            var resp = await _http.PostAsJsonAsync(url, payload);
            resp.EnsureSuccessStatusCode();
        }

        public class UploadResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsDir { get; set; }
            public string Kind { get; set; } = "";
            public string Hash { get; set; } = "";
            public long Size { get; set; }
            public string Path { get; set; } = "";
            public string Permissions { get; set; } = "";
        }
    }
}
