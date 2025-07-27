using RichardSzalay.MockHttp;
using System.Net.Http;

namespace EsperCertProject.Tests.Utils;

internal static class HttpClientBuilder
{
    /// <summary>
    /// Returns an HttpClient and the underlying MockHttpMessageHandler so
    /// the test can assert on calls or register additional expectations.
    /// </summary>
    public static (HttpClient client, MockHttpMessageHandler mock) Build()
    {
        var mock = new MockHttpMessageHandler();
        var client = mock.ToHttpClient();
        client.BaseAddress = new Uri("https://mbsdc.mbs.com/certsrv/");
        return (client, mock);
    }
}