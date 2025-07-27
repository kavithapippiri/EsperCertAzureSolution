using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace EsperCertProject.Utilities
{
    public static class CertificateHelper
    {
        public static byte[] GenerateCertificate(string subject, out string thumbprint)
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
            thumbprint = cert.Thumbprint;

            return cert.Export(X509ContentType.Pfx, "password");
        }
    }
}
