using System.Net.Http.Headers;
using System.Net.Http.Json;
using FairwayFinder.Core.HttpClients.UploadThing.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.HttpClients.UploadThing
{
    public class UploadThingHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UploadThingHttpClient> _logger;

        public UploadThingHttpClient(HttpClient httpClient, IConfiguration configuration, ILogger<UploadThingHttpClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("https://api.uploadthing.com/v7/");
            
            var api_key = configuration["UploadThing:ApiKey"];
            if (string.IsNullOrWhiteSpace(api_key))
            {
                throw new InvalidOperationException("UploadThing API key is missing in configuration.");
            }

            _httpClient.DefaultRequestHeaders.Add("x-uploadthing-api-key", api_key);
        }

        public async Task<bool> Upload(Stream imageStream, string fileName, string fileType)
        {
            var custom_id = Guid.NewGuid().ToString();
            var request = new UploadThingRequest
            {
                FileName = fileName,
                FileSize = imageStream.Length,
                FileType = fileType,
                Slug = "profile",
                CustomId = custom_id,
                Acl = "public-read",
                ExpiresIn = 3600,
                ContentDisposition = "inline"
            };
            
            // Get the presigned URL
            var presigned_url_response = await _httpClient.PostAsJsonAsync("prepareUpload", request);

            if (!presigned_url_response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to get presigned URL: {await presigned_url_response.Content.ReadAsStringAsync()}");
                return false;
            }

            var presigned_url = await presigned_url_response.Content.ReadFromJsonAsync<UploadThingPresignedUrlResponse>();

            if (string.IsNullOrEmpty(presigned_url.Url))
            {
                _logger.LogError("Presigned URL came back null.");
                return false;
            }

            var metadata_set = await SetMetadataBeforeUpload(presigned_url);

            if (!metadata_set)
            {
                return false;
            }
            
            // Upload the file using the presigned URL
            return await UploadFileToPresignedUrl(imageStream, fileName, presigned_url.Url);
        }

        private async Task<bool> SetMetadataBeforeUpload(UploadThingPresignedUrlResponse presignedUrl)
        {
            var ingest_uri = new Uri(presignedUrl.Url);
            var host_segments = ingest_uri.Host.Split('.');
            var region_alias = host_segments[0]; 

            // Now build the route-metadata endpoint for that region
            var metadata_endpoint = $"https://{region_alias}.ingest.uploadthing.com/route-metadata";

            var metadata_request = new
            {
                fileKeys = new[] { presignedUrl.Key },
                metadata = new { },
                callbackUrl = "",
                callbackSlug = "profile",
                awaitServerData = false,
                isDev = false
            };

            var register_response = await _httpClient.PostAsJsonAsync(metadata_endpoint, metadata_request);

            if (register_response.IsSuccessStatusCode)
            {
                return true;
            }

            var response_body = await register_response.Content.ReadAsStringAsync();
            _logger.LogError("UploadThing rejected it: {Body}", response_body);
            return false;
        }

        private async Task<bool> UploadFileToPresignedUrl(Stream imageStream, string fileName, string presignedUrl)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                
                imageStream.Position = 0;
                
                var file_content = new StreamContent(imageStream);
                file_content.Headers.ContentType = new MediaTypeHeaderValue("application/json");  
                form.Add(file_content, "file", fileName);
                
                var response = await _httpClient.PutAsync(presignedUrl, form);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                
                var response_body = await response.Content.ReadAsStringAsync();
                _logger.LogError("UploadThing rejected it: {Body}", response_body);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                _logger.LogError($"Exception in UploadFileToPresignedUrl: {ex}");
                return false;
            }
        }
    }
}
