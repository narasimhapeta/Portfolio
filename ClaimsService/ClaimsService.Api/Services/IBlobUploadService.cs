namespace ClaimsService.Api.Services;

public interface IBlobUploadService
{
    Task<(string SasUrl, string BlobPath)> GenerateSasUploadUrlAsync(string claimId, string fileName);
}
