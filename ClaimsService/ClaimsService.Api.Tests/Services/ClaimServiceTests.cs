// ClaimsService.Api.Tests/Services/ClaimServiceTests.cs
using ClaimsService.Api.Models.Requests;
using ClaimsService.Api.Services;
using ClaimsService.Core.Models;
using ClaimsService.Core.Repositories;
using Moq;
using Xunit;

namespace ClaimsService.Api.Tests.Services;

public class ClaimServiceTests
{
    private readonly Mock<IClaimRepository> _claimRepoMock = new();
    private readonly Mock<IAdjusterRepository> _adjusterRepoMock = new();
    private readonly Mock<IBlobUploadService> _blobServiceMock = new();
    private readonly ClaimService _sut;

    public ClaimServiceTests()
    {
        _sut = new ClaimService(_claimRepoMock.Object, _adjusterRepoMock.Object, _blobServiceMock.Object);
    }

    [Fact]
    public async Task CreateFnolAsync_ReturnsClaim_WithFnolStatus()
    {
        var request = new FnolRequest("POL-001", DateTime.UtcNow, "Rear-end collision");
        _claimRepoMock.Setup(r => r.CreateAsync(It.IsAny<Claim>()))
            .ReturnsAsync((Claim c) => c);

        var result = await _sut.CreateFnolAsync("cust-001", request);

        Assert.Equal("FNOL", result.Status);
        Assert.Equal("cust-001", result.CustomerId);
        Assert.Equal("POL-001", result.PolicyNumber);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesClaim()
    {
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "UnderReview" };
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("c1")).ReturnsAsync(claim);
        _claimRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Claim>())).ReturnsAsync((Claim c) => c);

        var result = await _sut.UpdateStatusAsync("c1", "Approved");

        Assert.Equal("Approved", result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ThrowsInvalidOperationException()
    {
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "FNOL" };
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("c1")).ReturnsAsync(claim);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync("c1", "Approved"));
    }

    [Fact]
    public async Task UpdateStatusAsync_ClaimNotFound_ThrowsKeyNotFoundException()
    {
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("missing"))
            .ReturnsAsync((Claim?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.UpdateStatusAsync("missing", "UnderReview"));
    }

    [Fact]
    public async Task GetClaimAsync_AsCustomer_UsesPartitionKeyRead()
    {
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "FNOL" };
        _claimRepoMock.Setup(r => r.GetByIdAsync("c1", "cust-1")).ReturnsAsync(claim);

        var result = await _sut.GetClaimAsync("c1", "cust-1", isAdmin: false);

        Assert.Equal("c1", result?.Id);
        _claimRepoMock.Verify(r => r.GetByIdAsync("c1", "cust-1"), Times.Once);
        _claimRepoMock.Verify(r => r.GetByIdCrossPartitionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetClaimAsync_AsAdmin_UsesCrossPartitionRead()
    {
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "FNOL" };
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("c1")).ReturnsAsync(claim);

        var result = await _sut.GetClaimAsync("c1", string.Empty, isAdmin: true);

        Assert.Equal("c1", result?.Id);
        _claimRepoMock.Verify(r => r.GetByIdCrossPartitionAsync("c1"), Times.Once);
    }

    [Fact]
    public async Task AssignAdjusterAsync_AdjusterNotFound_ThrowsKeyNotFoundException()
    {
        _adjusterRepoMock.Setup(r => r.GetByIdAsync("adj-missing"))
            .ReturnsAsync((Adjuster?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.AssignAdjusterAsync("c1", "adj-missing"));
    }

    [Fact]
    public async Task GeneratePhotoUploadUrlAsync_ClaimNotFound_ThrowsKeyNotFoundException()
    {
        _claimRepoMock.Setup(r => r.GetByIdAsync("missing", "cust-1"))
            .ReturnsAsync((Claim?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.GeneratePhotoUploadUrlAsync("missing", "cust-1", "photo.jpg"));
    }

    [Fact]
    public async Task GeneratePhotoUploadUrlAsync_ReturnsUploadUrl_AndUpdatesClaim()
    {
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "FNOL", PhotosBlobPaths = new() };
        _claimRepoMock.Setup(r => r.GetByIdAsync("c1", "cust-1")).ReturnsAsync(claim);
        _blobServiceMock.Setup(b => b.GenerateSasUploadUrlAsync("c1", "photo.jpg"))
            .ReturnsAsync(("https://storage.example.com/sas", "c1/photo.jpg"));
        _claimRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Claim>())).ReturnsAsync((Claim c) => c);

        var (sasUrl, blobPath, expiresAt) = await _sut.GeneratePhotoUploadUrlAsync("c1", "cust-1", "photo.jpg");

        Assert.Equal("https://storage.example.com/sas", sasUrl);
        Assert.Equal("c1/photo.jpg", blobPath);
        Assert.Contains("c1/photo.jpg", claim.PhotosBlobPaths);
        _claimRepoMock.Verify(r => r.UpdateAsync(claim), Times.Once);
    }

    [Fact]
    public async Task AssignAdjusterAsync_ClaimNotFound_ThrowsKeyNotFoundException()
    {
        var adjuster = new Adjuster { Id = "adj-001", Name = "Jane Smith" };
        _adjusterRepoMock.Setup(r => r.GetByIdAsync("adj-001")).ReturnsAsync(adjuster);
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("missing"))
            .ReturnsAsync((Claim?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.AssignAdjusterAsync("missing", "adj-001"));
    }

    [Fact]
    public async Task AssignAdjusterAsync_SuccessfullyAssigns_AndUpdatesClaim()
    {
        var adjuster = new Adjuster { Id = "adj-001", Name = "Jane Smith" };
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "UnderReview" };
        _adjusterRepoMock.Setup(r => r.GetByIdAsync("adj-001")).ReturnsAsync(adjuster);
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("c1")).ReturnsAsync(claim);
        _claimRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Claim>())).ReturnsAsync((Claim c) => c);

        var result = await _sut.AssignAdjusterAsync("c1", "adj-001");

        Assert.Equal("adj-001", result.AdjusterId);
        _claimRepoMock.Verify(r => r.UpdateAsync(claim), Times.Once);
    }
}
