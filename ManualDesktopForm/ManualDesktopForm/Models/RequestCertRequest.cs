namespace ManualDesktopForm.Models
{
    public class RequestCertRequest
    {
        public List<string> deviceIds { get; set; }
        public bool useSelfSigned { get; set; } = true;
        //public string PfxPassword { get; set; }
        //public string EsperGroup { get; set; }
        //public string CertAlias { get; set; }
    }
}
