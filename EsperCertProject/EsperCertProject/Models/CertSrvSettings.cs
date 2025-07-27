namespace EsperCertProject.Models
{
    public class CertSrvSettings
    {
        public string ServerUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string BaseUrl { get; set; }
        public string CaRequesterUsername { get; set; }
        public string CaRequesterPassword { get; set; }
        public string CaRequesterDomain { get; set; }
    }
}