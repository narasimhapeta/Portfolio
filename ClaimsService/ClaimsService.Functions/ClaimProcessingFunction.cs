// ClaimsService.Functions/ClaimProcessingFunction.cs
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using ClaimsService.Core.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ClaimsService.Functions;

public class ClaimProcessingFunction
{
    private readonly IClaimRepository _claimRepository;
    private readonly ILogger<ClaimProcessingFunction> _logger;

    public ClaimProcessingFunction(IClaimRepository claimRepository, ILogger<ClaimProcessingFunction> logger)
    {
        _claimRepository = claimRepository;
        _logger = logger;
    }

    [Function("ClaimProcessingFunction")]
    public async Task Run([EventGridTrigger] EventGridEvent eventGridEvent)
    {
        if (eventGridEvent.EventType != "Microsoft.Storage.BlobCreated")
        {
            _logger.LogInformation("Skipping event type: {EventType}", eventGridEvent.EventType);
            return;
        }

        var data = eventGridEvent.Data.ToObjectFromJson<StorageBlobCreatedEventData>();

        if (data == null || string.IsNullOrEmpty(data.Url))
        {
            _logger.LogWarning("BlobCreated event missing URL");
            return;
        }

        // Blob URL format: https://<account>.blob.core.windows.net/claims/{claimId}/{filename}
        var uri = new Uri(data.Url);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/');
        // segments[0] = "claims", segments[1] = claimId, segments[2] = filename
        if (segments.Length < 3)
        {
            _logger.LogWarning("Unexpected blob path format: {Url}", data.Url);
            return;
        }

        var claimId = segments[1];
        _logger.LogInformation("Processing photo upload for claim {ClaimId}", claimId);

        var claim = await _claimRepository.GetByIdCrossPartitionAsync(claimId);
        if (claim == null)
        {
            _logger.LogWarning("Claim {ClaimId} not found in Cosmos DB", claimId);
            return;
        }

        // Mock AI: simulate processing delay, return fixed damage score
        await Task.Delay(TimeSpan.FromSeconds(2));
        claim.DamageScore = 72;
        claim.Status = "UnderReview";

        await _claimRepository.UpdateAsync(claim);
        _logger.LogInformation("Claim {ClaimId} updated to UnderReview with damage score 72", claimId);
    }
}
