using System;
using Newtonsoft.Json.Linq; // Correct using for JObject
using EsperCertProject.Models; // For Device and CertificateStoreInfo

namespace EsperCertProject.Models
{
    public static class EsperMapper
    {
        /// <summary>
        /// Maps a raw JObject representing an Esper device API response to your MongoDB Device model.
        /// This method is primarily for initial device enrollment or syncing basic device metadata.
        /// Certificate details are typically populated by the GenerateAndUploadCert function.
        /// </summary>
        /// <param name="deviceObj">The JObject containing the Esper device data.</param>
        /// <returns>A new Device object ready for MongoDB storage.</returns>
        public static Device FromEsperDevice(JObject deviceObj)
        {
            // Parse provisioned_on to EnrolledOn (nullable DateTime)
            DateTime? enrolledOn = null;
            if (DateTime.TryParse(deviceObj["provisioned_on"]?.ToString(), out var parsedEnrolledDt))
            {
                enrolledOn = parsedEnrolledDt;
            }

            // Parse updated_on to UpdatedOn (nullable DateTime)
            DateTime? updatedOn = null;
            if (DateTime.TryParse(deviceObj["updated_on"]?.ToString(), out var parsedUpdatedDt))
            {
                updatedOn = parsedUpdatedDt;
            }

            return new Device
            {
                // Basic device properties mapping directly from the Esper JObject
                // Using null-conditional operator '?.' and null-coalescing operator '??' for safety
                DeviceId = deviceObj["id"]?.ToString() ?? string.Empty, // Esper's 'id' field
                DeviceName = deviceObj["device_name"]?.ToString(),      // Assuming 'device_name' from Esper API
                                                                        // DeviceModel = deviceObj["hardwareInfo"]?["model"]?.ToString() ?? "UnknownModel", // Get from hardwareInfo
                DeviceModel = new DeviceModelInfo { Name = deviceObj["hardwareInfo"]?["model"]?.ToString() ?? "UnknownModel" }, // Get from hardwareInfo                Status = (int?)deviceObj["status"] ?? 0, // Esper's 'status' field, cast to int
                SerialNumber = deviceObj["hardwareInfo"]?["serial"]?.ToString(), // Assuming 'serial' from hardwareInfo

                // Map to the new 'EnrolledOn' property
                EnrolledOn = enrolledOn,
                UpdatedOn = updatedOn,

                // IMPORTANT: The 'Certificate' sub-document should be null here.
                // It is populated by the GenerateAndUploadCert function AFTER a certificate is generated.
                Certificate = null // Ensure this is null on initial mapping.
                                   // The 'GenerateAndUploadCert' function will create and populate it.
            };
        }

        // You might consider adding other mapping methods here if needed,
        // for instance, a method to update specific fields on an existing Device object.
    }
}