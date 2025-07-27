using EsperCertProject.Models; // Ensure this namespace contains IssueCertificateRequest

// Add this class definition if IssueCertificateRequest is missing in your codebase
namespace EsperCertProject.Models
{
    public class IssueCertificateRequest
    {
        public string CsrPem { get; set; }
        public string TemplateName { get; set; }
        public string? RequesterUsername { get; set; }
        public string? RequesterPassword { get; set; }
    }
}