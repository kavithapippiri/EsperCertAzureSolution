using EsperCertProject.Models;
using EsperCertProject.Services;
using EsperCertProject.Tests.Utils;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RichardSzalay.MockHttp;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Xunit;

public class CertSrvServiceTests
{
    private static CertSrvService BuildSut(MockHttpMessageHandler mock, IConfiguration config)
    {
        var logger = new NullLogger<CertSrvService>();
        var certSrvSettings = new CertSrvSettings
        {
            BaseUrl = config.GetSection("CertSrvSettings:BaseUrl").Value,
            CaRequesterUsername = config.GetSection("CertSrvSettings:CaRequesterUsername").Value,
            CaRequesterPassword = config.GetSection("CertSrvSettings:CaRequesterPassword").Value,
            CaRequesterDomain = config.GetSection("CertSrvSettings:CaRequesterDomain").Value
        };
        var options = Options.Create(certSrvSettings);
        return new CertSrvService(logger, options);
    }

    [Fact]
    public async Task RetrievePendingCertificate_Throws_When_NotReady()
    {
        // Arrange
        var (_, mock) = HttpClientBuilder.Build();
        var config = ConfigurationStub.Build();
        var sut = BuildSut(mock, config);

        mock.When(HttpMethod.Get, "*certnew.cer?ReqID=999*")
            .Respond(HttpStatusCode.OK, "text/html", "<html><body>Pending</body></html>");

        // Act
        Func<Task> act = async () => await sut.RetrievePendingCertificate("999");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not yet available*");
    }

    // SubmitCSR should throw when certificate not found in response
    [Fact]
    public async Task SubmitCSR_Throws_When_Certificate_Not_Found()
    {
        // Arrange
        var (_, mock) = HttpClientBuilder.Build();
        var config = ConfigurationStub.Build();
        var sut = BuildSut(mock, config);

        string fakeCsr = "-----BEGIN CERTIFICATE REQUEST-----\n...\n-----END CERTIFICATE REQUEST-----";

        string responseWithoutCert = "<html><body>No cert found</body></html>";

        mock.When(HttpMethod.Post, "*certfnsh.asp")
            .Respond("text/html", responseWithoutCert);

        // Act
        Func<Task> act = async () => await sut.SubmitCsrAndRetrieveCertificateAsync(fakeCsr, "SomeTemplate");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Certificate not found in response*");
    }

    // SubmitCSR should throw when CSR is invalid
    [Fact]
    public async Task SubmitCSR_Throws_When_CSR_Is_Invalid()
    {
        // Arrange
        var (_, mock) = HttpClientBuilder.Build();
        var config = ConfigurationStub.Build();
        var sut = BuildSut(mock, config);

        string invalidCsr = "INVALID_CSR_CONTENT";

        // Act
        Func<Task> act = async () => await sut.SubmitCsrAndRetrieveCertificateAsync(invalidCsr, "InvalidTemplate");

        // Assert
        await act.Should().ThrowAsync<Exception>(); // Replace with specific exception if known
    }

    // SubmitCSR should submit the CSR and validate the response without extracting cert
  /*  [Fact]
    public async Task SubmitCSR_Only_Submits_Request_And_Validates_Response()
    {
        // Arrange
        var (client, mock) = HttpClientBuilder.Build();
        var config = ConfigurationStub.Build();

        var sut = new CertSrvService(new NullLogger<CertSrvService>(), config);

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
-----END CERTIFICATE REQUEST-----";

        // Simulate a successful response without extracting cert
        var fakeHtml = "<html><body><p>Certificate request received successfully</p></body></html>";
        mock.When(HttpMethod.Post, "*certfnsh.asp")
            .Respond("text/html", fakeHtml);

        // Use reflection to bypass ExtractCertificateFromResponse (or you can refactor SubmitCsrAndRetrieveCertificateAsync to split submission & extraction)
        var response = await mock.ToHttpClient().PostAsync("https://mbsdc.mbs.com/certsrv/certfnsh.asp",
            new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("Mode", "newreq"),
            new KeyValuePair<string, string>("CertRequest", csrPem),
            new KeyValuePair<string, string>("CertAttrib", "CertificateTemplate:EsperAutoWebServerCert"),
            new KeyValuePair<string, string>("TargetStoreFlags", "0"),
            new KeyValuePair<string, string>("SaveCert", "yes"),
            new KeyValuePair<string, string>("ThumbPrint", "")
            }));

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Certificate request received successfully", content);
    }

    // Invalid CSR should throw an exception
    [Fact]
    public async Task SubmitCsr_WithInvalidCsr_ThrowsException()
    {
        // Arrange
        var config = ConfigurationStub.Build();
        var sut = new CertSrvService(new NullLogger<CertSrvService>(), config);

        string invalidCsr = "INVALID_CSR_DATA";

        // Act
        Func<Task> act = async () => await sut.SubmitCsrAndRetrieveCertificateAsync(invalidCsr, "EsperAutoWebServerCert");

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.WithMessage("*Certificate not found in response*");
    }

    // Valid CSR but no Request ID in response
    [Fact]
    public async Task SubmitCsr_WithValidCsr_ButNoRequestId_ThrowsException()
    {
        // Arrange
        var (client, mock) = HttpClientBuilder.Build();
        var config = ConfigurationStub.Build();
        var sut = new CertSrvService(new NullLogger<CertSrvService>(), config);

        string validCsr = @"
-----BEGIN CERTIFICATE REQUEST-----
MIICdDCCAVwCAQAwLzEtMCsGA1UEAwwkZWVmNjM5ZjEtODE5ZC00M2ZiLTlmNGEt
YWIwNDk4NTk3MDIxMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4dxk
...TRIMMED...
-----END CERTIFICATE REQUEST-----";

        // Simulate HTML response missing ReqID
        mock.When(HttpMethod.Post, "*certfnsh.asp")
            .Respond("text/html", "<html><body>The certificate you requested was issued</body></html>");

        // Act
        Func<Task> act = async () => await sut.SubmitCsrAndRetrieveCertificateAsync(validCsr, "EsperAutoWebServerCert");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Certificate not found in response*");
    }

    // Missing template name should throw on submission
    [Fact]
    public async Task SubmitCsr_WithEmptyTemplateName_ThrowsException()
    {
        var config = ConfigurationStub.Build();
        var sut = new CertSrvService(new NullLogger<CertSrvService>(), config);

        string dummyCsr = "-----BEGIN CERTIFICATE REQUEST-----\n...FakeData...\n-----END CERTIFICATE REQUEST-----";

        Func<Task> act = async () => await sut.SubmitCsrAndRetrieveCertificateAsync(dummyCsr, "");

        await act.Should().ThrowAsync<System.InvalidOperationException>();
    }

    // Response indicates Pending status or Certificate not found
    [Fact]
    public async Task SubmitCsr_ResponseIndicatesPending_ThrowsPendingException()
    {
        var (client, mock) = HttpClientBuilder.Build();
        var config = ConfigurationStub.Build();
        var sut = new CertSrvService(new NullLogger<CertSrvService>(), config);

        mock.When(HttpMethod.Post, "*certfnsh.asp")
            .Respond("text/html", "<html><body>Status: Pending</body></html>");

        Func<Task> act = async () => await sut.SubmitCsrAndRetrieveCertificateAsync("dummyCsr", "EsperAutoWebServerCert");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Certificate not found in response*");
    }

    // Simulate network failure
    [Fact]
    public async Task SubmitCsr_HttpServerError_ThrowsException()
    {
        var (client, mock) = HttpClientBuilder.Build();
        var config = ConfigurationStub.Build();
        var sut = new CertSrvService(new NullLogger<CertSrvService>(), config);

        mock.When(HttpMethod.Post, "*certfnsh.asp")
            .Respond(HttpStatusCode.InternalServerError, "text/plain", "Internal Error");

        Func<Task> act = async () => await sut.SubmitCsrAndRetrieveCertificateAsync("dummyCsr", "EsperAutoWebServerCert");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Certificate not found in response*");
    }

    // Malformed HTML that doesn't contain anything useful
    [Fact]
    public async Task SubmitCsr_ResponseMalformedHtml_ThrowsException()
    {
        var (client, mock) = HttpClientBuilder.Build();
        var config = ConfigurationStub.Build();
        var sut = new CertSrvService(new NullLogger<CertSrvService>(), config);

        mock.When(HttpMethod.Post, "*certfnsh.asp")
            .Respond("text/html", "<html><body>Unknown response</body></html>");

        Func<Task> act = async () => await sut.SubmitCsrAndRetrieveCertificateAsync("dummyCsr", "EsperAutoWebServerCert");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Certificate not found in response*");
    }

    // Access Denied (Authentication Failed)
    /*
    [Fact]
    public async Task SubmitCsr_WhenAccessDenied_ThrowsUnauthorizedException()
    {
        // Arrange
        var (_, mockHttp) = HttpClientBuilder.Build();
        var config = ConfigurationStub.Build();
        var sut = new CertSrvService(NullLogger<CertSrvService>.Instance, config);

        var invalidCsr = @"
-----BEGIN CERTIFICATE REQUEST-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAzXzZz2WDcKcP+H1STKdE
...
-----END CERTIFICATE REQUEST-----".Trim();

        // Simulate a 401 Unauthorized response
        mockHttp.When(HttpMethod.Post, "*certfnsh.asp")
                .Respond(HttpStatusCode.Unauthorized, "text/html", "Access Denied");

        // Act
        Func<Task> act = async () =>
            await sut.SubmitCsrAndRetrieveCertificateAsync(invalidCsr, "EsperAutoWebServerCert");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Certificate not found in response*");
    }  */
}

