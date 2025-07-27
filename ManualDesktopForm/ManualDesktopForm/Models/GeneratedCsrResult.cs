using System.Security.Cryptography; // Required for RSA

namespace ManualDesktopForm.Models
{
    /// <summary>
    /// Represents the result of a CSR and private key generation operation.
    /// </summary>
    public class GeneratedCsrResult
    {
        /// <summary>
        /// Gets or sets the Certificate Signing Request (CSR) in PEM format.
        /// </summary>
        public string CsrPem { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the generated private key as an RSA object.
        /// This key is held in memory and will be used to combine with the issued certificate.
        /// </summary>
        public RSA? PrivateKey { get; set; }

        /// <summary>
        /// Gets or sets the Device ID for which the CSR was generated.
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;
    }
}