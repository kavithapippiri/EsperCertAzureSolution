using System;
using System.IO;
using System.Net;
using System.Net.Http; // Added for HttpRequestException
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using EsperCertProject.Services;
using EsperCertProject.Models; // Make sure this is the correct namespace for EsperSettings
using Microsoft.Extensions.Options; // Required for IOptions

namespace EsperCertProject.Functions
{
    public class UploadCertificateFunction
    {
        private readonly EsperContentService _esperContentService; // Inject EsperContentService
        private readonly ILogger<UploadCertificateFunction> _logger;
        private readonly EsperSettings _esperSettings; // To access any settings here if needed

        /// <summary>
        /// Constructor for the UploadCertificateFunction, injecting required services.
        /// </summary>
        /// <param name="esperContentService">The service responsible for interacting with the Esper Content API.</param>
        /// <param name="logger">The logger for this function.</param>
        /// <param name="esperSettings">The Esper configuration settings, provided via IOptions.</param>
        public UploadCertificateFunction(
            EsperContentService esperContentService,
            ILogger<UploadCertificateFunction> logger,
            IOptions<EsperSettings> esperSettings
        )
        {
            _esperContentService = esperContentService;
            _logger = logger;
            _esperSettings = esperSettings.Value; // Get the concrete settings object
        }

        /// <summary>
        /// HTTP triggered function to upload a PFX certificate to Esper.
        /// Expects a JSON body with a 'filePath' property pointing to the PFX file.
        /// </summary>
        /// <param name="req">The HTTP request data.</param>
        /// <param name="context">The function execution context.</param>
        /// <returns>An HTTP response indicating success or failure.</returns>
        [Function("UploadCertificate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "certificates/upload")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("UploadCertificate HTTP trigger function processed a request.");

            // Deserialize the request body to get the file path
            UploadRequest? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<UploadRequest>(req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Invalid JSON payload received.");
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync($"Invalid JSON format: {jsonEx.Message}");
                return badRequest;
            }


            // Validate the provided file path
            if (body?.FilePath == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Please provide a 'filePath' in the JSON request body.");
                return badRequest;
            }

            if (!File.Exists(body.FilePath))
            {
                // Important: In Azure, the function app runs on a server, not your local machine.
                // The 'filePath' must point to a file accessible by the function app (e.g., in temp storage, mounted share, or if reading from blob storage).
                _logger.LogError("File not found at specified path: {FilePath}", body.FilePath);
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"The file specified at '{body.FilePath}' was not found on the function's file system.");
                return notFound;
            }

            try
            {
                // Read the PFX file bytes from the local file system
                byte[] pfxBytes;
                try
                {
                    pfxBytes = await File.ReadAllBytesAsync(body.FilePath);
                    _logger.LogInformation("Successfully read {FileSize} bytes from file: {FilePath}", pfxBytes.Length, body.FilePath);
                }
                catch (IOException ioEx)
                {
                    _logger.LogError(ioEx, "Failed to read file {FilePath}. Check file permissions or if the file is locked.", body.FilePath);
                    var internalError = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await internalError.WriteStringAsync($"Failed to read file: {ioEx.Message}");
                    return internalError;
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    _logger.LogError(uaEx, "Unauthorized access when attempting to read file {FilePath}. Check permissions.", body.FilePath);
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync($"Unauthorized to access file: {uaEx.Message}");
                    return forbidden;
                }

                // Determine the name for the uploaded content. Using the file name without extension as a default.
                // You might want to get this from the request body if it needs to be different from the file name.
                var contentName = Path.GetFileNameWithoutExtension(body.FilePath);

                // Call the EsperContentService to upload the certificate
                _logger.LogInformation("Calling EsperContentService to upload certificate with name: {ContentName}", contentName);
                var (contentId, uploadedContentName) = await _esperContentService.UploadCertificateAsync(contentName, pfxBytes);

                // Prepare a successful response
                var okResponse = req.CreateResponse(HttpStatusCode.OK);
                await okResponse.WriteAsJsonAsync(new { ContentId = contentId, ContentName = uploadedContentName });
                _logger.LogInformation("Certificate uploaded successfully. ContentId: {ContentId}, ContentName: {ContentName}", contentId, uploadedContentName);
                return okResponse;
            }
            catch (HttpRequestException httpEx)
            {
                // This catches HTTP errors (like 400 Bad Request, 401 Unauthorized, etc.) from the Esper API.
                // The detailed error message (including Esper's response body) should already be logged by EsperContentService.
                _logger.LogError(httpEx, "Esper API request failed during certificate upload. Refer to EsperContentService logs for detailed response.");
                var errorResponse = req.CreateResponse(httpEx.StatusCode ?? HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Esper API error: {httpEx.Message}. Check logs for more details from Esper.");
                return errorResponse;
            }
            catch (Exception ex)
            {
                // Catch any other unexpected exceptions
                _logger.LogError(ex, "An unexpected error occurred during certificate upload process for file {FilePath}", body.FilePath);
                var internalError = req.CreateResponse(HttpStatusCode.InternalServerError);
                await internalError.WriteStringAsync($"An internal server error occurred: {ex.Message}");
                return internalError;
            }
        }

        /// <summary>
        /// Represents the expected JSON request body for this function.
        /// </summary>
        private class UploadRequest
        {
            public string? FilePath { get; set; }
        }
    }
}