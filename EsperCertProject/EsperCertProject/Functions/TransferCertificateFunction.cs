using EsperCertProject.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace EsperCertProject.Functions
{
    public class TransferCertificateFunction
    {
        private readonly EsperApiService _esper;

        public TransferCertificateFunction(IConfiguration config)
        {
            var tenant = config["EsperTenant"]!;
            var enterpriseId = config["EsperEnterpriseId"]!;
            var apiKey = config["EsperApiKey"]!;
            _esper = new EsperApiService(tenant, enterpriseId, apiKey);
        }

        [Function("TransferCertificate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "certificates/transfer")] HttpRequestData req,
            FunctionContext context)
        {
            var log = context.GetLogger("TransferCertificate");
            log.LogInformation("TransferCertificate triggered.");

            // Expect JSON body:
            // { "contentId": 1734, "deviceIds": ["uuid1","uuid2"], "destinationPath": "/storage/..."}
            var body = await JsonSerializer.DeserializeAsync<TransferRequest>(req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body == null || body.ContentId == 0 || body.DeviceIds?.Length == 0)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Please supply contentId (long) and deviceIds (array).");
                return bad;
            }

            try
            {
                await _esper.TransferCertificateAsync(body.ContentId, body.DeviceIds, body.DestinationPath);
                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteStringAsync("Transfer command sent.");
                return ok;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Transfer failed");
                var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
                await resp.WriteStringAsync(ex.Message);
                return resp;
            }
        }

        private class TransferRequest
        {
            public long ContentId { get; set; }
            public string[]? DeviceIds { get; set; }
            public string DestinationPath { get; set; } = "/storage/emulated/0/Android/data/io.shoonya.shoonyadpc/files/Downloads/";
        }
    }
}
