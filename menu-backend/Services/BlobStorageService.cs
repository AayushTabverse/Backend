using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using menu_backend.Services.Interfaces;

namespace menu_backend.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;

    public BlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["BlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("BlobStorage:ConnectionString is not configured.");
        var containerName = configuration["BlobStorage:ContainerName"] ?? "images";

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        _containerClient.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string folder)
    {
        var uniqueName = $"{folder}/{Guid.NewGuid():N}_{fileName}";
        var blobClient = _containerClient.GetBlobClient(uniqueName);

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(fileStream, new BlobUploadOptions { HttpHeaders = headers });

        return blobClient.Uri.ToString();
    }
}
