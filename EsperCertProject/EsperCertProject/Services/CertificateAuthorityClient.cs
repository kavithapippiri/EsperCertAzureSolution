using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates; // Ensure this is present
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EsperCertProject.Services
{
    public class CertificateAuthorityClient : ICertificateAuthorityClient
    {
        private readonly ILogger<CertificateAuthorityClient> _logger;

        public CertificateAuthorityClient(ILogger<CertificateAuthorityClient> logger)
        {
            _logger = logger;
        }

        public async Task<byte[]> GenerateCertificateAsync(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                _logger.LogError("[CertificateAuthorityClient] Device ID cannot be null or empty for certificate generation.");
                throw new ArgumentNullException(nameof(deviceId), "Device ID must be provided for certificate generation.");
            }

            _logger.LogInformation($"[CertificateAuthorityClient] Starting certificate generation for device: {deviceId}...");

            try
            {
                using (RSA rsa = RSA.Create(2048))
                {
                    var subjectName = new X500DistinguishedName($"CN={deviceId}");
                    var request = new CertificateRequest(
                        subjectName,
                        rsa,
                        HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pkcs1);

                    request.CertificateExtensions.Add(
                        new X509EnhancedKeyUsageExtension(
                            new OidCollection
                            {
                                new Oid("1.3.6.1.5.5.7.3.2") // Client Authentication OID
                            },
                            critical: false
                        ));

                    using (X509Certificate2 cert = request.CreateSelfSigned(
                        DateTimeOffset.UtcNow.AddMinutes(-5),
                        DateTimeOffset.UtcNow.AddYears(1)))
                    {
                        // *** CRITICAL CHANGE HERE: Remove the randomly generated password
                        // *** and export the PFX as password-less.
                        byte[] pfxBytes = cert.Export(X509ContentType.Pkcs12, ""); // Pass an empty string for no password

                        _logger.LogInformation($"[CertificateAuthorityClient] Generated {pfxBytes.Length} bytes PFX for device: {deviceId}.");
                        return pfxBytes;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[CertificateAuthorityClient] An error occurred during certificate generation for device {deviceId}: {ex.Message}");
                throw;
            }
        }
    }
}