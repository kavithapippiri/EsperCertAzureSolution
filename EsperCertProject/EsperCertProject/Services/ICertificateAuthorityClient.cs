using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
namespace EsperCertProject.Services
{
    public interface ICertificateAuthorityClient
    {
        /// <summary>
        /// Generates a PKCS#12 certificate (PFX) for the given device.
        /// </summary>
        /// <param name="deviceId">The unique device identifier.</param>
        /// <returns>Byte array containing the generated .p12 file.</returns>
        Task<byte[]> GenerateCertificateAsync(string deviceId);
    }
}
