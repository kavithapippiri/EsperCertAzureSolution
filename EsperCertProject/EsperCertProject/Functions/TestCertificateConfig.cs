using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EsperCertProject.Functions
{
    public class TestCertificateConfig
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public TestCertificateConfig(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<TestCertificateConfig>();
            _configuration = configuration;
        }

        [Function("TestCertificateConfig")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Reading CertSrv and Certificate configuration settings...");

            var configValues = new
            {
                CertSrv = new
                {
                    BaseUrl = _configuration["CertSrv:BaseUrl"],
                    CaRequesterUsername = _configuration["CertSrv:CaRequesterUsername"],
                    CaRequesterPassword = "[HIDDEN]", // mask for security
                    TemplateName = _configuration["CertSrv:TemplateName"]
                },
                Certificate = new
                {
                    UseSelfSigned = _configuration.GetValue<bool>("Certificate:UseSelfSigned"),
                    ForceIssuance = _configuration.GetValue<bool>("Certificate:ForceIssuance"),
                    RenewalThresholdDays = _configuration.GetValue<int>("Certificate:RenewalThresholdDays", 30)
                }
            };

            string json = JsonSerializer.Serialize(configValues, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(json);

            return response;
        }
    }
}
