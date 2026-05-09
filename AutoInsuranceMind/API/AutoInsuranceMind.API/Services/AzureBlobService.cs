using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AutoInsuranceMind.API.Services;

public class AzureBlobService
{
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<AzureBlobService> _logger;
    public readonly bool IsConfigured;

    public AzureBlobService(ILogger<AzureBlobService> logger, IConfiguration config)
    {
        _logger = logger;
        _containerName = config["AzureBlob:ContainerName"] ?? "policy-documents";

        var connectionString = config["AzureBlob:ConnectionString"] ?? string.Empty;
        IsConfigured = !string.IsNullOrWhiteSpace(connectionString)
                       && !connectionString.StartsWith("YOUR_");

        if (IsConfigured)
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
            _logger.LogInformation("Azure Blob Storage configured. Container: {Container}", _containerName);
        }
    }

    public async Task EnsureContainerExistsAsync()
    {
        if (!IsConfigured) return;
        var container = _blobServiceClient!.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None);
    }

    public async Task<string> UploadAsync(Stream fileStream, string blobName, string contentType)
    {
        var container = _blobServiceClient!.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(blobName);

        await blob.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType });
        _logger.LogInformation("Uploaded blob: {BlobName}", blobName);
        return blob.Uri.ToString();
    }

    public async Task DeleteAsync(string blobName)
    {
        var container = _blobServiceClient!.GetBlobContainerClient(_containerName);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync();
    }
}
