// ClaimsService.Api/Services/ClaimService.cs
using ClaimsService.Api.Models.Requests;
using ClaimsService.Core.Models;
using ClaimsService.Core.Repositories;

namespace ClaimsService.Api.Services;

public class ClaimService : IClaimService
{
    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        ["FNOL"]        = new HashSet<string> { "UnderReview" },
        ["UnderReview"] = new HashSet<string> { "Approved", "Rejected" },
        ["Approved"]    = new HashSet<string> { "Paid" },
        ["Rejected"]    = new HashSet<string>(),
        ["Paid"]        = new HashSet<string>()
    };

    private readonly IClaimRepository _claimRepository;
    private readonly IAdjusterRepository _adjusterRepository;
    private readonly IBlobUploadService _blobUploadService;

    public ClaimService(
        IClaimRepository claimRepository,
        IAdjusterRepository adjusterRepository,
        IBlobUploadService blobUploadService)
    {
        _claimRepository = claimRepository;
        _adjusterRepository = adjusterRepository;
        _blobUploadService = blobUploadService;
    }

    public async Task<Claim> CreateFnolAsync(string customerId, FnolRequest request)
    {
        var claim = new Claim
        {
            CustomerId = customerId,
            PolicyNumber = request.PolicyNumber,
            IncidentDate = request.IncidentDate,
            IncidentDescription = request.IncidentDescription,
            Status = "FNOL"
        };
        return await _claimRepository.CreateAsync(claim);
    }

    public Task<Claim?> GetClaimAsync(string id, string customerId, bool isAdmin)
    {
        return isAdmin
            ? _claimRepository.GetByIdCrossPartitionAsync(id)
            : _claimRepository.GetByIdAsync(id, customerId);
    }

    public Task<IEnumerable<Claim>> GetAllClaimsAsync(string? status)
        => _claimRepository.GetAllAsync(status);

    public async Task<(string SasUrl, string BlobPath, DateTime ExpiresAt)> GeneratePhotoUploadUrlAsync(
        string claimId, string customerId, string fileName)
    {
        var claim = await _claimRepository.GetByIdAsync(claimId, customerId)
            ?? throw new KeyNotFoundException($"Claim {claimId} not found");

        var (sasUrl, blobPath) = await _blobUploadService.GenerateSasUploadUrlAsync(claimId, fileName);
        var expiresAt = DateTime.UtcNow.AddMinutes(5);

        claim.PhotosBlobPaths.Add(blobPath);
        await _claimRepository.UpdateAsync(claim);

        return (sasUrl, blobPath, expiresAt);
    }

    public async Task<Claim> AssignAdjusterAsync(string claimId, string adjusterId)
    {
        _ = await _adjusterRepository.GetByIdAsync(adjusterId)
            ?? throw new KeyNotFoundException($"Adjuster {adjusterId} not found");

        var claim = await _claimRepository.GetByIdCrossPartitionAsync(claimId)
            ?? throw new KeyNotFoundException($"Claim {claimId} not found");

        claim.AdjusterId = adjusterId;
        return await _claimRepository.UpdateAsync(claim);
    }

    public async Task<Claim> UpdateStatusAsync(string claimId, string newStatus)
    {
        var claim = await _claimRepository.GetByIdCrossPartitionAsync(claimId)
            ?? throw new KeyNotFoundException($"Claim {claimId} not found");

        if (!ValidTransitions.TryGetValue(claim.Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOperationException($"Cannot transition from '{claim.Status}' to '{newStatus}'");

        claim.Status = newStatus;
        return await _claimRepository.UpdateAsync(claim);
    }
}
