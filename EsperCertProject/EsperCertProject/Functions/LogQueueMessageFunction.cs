using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EsperCertProject.Functions
{
    public class LogQueueMessage
    {
        private readonly ILogger<LogQueueMessage> _logger;

        public LogQueueMessage(ILogger<LogQueueMessage> logger)
        {
            _logger = logger;
        }

        [Function("LogQueueMessage")]
        public void Run([QueueTrigger("esper-device-certificate-queue", Connection = "AzureWebJobsStorage")] string message)
        {
            _logger.LogInformation("Received queue message.");

            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Queue message is empty or null.");
                return;
            }

            _logger.LogInformation("Raw Message: {Raw}", message);

            try
            {
                using var jsonDoc = JsonDocument.Parse(message);
                string prettyJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("Formatted JSON:\n{Json}", prettyJson);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Message is not valid JSON. Logging raw text.");
            }
        }
    }
}
