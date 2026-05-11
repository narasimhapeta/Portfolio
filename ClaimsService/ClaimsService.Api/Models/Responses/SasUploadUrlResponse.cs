namespace ClaimsService.Api.Models.Responses;

public record SasUploadUrlResponse(string UploadUrl, string BlobPath, DateTime ExpiresAt);
