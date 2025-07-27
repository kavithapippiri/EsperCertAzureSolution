using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EsperCertProject.Functions;

public class TestMongoConnectionFunction
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestMongoConnectionFunction> _logger;

    public TestMongoConnectionFunction(IConfiguration configuration, ILogger<TestMongoConnectionFunction> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [Function("TestMongoConnection")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        var connStr = _configuration["MongoDb:ConnectionString"];
        var dbName = _configuration["MongoDb:DatabaseName"];
        var collName = _configuration["MongoDb:CollectionName"];

        if (string.IsNullOrWhiteSpace(connStr) || string.IsNullOrWhiteSpace(dbName) || string.IsNullOrWhiteSpace(collName))
        {
            _logger.LogError("One or more MongoDB config values are missing.");
            return new BadRequestObjectResult("MongoDB configuration values are missing.");
        }

        try
        {
            var client = new MongoClient(connStr);
            var db = client.GetDatabase(dbName);
            var collection = db.GetCollection<BsonDocument>(collName);

            // Try a simple query to validate connectivity
            var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

            _logger.LogInformation("Successfully connected to MongoDB. Document count: {Count}", count);
            return new OkObjectResult($"Connected to MongoDB! Document count in '{collName}': {count}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to MongoDB.");
            return new ObjectResult("MongoDB connection failed: " + ex.Message) { StatusCode = 500 };
        }
    }
}
