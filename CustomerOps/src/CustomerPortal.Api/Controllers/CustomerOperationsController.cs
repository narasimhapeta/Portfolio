using Asp.Versioning;
using CustomerPortal.Application.Common;
using CustomerPortal.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
public class CustomerOperationsController(CustomerService customerService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerDto>>> List(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await customerService.ListAsync(pageNumber, pageSize, ct));

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<CustomerDto>>> Search(
        [FromQuery] string query, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await customerService.SearchAsync(query, pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await customerService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var created = await customerService.CreateAsync(request, ct);
        return Created($"/api/v1/customers/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, UpdateCustomerRequest request, CancellationToken ct)
        => Ok(await customerService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await customerService.DeactivateAsync(id, ct);
        return NoContent();
    }
}
