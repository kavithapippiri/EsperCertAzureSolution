using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace EsperCertProject.Models
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

        [BsonElement("processingStatus")]
        public string ProcessingStatus { get; set; } = string.Empty;

        [BsonElement("lastProcessingMessage")]
        public string LastProcessingMessage { get; set; } = string.Empty;

        [BsonElement("lastProcessedOn")]
        public DateTime? LastProcessedOn { get; set; }
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
