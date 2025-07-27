using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EsperCertProject.Functions
{
    public class TestSubmitCsrFunction
    {
        private readonly ILogger _logger;

        public TestSubmitCsrFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TestSubmitCsrFunction>();
        }

        [Function("TestSubmitCsrFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("TestSubmitCsrFunction triggered.");
            string csrPem = @"
-----BEGIN CERTIFICATE REQUEST-----
MIICdDCCAVwCAQAwLzEtMCsGA1UEAwwkZWVmNjM5ZjEtODE5ZC00M2ZiLTlmNGEt
YWIwNDk4NTk3MDIxMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4dxk
LIuLdmB8ImqGzupippcdXYYdXvvUOJFmjq8wLQ5AYGHX/NTr/IsejZ7f9Sj2Actg
s5D68iUcqtrsry/q/mol7pi0ZeHJbljJFi0VS++W+WjmZgRv5FRb5hC4QqpGf4n1
UHKvd/f7SN0Qn6VomHI2woLEi6AVbMl1AbadSxSuRpgNRZlQuvNmqvnj+dPNKNzp
p0IBzCu1aisn6cgI3kjRa9sdcrdiiTB95NxGUdg6RZyrAbGuaR2+mZzc68rjfRIB
T8acNNr/Nt0tgnWl7ZKvP25xQGqWRiwiEct2khQ+k32cxc2w3fVbvgtytooETNlp
jL8eSRrpdmVTZ+or5QIDAQABoAAwDQYJKoZIhvcNAQELBQADggEBAHbHjaagIm9g
r2GkyTGjaIKceUGzqH3JpSFz3SHGz1D3ZAKfvra7LJelhbEGD1Ex4lWTERyfA++E
veO4BLSxhbzDmRC5wxT123zr7Np7tzdeidiAK8W7vnLCPeY5GbRE+r9WHv7oje+P
R9RtIT5TvZgqMzwqCRObLHOokbHRZbniVgT6Sn/WZrWU17dhBQkbzhUbvwdYhkeJ
IG/Y2m50KKuILK5i/WWSFlQfV/ReHJcWjmovU1gXdFgy1jOes6wiSw6rUgC737Dv
YpQ80hnIeoWAaYLNYfs5yNKZgnQijjOFfTqAmix389jMcoDt5wCz/KIDCzCNciBn
pzYjx+TLDJk=
-----END CERTIFICATE REQUEST-----
";

            var cleanCsr = csrPem
                .Replace("-----BEGIN CERTIFICATE REQUEST-----", "")
                .Replace("-----END CERTIFICATE REQUEST-----", "")
                .Replace("\r", "").Replace("\n", "").Trim();

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential("kavithap@mbs.com", "LAGGzy6JtM3t", ""),
                PreAuthenticate = true,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using var client = new HttpClient(handler);

            var form = new Dictionary<string, string>
            {
                { "Mode", "newreq" },
                { "CertRequest", cleanCsr },
                { "CertAttrib", "CertificateTemplate:EsperAutoWebServerCert" },
                { "TargetStoreFlags", "0" },
                { "SaveCert", "yes" },
                { "ThumbPrint", "" }
            };

            var response = await client.PostAsync(
                "https://mbsdc.mbs.com/certsrv/certfnsh.asp",
                new FormUrlEncodedContent(form)
            );

            var content = await response.Content.ReadAsStringAsync();

            var res = req.CreateResponse(response.IsSuccessStatusCode ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            await res.WriteStringAsync(content);
            return res;
        }
    }
}
