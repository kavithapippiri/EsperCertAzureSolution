using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EsperCertProject.Functions;

public class EnrollDeviceFunction
{
    private readonly ILogger<EnrollDeviceFunction> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public EnrollDeviceFunction(
        ILogger<EnrollDeviceFunction> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    [Function("EnrollDevice")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "devices/enroll")] HttpRequestData req,
        FunctionContext ctx)
    {
        var esperBaseUrl = _configuration["Esper:BaseUrl"];
        var apiKey = _configuration["Esper:ApiKey"];
        var enterpriseId = _configuration["Esper:EnterpriseId"];

        var mongoConn = _configuration["MongoDb:ConnectionString"];
        var dbName = _configuration["MongoDb:DatabaseName"];
        var collectionName = _configuration["MongoDb:CollectionName"];

        if (string.IsNullOrWhiteSpace(esperBaseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(enterpriseId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Esper configuration is missing.");
            return bad;
        }

        if (string.IsNullOrWhiteSpace(mongoConn) || string.IsNullOrWhiteSpace(dbName) || string.IsNullOrWhiteSpace(collectionName))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("MongoDB configuration is missing.");
            return bad;
        }

        _httpClient.BaseAddress = new Uri(esperBaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var url = $"enterprise/{enterpriseId}/device/";
        _logger.LogInformation("Calling Esper API: {url}", url);

        using var resp = await _httpClient.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            var errorContent = await resp.Content.ReadAsStringAsync();
            _logger.LogError("Esper API error: {StatusCode} - {ErrorContent}", resp.StatusCode, errorContent);
            var error = req.CreateResponse(resp.StatusCode);
            await error.WriteStringAsync($"Esper API request failed: {errorContent}");
            return error;
        }

        var json = await resp.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<DeviceListResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (list?.Results == null || list.Results.Count == 0)
        {
            var none = req.CreateResponse(HttpStatusCode.NoContent);
            await none.WriteStringAsync("No devices returned from Esper.");
            return none;
        }

        var client = new MongoClient(mongoConn);
        var db = client.GetDatabase(dbName);
        var _devicesCollection = db.GetCollection<BsonDocument>(collectionName);

        var devicesProcessed = 0;
        foreach (var d in list.Results)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("id", d.Id);
            var update = Builders<BsonDocument>.Update
                .Set("id", d.Id)
                .Set("device_name", d.DeviceName ?? (object)BsonNull.Value)
                .Set("device_model", d.DeviceModel ?? (object)BsonNull.Value)
                .Set("status", d.Status)
                .Set("serialNumber", d.HardwareInfo?.SerialNumber ?? (object)BsonNull.Value)
                .Set("enrolled_on", d.CreatedOn != default ? d.CreatedOn : (object)BsonNull.Value)
                .Set("updated_on", d.UpdatedOn != default ? d.UpdatedOn : (object)BsonNull.Value)
                .SetOnInsert("certificate", BsonNull.Value);

            try
            {
                await _devicesCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
                devicesProcessed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing device {DeviceId}", d.Id);
            }
        }

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteStringAsync($"{devicesProcessed} devices processed and stored in MongoDB.");
        return ok;
    }

    private class DeviceListResponse
    {
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("next")] public string? Next { get; set; }
        [JsonPropertyName("previous")] public string? Previous { get; set; }
        [JsonPropertyName("results")] public List<DeviceDto> Results { get; set; } = new();
    }

    private class DeviceDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("device_name")] public string? DeviceName { get; set; }
        [JsonPropertyName("device_model")] public string? DeviceModel { get; set; }
        [JsonPropertyName("status")] public int Status { get; set; }
        [JsonPropertyName("hardwareInfo")] public HardwareInfoDto? HardwareInfo { get; set; }
        [JsonPropertyName("created_on")] public DateTime CreatedOn { get; set; }
        [JsonPropertyName("updated_on")] public DateTime UpdatedOn { get; set; }
    }

    private class HardwareInfoDto
    {
        [JsonPropertyName("serialNumber")] public string? SerialNumber { get; set; }
    }
}
