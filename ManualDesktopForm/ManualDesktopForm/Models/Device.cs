using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ManualDesktopForm.Models
{
    [BsonIgnoreExtraElements]

    public class Device
    {

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("id")]
        public string DeviceId { get; set; } = string.Empty;

        [BsonElement("device_name")]
        public string? DeviceName { get; set; }


        [BsonElement("device_model")]
        public DeviceModelInfo? DeviceModel { get; set; } // Now matches the object in MongoDB

        [BsonElement("status")]
        public int Status { get; set; }

        [BsonElement("serialNumber")]
        public string? SerialNumber { get; set; }

        [BsonElement("enrolled_on")]
        public DateTime? EnrolledOn { get; set; }

        [BsonElement("updated_on")]
        public DateTime? UpdatedOn { get; set; }

        [BsonElement("certificate")]
        public CertificateStoreInfo? Certificate { get; set; }

        [BsonElement("lastRenewedOn")]
        public DateTime? LastRenewedOn { get; set; }

        public DateTime? CertificateExpiryDate => Certificate?.ExpiryDate;
        public string? Thumbprint => Certificate?.Thumbprint;

        [BsonElement("processingStatus")]
        public string? ProcessingStatus { get; set; }

        [BsonElement("lastProcessingMessage")]
        public string? LastProcessingMessage { get; set; }

        [BsonElement("lastProcessedOn")]
        public DateTime? LastProcessedOn { get; set; }
    }
    [BsonIgnoreExtraElements]
    public class CertificateStoreInfo
    {
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
        public string? EsperContentId { get; set; }

        [BsonElement("esperContentName")]
        public string? EsperContentName { get; set; }

        [BsonElement("deviceFilePath")]
        public string DeviceFilePath { get; set; } = string.Empty;

        [BsonElement("lastConfigured")]
        public DateTime LastConfigured { get; set; }

        [BsonElement("base64Raw")]
        public string? Base64Raw { get; set; }

        [BsonElement("rawBytes")]
        public byte[]? RawBytes { get; set; }
    }
    [BsonIgnoreExtraElements]
    public class DeviceModelInfo
    {
        // You can extend this based on the actual structure in MongoDB
        [BsonElement("name")]
        public string? Name { get; set; }

        // Add more fields if your device_model includes them, like type, manufacturer, etc.
    }
}
