using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManualDesktopForm.Models
{
    public class EsperDeviceApiResponse
    {
        public List<EsperDevice> results { get; set; }
    }

    public class EsperDevice
    {
        public string id { get; set; }
        public string device_name { get; set; }
        public string serial_number { get; set; }
        public string device_model_name { get; set; }
        public DateTime created { get; set; }
        public string status { get; set; }
        public DateTime modified { get; set; }
    }
}
