// ============================================================================
// CertSrvService.cs - Microsoft Certificate Services Integration (Updated)
// ============================================================================
using EsperCertProject.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EsperCertProject.Services
{
    public class CertSrvService : ICertSrvService, IDisposable
    {
        private readonly ILogger<CertSrvService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        private readonly string _certSrvBaseUrl;
        private readonly string _caRequesterUsername;
        private readonly string _caRequesterPassword;
        private readonly string _caRequesterDomain;

        public CertSrvService(ILogger<CertSrvService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // === Read from app settings directly ===
            _certSrvBaseUrl = _configuration["CertSrv:BaseUrl"] ?? throw new InvalidOperationException("CertSrv:BaseUrl is not configured.");
            _caRequesterUsername = _configuration["CertSrv:CaRequesterUsername"] ?? throw new InvalidOperationException("CertSrv:CaRequesterUsername is not configured.");
            _caRequesterPassword = _configuration["CertSrv:CaRequesterPassword"] ?? throw new InvalidOperationException("CertSrv:CaRequesterPassword is not configured.");
            _caRequesterDomain = _configuration["CertSrv:CaRequesterDomain"] ?? ""; // Optional

          
            _httpClient = CreateHttpClientWithWindowsAuth();

            _logger.LogInformation("_certSrvBaseUrl is..from kavi iconfig..{Url}", _certSrvBaseUrl);
            _logger.LogInformation("__caRequesterUsername:" + _caRequesterUsername);
            _logger.LogInformation("__caRequesterPassword:" + _caRequesterPassword);
        }

        private HttpClient CreateHttpClientWithWindowsAuth()
        {
            //use this for production
            //var handler = new HttpClientHandler();
            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential("_caRequesterUsername", "_caRequesterPassword", ""),
                PreAuthenticate = true,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };


            if (!string.IsNullOrEmpty(_caRequesterUsername) && !string.IsNullOrEmpty(_caRequesterPassword))
            {
                handler.Credentials = new NetworkCredential(_caRequesterUsername, _caRequesterPassword, _caRequesterDomain);
                handler.PreAuthenticate = true;
                _logger.LogInformation("HttpClient configured for Windows Authentication with explicit credentials.");
            }
            else
            {
                handler.UseDefaultCredentials = true;
                handler.PreAuthenticate = true;
                _logger.LogInformation("HttpClient configured for Windows Authentication with default credentials.");
            }

            return new HttpClient(handler);
        }

        public async Task<X509Certificate2> SubmitCsrAndRetrieveCertificateAsync(string csrPem, string templateName)
        {
            try
            {
                _logger.LogInformation("Submitting CSR to Microsoft Certificate Services");

                var cleanCsr = csrPem
                    .Replace("-----BEGIN CERTIFICATE REQUEST-----", "")
                    .Replace("-----END CERTIFICATE REQUEST-----", "")
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Replace(" ", "");
                var handler = new HttpClientHandler
                {
                    Credentials = new NetworkCredential("_caRequesterUsername", "_caRequesterPassword", ""),
                    PreAuthenticate = true,
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);
                string certAttribValue = $"CertificateTemplate:{templateName}";
                _logger.LogInformation("CertAttrib value: {CertAttrib}", certAttribValue);

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("Mode", "newreq"),
                    new("CertRequest", cleanCsr),
                    new("CertAttrib", certAttribValue),
                    new("TargetStoreFlags", "0"),
                    new("SaveCert", "yes"),
                    new("ThumbPrint", "")
                };

                foreach (var item in formData)
                    _logger.LogInformation("formData: {Key} = {Value}", item.Key, item.Value);

                var formContent = new FormUrlEncodedContent(formData);
                var baseUrl = _certSrvBaseUrl.TrimEnd('/');
                var submitUrl = $"{baseUrl}/certfnsh.asp";
                _logger.LogInformation("_certSrvBaseUrl is..SubmitCsrAndRetrieveCertificateAsync..{Url}", _certSrvBaseUrl);

                _logger.LogInformation("Submitting to URL: {SubmitUrl}", submitUrl);
                _logger.LogInformation("Using template: {Template}", templateName);

                var response = await _httpClient.PostAsync(submitUrl, formContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Certificate request failed: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);
                    _logger.LogError("Response:\n{ResponseContent}", responseContent);
                    throw new InvalidOperationException($"Certificate request failed: {response.StatusCode} - {response.ReasonPhrase}");
                }

                _logger.LogInformation("Certificate request submitted. Processing response...");
                return await ExtractCertificateFromResponse(responseContent, templateName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting CSR to Certificate Services");
                throw;
            }
        }

        private async Task<X509Certificate2> ExtractCertificateFromResponse(string htmlResponse, string templateName)
        {
            try
            {
                _logger.LogDebug("Received HTML response (length: {Length})", htmlResponse.Length);
                _logger.LogDebug("Full HTML response:\n{Html}", htmlResponse);

                if (htmlResponse.Contains("The certificate you requested was issued", StringComparison.OrdinalIgnoreCase))
                {
                    var reqIdMatch = Regex.Match(htmlResponse, @"ReqID=(\d+)", RegexOptions.IgnoreCase);
                    if (reqIdMatch.Success)
                    {
                        var reqId = reqIdMatch.Groups[1].Value;
                        _logger.LogInformation("Certificate was issued. Extracted Request ID: {ReqId}", reqId);
                        return await RetrievePendingCertificate(reqId);
                    }

                    _logger.LogError("Certificate was issued, but Request ID could not be extracted.");
                    throw new InvalidOperationException("Failed to extract request ID from response");
                }

                if (htmlResponse.Contains("pending", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Certificate request is pending manual approval.");
                    throw new InvalidOperationException("Certificate request is pending manual approval");
                }

                _logger.LogError("Unexpected response format. Certificate not issued or request ID not found.");
                throw new InvalidOperationException("Certificate not found in response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting or retrieving certificate from response");
                throw;
            }
        }

        public async Task<X509Certificate2> RetrievePendingCertificate(string requestId)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve pending certificate for request ID: {RequestId}", requestId);

                var retrieveUrl = $"{_certSrvBaseUrl.TrimEnd('/')}/certnew.cer?ReqID={requestId}&Enc=b64";
                var response = await _httpClient.GetAsync(retrieveUrl);

                if (response.IsSuccessStatusCode)
                {
                    var certContent = await response.Content.ReadAsStringAsync();

                    if (certContent.Contains("-----BEGIN CERTIFICATE-----"))
                    {
                        var certBytes = Convert.FromBase64String(
                            certContent.Replace("-----BEGIN CERTIFICATE-----", "")
                                       .Replace("-----END CERTIFICATE-----", "")
                                       .Replace("\r", "")
                                       .Replace("\n", ""));

                        _logger.LogInformation("Successfully retrieved certificate for request ID: {RequestId}", requestId);
                        return new X509Certificate2(certBytes);
                    }
                }

                _logger.LogWarning("Certificate with request ID {RequestId} is not yet available. Status: {Status}", requestId, response.StatusCode);
                throw new InvalidOperationException($"Certificate with request ID {requestId} is not yet available or could not be retrieved. Status: {response.StatusCode} ({response.ReasonPhrase})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending certificate for request ID: {RequestId}", requestId);
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
