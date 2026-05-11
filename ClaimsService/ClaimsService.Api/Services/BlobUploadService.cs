using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace ClaimsService.Api.Services;

public class BlobUploadService : IBlobUploadService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public BlobUploadService(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = configuration["Azure:BlobStorage:ContainerName"]!;
    }

    public Task<(string SasUrl, string BlobPath)> GenerateSasUploadUrlAsync(string claimId, string fileName)
    {
        var blobPath = $"{claimId}/{fileName}";
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult((sasUri.ToString(), blobPath));
    }
}
