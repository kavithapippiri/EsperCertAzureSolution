using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EsperCertProject.Models
{
    public class EsperContentListResponse
    {
        [JsonPropertyName("results")]
        public List<EsperContentResult> Results { get; set; } = new List<EsperContentResult>();

        [JsonPropertyName("count")]
        public int Count { get; set; }

        // [JsonPropertyName("next")]
        // public string? Next { get; set; }
        // [JsonPropertyName("previous")]
        // public string? Previous { get; set; }
    }
}

