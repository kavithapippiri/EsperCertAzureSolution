using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManualDesktopForm.Models
{
    public class DeviceListResponse
    {
        public int count { get; set; }
        public string? next { get; set; }
        public string? previous { get; set; }
        public List<DeviceDto> results { get; set; } = new();
    }
    public class DeviceDto
    {
        public string id { get; set; } = "";
        public string? device_name { get; set; }
        public string? device_model { get; set; }
        public int status { get; set; }
        public HardwareInfoDto? hardwareInfo { get; set; }
        public DateTime created_on { get; set; }
        public DateTime updated_on { get; set; }
    }
    public class HardwareInfoDto
    {
        public string? serialNumber { get; set; }
    }

}
