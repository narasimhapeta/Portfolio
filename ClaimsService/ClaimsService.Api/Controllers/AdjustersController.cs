// ClaimsService.Api/Controllers/AdjustersController.cs
using ClaimsService.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class AdjustersController : ControllerBase
{
    private readonly IAdjusterRepository _adjusterRepository;

    public AdjustersController(IAdjusterRepository adjusterRepository)
        => _adjusterRepository = adjusterRepository;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var adjusters = await _adjusterRepository.GetAllAsync();
        return Ok(adjusters);
    }
}
