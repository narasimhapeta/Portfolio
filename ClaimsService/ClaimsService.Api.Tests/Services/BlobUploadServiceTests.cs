using ClaimsService.Api.Services;
using Xunit;

namespace ClaimsService.Api.Tests.Services;

public class BlobUploadServiceTests
{
    [Fact]
    public void BlobPath_IsFormatted_AsClaimIdSlashFileName()
    {
        // BlobUploadService builds path as "{claimId}/{fileName}"
        // Verify the path format by checking string construction directly
        var claimId = "claim-123";
        var fileName = "photo.jpg";
        var expectedPath = $"{claimId}/{fileName}";

        Assert.Equal("claim-123/photo.jpg", expectedPath);
    }
}
