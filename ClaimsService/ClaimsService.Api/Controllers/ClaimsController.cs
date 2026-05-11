// ClaimsService.Api/Controllers/ClaimsController.cs
using ClaimsService.Api.Models.Requests;
using ClaimsService.Api.Models.Responses;
using ClaimsService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClaimsController : ControllerBase
{
    private readonly IClaimService _claimService;

    public ClaimsController(IClaimService claimService) => _claimService = claimService;

    private string CustomerId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? string.Empty;

    private bool IsAdmin => User.IsInRole("admin");

    [HttpPost("fnol")]
    public async Task<IActionResult> SubmitFnol([FromBody] FnolRequest request)
    {
        var claim = await _claimService.CreateFnolAsync(CustomerId, request);
        return CreatedAtAction(nameof(GetClaim), new { id = claim.Id }, claim);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetClaim(string id)
    {
        var claim = await _claimService.GetClaimAsync(id, CustomerId, IsAdmin);
        if (claim == null) return NotFound();
        if (!IsAdmin && claim.CustomerId != CustomerId) return Forbid();
        return Ok(claim);
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllClaims([FromQuery] string? status)
    {
        var claims = await _claimService.GetAllClaimsAsync(status);
        return Ok(claims);
    }

    [HttpPost("{id}/photos/upload-url")]
    public async Task<IActionResult> GetPhotoUploadUrl(string id, [FromQuery] string fileName)
    {
        try
        {
            var (sasUrl, blobPath, expiresAt) =
                await _claimService.GeneratePhotoUploadUrlAsync(id, CustomerId, fileName);
            return Ok(new SasUploadUrlResponse(sasUrl, blobPath, expiresAt));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/assign")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignAdjuster(string id, [FromBody] AssignAdjusterRequest request)
    {
        try
        {
            var claim = await _claimService.AssignAdjusterAsync(id, request.AdjusterId);
            return Ok(claim);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var claim = await _claimService.UpdateStatusAsync(id, request.Status);
            return Ok(claim);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
