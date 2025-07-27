using EsperCertProject.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace EsperCertProject.Services
{
    public class CertificateService
    {
        private readonly ILogger<CertificateService> _logger;

        public CertificateService(ILogger<CertificateService> logger)
        {
            _logger = logger;
        }

        public GeneratedCsrResult GenerateCsrAndPrivateKey(string deviceId)
        {
            _logger.LogInformation($"Generating private key and CSR for device: {deviceId}");

            using var rsa = RSA.Create(2048);
            var subjectName = new X500DistinguishedName($"CN={deviceId}, OU=Devices, O=Kyndryl, L=Fremont, S=CA, C=US");
            var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Add certificate extensions
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, false));

            var csrPem = $"-----BEGIN CERTIFICATE REQUEST-----\n{Convert.ToBase64String(request.CreateSigningRequest(), Base64FormattingOptions.InsertLineBreaks)}\n-----END CERTIFICATE REQUEST-----";

            // Create a new RSA instance for return (the using statement will dispose the original)
            var returnRsa = RSA.Create(2048);
            returnRsa.ImportParameters(rsa.ExportParameters(true));

            return new GeneratedCsrResult
            {
                CsrPem = csrPem,
                PrivateKey = returnRsa,
                DeviceId = deviceId
            };
        }

        public byte[] CombineCertificateAndPrivateKey(X509Certificate2 issuedCert, RSA privateKey, string pfxPassword = "whatever", string friendlyName = "Enterprise Client Certificate", string? caCertificatePem = null)
        {
            using var certWithKey = issuedCert.HasPrivateKey ? issuedCert : issuedCert.CopyWithPrivateKey(privateKey);
            certWithKey.FriendlyName = friendlyName;

            var certCollection = new X509Certificate2Collection(certWithKey);

            if (!string.IsNullOrWhiteSpace(caCertificatePem))
            {
                var caCertBytes = Convert.FromBase64String(
                    caCertificatePem.Replace("-----BEGIN CERTIFICATE-----", "")
                                   .Replace("-----END CERTIFICATE-----", "")
                                   .Replace("\n", "")
                                   .Replace("\r", ""));
                certCollection.Add(new X509Certificate2(caCertBytes));
            }

            return certCollection.Export(X509ContentType.Pkcs12, pfxPassword);
        }

        public GeneratedCertificateResult GeneratePfxSelfSignedCertificate(string deviceId, string? pfxPassword = null)
        {
            _logger.LogInformation($"Generating self-signed PFX certificate for device: {deviceId}");

            try
            {
                using var rsa = RSA.Create(2048);
                string subjectName = $"CN={deviceId}, OU=Devices, O=Kyndryl, L=Fremont, S=CA, C=US";
                var certRequest = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                certRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
                certRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, false));
                certRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));

                DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
                DateTimeOffset notAfter = notBefore.AddYears(1);

                using var certificate = certRequest.CreateSelfSigned(notBefore, notAfter);
                byte[] pfxBytes = string.IsNullOrEmpty(pfxPassword) ?
                                  certificate.Export(X509ContentType.Pkcs12) :
                                  certificate.Export(X509ContentType.Pkcs12, pfxPassword);

                _logger.LogInformation($"Successfully generated PFX certificate for device: {deviceId}");

                return new GeneratedCertificateResult
                {
                    PfxBytes = pfxBytes,
                    Thumbprint = certificate.Thumbprint,
                    CommonName = certificate.GetNameInfo(X509NameType.SimpleName, false),
                    CreatedDate = certificate.NotBefore,
                    ExpiryDate = certificate.NotAfter
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating PFX certificate for device {deviceId}");
                throw;
            }
        }
    }
}