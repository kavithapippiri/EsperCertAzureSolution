using Azure.Storage.Queues;
using ManualDesktopForm.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace ManualDesktopForm.Services
{
    public class QueueSettings
    {
        public string ConnectionString { get; set; }
        public string QueueName { get; set; }
    }

    public class QueueService
    {
        private readonly QueueClient _queueClient;
        public readonly bool _useSelfSigned;

        public QueueService()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var queueSettings = config.GetSection("QueueSettings").Get<QueueSettings>();

            if (queueSettings is null)
                throw new InvalidOperationException("Missing QueueSettings in appsettings.json");

            _queueClient = new QueueClient(queueSettings.ConnectionString, queueSettings.QueueName);
            _queueClient.CreateIfNotExists();
        }

        public async Task<bool> EnqueueDevicesAsync(RequestCertRequest request, Action<string> logger = null)
        {
            string json = JsonSerializer.Serialize(request);
            logger?.Invoke($"Sending request: {json}");

            try
            {
                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                await _queueClient.SendMessageAsync(base64);
                logger?.Invoke("Enqueued to queue.");
                return true;
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Queue enqueue failed: {ex.Message}");
                return false;
            }
        }
    }
}
