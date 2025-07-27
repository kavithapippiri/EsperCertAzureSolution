using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace EsperCertProject.Services
{
    public interface ICertSrvService
    {
        Task<X509Certificate2> SubmitCsrAndRetrieveCertificateAsync(string csrPem, string templateName);
    }
}