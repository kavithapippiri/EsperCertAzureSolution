// File: EsperCertProject/Models/CertificateStoreInfo.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace EsperCertProject.Models
{

    public class CertificateStoreInfo
    {
        // No [BsonId] for embedded documents
        [BsonElement("thumbprint")]
        public string Thumbprint { get; set; } = string.Empty;

        [BsonElement("commonName")]
        public string CommonName { get; set; } = string.Empty;
        [BsonElement("createdDate")]
        public DateTime CreatedDate { get; set; }

        [BsonElement("expiryDate")]
        public DateTime ExpiryDate { get; set; }

        [BsonElement("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        [BsonElement("esperContentId")]
        public string? EsperContentId { get; set; } // Nullable long, as it might not be set initially

        [BsonElement("esperContentName")]
        public string? EsperContentName { get; set; } // Nullable string

        public string DeviceFilePath { get; set; } = string.Empty; // Full path on the device
        public DateTime LastConfigured { get; set; } // When the certificate was last configured
        public string Base64Raw { get; set; }
        public byte[] RawBytes { get; set; }
    }
}