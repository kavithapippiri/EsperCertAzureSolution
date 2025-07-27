using System.Net;
using System.Net.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace EsperCertProject.Functions
{
    public class TestHttpClientFunction
    {
        private readonly ILogger _logger;

        public TestHttpClientFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TestHttpClientFunction>();
        }

        [Function("TestHttpClientFunction")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            var username = "kavithap@mbs.com";
            var password = "LAGGzy6JtM3t";
            var domain = ""; // Optional domain

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password, domain),
                PreAuthenticate = true,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            var httpClient = new HttpClient(handler);
            var response = await httpClient.GetAsync("https://mbsdc.mbs.com/certsrv/certrqxt.asp");

            var result = req.CreateResponse(response.IsSuccessStatusCode ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            await result.WriteStringAsync($"Status: {response.StatusCode}, Content Length: {response.Content.Headers.ContentLength}");
            return result;
        }
    }
}
