using System;

namespace ManualDesktopForm.Models
{
    public class GeneratedCertificateResult
    {
        public byte[] PfxBytes { get; set; } = Array.Empty<byte>();
        public string Thumbprint { get; set; } = string.Empty;
        public string CommonName { get; set; } = string.Empty; // New: Matches CertificateStoreInfo
        public DateTime CreatedDate { get; set; } // Renamed from NotBefore
        public DateTime ExpiryDate { get; set; } // Renamed from NotAfter
        // Removed: SerialNumber, Subject, Issuer as they are no longer in CertificateStoreInfo
    }
}