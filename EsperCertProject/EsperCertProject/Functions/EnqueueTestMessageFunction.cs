using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace EsperCertProject.Functions
{
    public class EnqueueTestMessageFunction
    {
        private readonly ILogger _logger;

        public EnqueueTestMessageFunction(ILogger<EnqueueTestMessageFunction> logger)
        {
            _logger = logger;
        }

        [Function("EnqueueTestMessage")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var queueName = "esper-device-certificate-queue";
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            _logger.LogInformation("EnqueueTestMessageFunction triggered. QueueName: {QueueName}, ConnectionString: {ConnectionString}", queueName, connectionString);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError("AzureWebJobsStorage is not set.");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("AzureWebJobsStorage is not configured.");
                return errorResponse;
            }

            var testPayload = new
            {
                deviceIds = new List<string>
                {
                    "eef639f1-819d-43fb-9f4a-ab0498597021",
                    "9be63b7f-e836-4692-afc1-cbb8a82be01b"
                },
                useSelfSigned = false
            };

            string jsonMessage = JsonSerializer.Serialize(testPayload);

            try
            {
                var queueClient = new QueueClient(connectionString, queueName);
                await queueClient.CreateIfNotExistsAsync();
                await queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonMessage)));

                _logger.LogInformation("Enqueued test message to queue: {QueueName}", queueName);

                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteStringAsync($"Test message enqueued successfully to '{queueName}'.");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueueing test message.");
                var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Failed to enqueue test message.");
                return response;
            }
        }
    }
}
