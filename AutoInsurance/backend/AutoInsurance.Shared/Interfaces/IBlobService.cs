namespace AutoInsurance.Shared.Interfaces;

public interface IBlobService
{
    Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<string> GenerateSasUrlAsync(string containerName, string blobName, TimeSpan expiry, CancellationToken cancellationToken = default);
}
