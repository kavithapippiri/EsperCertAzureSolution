using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsperCertProject.Models
{
    public class EsperSettings
    {
        public const string Esper = "Esper"; // Used for section name in appsettings/local.settings

        public string BaseUrl { get; set; } = string.Empty;
        public string Tenant { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string EnterpriseId { get; set; } = string.Empty;

        public string WifiSsid { get; set; } = string.Empty;
        public string WifiIdentity { get; set; } = string.Empty;
        public string WifiDomain { get; set; } = string.Empty;
        public string CertificatePassword { get; set; } = string.Empty;
    }
}
