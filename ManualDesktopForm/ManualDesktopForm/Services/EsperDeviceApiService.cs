using ManualDesktopForm.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class EsperDeviceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EsperDeviceApiService> _logger;
    private readonly EsperSettings _esperSettings;

    public EsperDeviceApiService(HttpClient httpClient, ILogger<EsperDeviceApiService> logger, EsperSettings esperSettings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _esperSettings = esperSettings ?? throw new ArgumentNullException(nameof(esperSettings));
    }

    public async Task<List<Device>> GetEsperDevicesAsync()
    {
        _logger.LogInformation("Fetching devices from Esper API...");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"enterprise/{_esperSettings.EnterpriseId}/device/");

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _esperSettings.ApiKey);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            string jsonResponse = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode(); // throws on 401, etc.

            var esperApiDevices = JsonConvert.DeserializeObject<EsperDeviceApiResponse>(jsonResponse);

            var mappedDevices = esperApiDevices.results.Select(esperDev => new Device
            {
                DeviceId = esperDev.id,
                DeviceName = esperDev.device_name,
                SerialNumber = esperDev.serial_number,
                DeviceModel = new DeviceModelInfo { Name = esperDev.device_model_name },
                EnrolledOn = esperDev.created,
                Status = MapEsperStatusToLocalStatus(esperDev.status),
                UpdatedOn = esperDev.modified
            }).ToList();

            _logger.LogInformation($"Successfully fetched {mappedDevices.Count} devices from Esper API.");
            return mappedDevices;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP request error: {ex.Message}");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"JSON deserialization error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error: {ex.Message}");
            throw;
        }
    }

    private int MapEsperStatusToLocalStatus(string esperStatus)
    {
        return esperStatus?.ToUpperInvariant() switch
        {
            "ONLINE" => 1,
            "OFFLINE" => 0,
            _ => -1,
        };
    }
}
