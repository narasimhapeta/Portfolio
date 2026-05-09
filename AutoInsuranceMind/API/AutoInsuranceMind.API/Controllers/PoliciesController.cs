using AutoInsuranceMind.API.Data;
using AutoInsuranceMind.API.Models;
using AutoInsuranceMind.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsuranceMind.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly ILogger<PoliciesController> _logger;

    public PoliciesController(NotificationService notificationService, ILogger<PoliciesController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetPolicies([FromQuery] string customerId = "cust-001")
    {
        var policies = MockDataStore.Policies
            .Where(p => p.CustomerId == customerId)
            .ToList();
        return Ok(new { policies, total = policies.Count });
    }

    [HttpGet("{id}")]
    public IActionResult GetPolicy(string id)
    {
        var policy = MockDataStore.Policies.FirstOrDefault(p => p.Id == id);
        return policy == null ? NotFound(new { message = "Policy not found" }) : Ok(policy);
    }

    [HttpGet("{id}/coverages")]
    public IActionResult GetCoverages(string id)
    {
        var policy = MockDataStore.Policies.FirstOrDefault(p => p.Id == id);
        if (policy == null) return NotFound(new { message = "Policy not found" });
        return Ok(new { coverages = policy.Coverages });
    }

    [HttpPut("{id}/coverages/{covId}")]
    public async Task<IActionResult> UpdateCoverage(string id, string covId, [FromBody] Coverage updated)
    {
        var policy = MockDataStore.Policies.FirstOrDefault(p => p.Id == id);
        if (policy == null) return NotFound(new { message = "Policy not found" });

        var cov = policy.Coverages.FirstOrDefault(c => c.Id == covId);
        if (cov == null) return NotFound(new { message = "Coverage not found" });

        cov.Limit = updated.Limit;
        cov.Deductible = updated.Deductible;
        if (!string.IsNullOrEmpty(updated.Description))
            cov.Description = updated.Description;

        _logger.LogInformation("Coverage {CovId} updated on policy {PolicyId}", covId, id);

        await _notificationService.SendCoverageUpdateNotificationAsync(
            policy.CustomerId, policy.PolicyNumber, cov.Type);

        return Ok(new { success = true, message = "Coverage updated successfully", coverage = cov });
    }
}
