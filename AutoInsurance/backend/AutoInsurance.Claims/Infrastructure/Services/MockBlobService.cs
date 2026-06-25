using AutoInsurance.Shared.Interfaces;

namespace AutoInsurance.Claims.Infrastructure.Services;

public class MockBlobService : IBlobService
{
    private readonly string _baseUrl;

    public MockBlobService(IConfiguration configuration)
    {
        _baseUrl = configuration["Blob:BaseUrl"] ?? "http://localhost:10000/devstoreaccount1";
    }

    public Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
        => Task.FromResult($"{_baseUrl}/{containerName}/{blobName}");

    public Task<string> GenerateSasUrlAsync(string containerName, string blobName, TimeSpan expiry, CancellationToken cancellationToken = default)
        => Task.FromResult($"{_baseUrl}/{containerName}/{blobName}?sv=mock&se={DateTime.UtcNow.Add(expiry):O}&sig=mocksig");
}
