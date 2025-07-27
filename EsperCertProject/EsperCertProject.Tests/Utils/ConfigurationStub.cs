// EsperCertProject.Tests/Utils/ConfigurationStub.cs
using Microsoft.Extensions.Configuration;

namespace EsperCertProject.Tests.Utils;

internal static class ConfigurationStub
{
    public static IConfiguration Build() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CertSrv:BaseUrl"] = "https://mbsdc.mbs.com/certsrv",
                ["CertSrv:CaRequesterUsername"] = "kavithap@mbs.com",
                ["CertSrv:CaRequesterPassword"] = "g$N6wM$yfurtive",
                ["CertSrv:CaRequesterDomain"] = "mbs.com"
            })
            .Build();
}
