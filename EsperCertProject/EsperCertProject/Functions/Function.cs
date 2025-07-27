using EsperCertProject.Models;

using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.Mvc;

using Microsoft.Azure.Functions.Worker;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;

using MongoDB.Bson;

using MongoDB.Driver;

namespace EsperCertProject.Functions;
using Microsoft.Extensions.Configuration;

public class Function

{

    private readonly ILogger<Function> _logger;

    private readonly HttpClient _httpClient;

    private readonly IMongoCollection<BsonDocument> _devicesCollection;

    private readonly string _enterpriseId;

    private readonly string _apiKey;

    private readonly string _esperBaseUrl;
    private readonly IConfiguration _configuration;


    public Function(

        HttpClient httpClient,

        IOptions<EsperSettings> esperSettings,

        IOptions<MongoDbSettings> mongoDbSettings,

        ILogger<EnrollDeviceFunction> logger,
  IConfiguration configuration)

    {
        _configuration = configuration;



    }

    [Function("Function")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        var enterpriseId = _configuration["Esper:EnterpriseId"];
        //string enterpriseIdnew = Environment.GetEnvironmentVariable("Esper:EnterpriseId");

        if (string.IsNullOrEmpty(enterpriseId))
        {
            return new BadRequestObjectResult("EnterpriseId configuration is missing or empty.");
        }

        return new OkObjectResult($"EnterpriseId: {enterpriseId}");
    }

}
