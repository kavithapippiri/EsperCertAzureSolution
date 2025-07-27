using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace EsperCertProject.Functions
{
    public class TestCAReachability
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        public TestCAReachability(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TestCAReachability>();

            // Suppress SSL certificate validation (bypass hostname/CN check)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _httpClient = new HttpClient(handler);
        }

        [Function("TestCAReachability")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            var response = req.CreateResponse();
            //string urlToTest = "https://10.0.0.6/certsrv/certfnsh.asp";
            string urlToTest = "https://mbsdc.mbs.com/certsrv/certfnsh.asp";

            _logger.LogInformation("Testing reachability to CA server: {Url}", urlToTest);

            try
            {
                HttpResponseMessage result = await _httpClient.GetAsync(urlToTest);

                string content = $"Success: {(int)result.StatusCode} {result.ReasonPhrase}";
                _logger.LogInformation(content);

                response.StatusCode = HttpStatusCode.OK;
                await response.WriteStringAsync(content);
            }
            catch (HttpRequestException ex)
            {
                string error = $"Request error: {ex.Message}";
                _logger.LogError(ex, error);
                response.StatusCode = HttpStatusCode.ServiceUnavailable;
                await response.WriteStringAsync(error);
            }
            catch (Exception ex)
            {
                string error = $"Unexpected error: {ex.Message}";
                _logger.LogError(ex, error);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync(error);
            }

            return response;
        }
    }
}
