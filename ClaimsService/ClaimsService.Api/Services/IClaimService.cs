// ClaimsService.Api/Services/IClaimService.cs
using ClaimsService.Api.Models.Requests;
using ClaimsService.Core.Models;

namespace ClaimsService.Api.Services;

public interface IClaimService
{
    Task<Claim> CreateFnolAsync(string customerId, FnolRequest request);
    Task<Claim?> GetClaimAsync(string id, string customerId, bool isAdmin);
    Task<IEnumerable<Claim>> GetAllClaimsAsync(string? status);
    Task<(string SasUrl, string BlobPath, DateTime ExpiresAt)> GeneratePhotoUploadUrlAsync(
        string claimId, string customerId, string fileName);
    Task<Claim> AssignAdjusterAsync(string claimId, string adjusterId);
    Task<Claim> UpdateStatusAsync(string claimId, string newStatus);
}
