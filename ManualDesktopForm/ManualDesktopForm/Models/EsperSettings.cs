namespace ManualDesktopForm.Models
{
    public class EsperSettings
    {
        public string BaseUrl { get; set; }                 // e.g. "https://kyndryl-api.esper.cloud/api/v0/"
        public string EnterpriseId { get; set; }            // Esper enterprise UUID
        public string ApiKey { get; set; }                  // Bearer token (API Key)

        public bool UseSelfSigned { get; set; } = false;    // Cert mode toggle

        // Wi-Fi configuration for Esper device provisioning
        public string WifiSsid { get; set; }
        public string WifiIdentity { get; set; }
        public string WifiDomain { get; set; }
        public string CertificatePassword { get; set; }

        // Optional: Remove this if ApiKey is always used
        // public string BearerToken { get; set; }          
    }
}
