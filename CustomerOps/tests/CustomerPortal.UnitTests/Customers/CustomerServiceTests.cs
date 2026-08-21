using CustomerPortal.Application.Customers;
using CustomerPortal.UnitTests.TestDoubles;
using FluentValidation;
using Xunit;

namespace CustomerPortal.UnitTests.Customers;

public class CustomerServiceTests
{
    private readonly CustomerService _service = new(
        new InMemoryCustomerRepository(),
        new CreateCustomerRequestValidator(),
        new UpdateCustomerRequestValidator());

    private static CreateCustomerRequest ValidCreateRequest() => new()
    {
        FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", Phone = "555-0100"
    };

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsCreatedCustomer()
    {
        var result = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.Equal("Ada", result.FirstName);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ThrowsValidationException()
    {
        var request = new CreateCustomerRequest { FirstName = "", LastName = "Lovelace", Email = "ada@example.com", Phone = "555-0100" };

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ThrowsCustomerNotFoundException()
    {
        await Assert.ThrowsAsync<CustomerNotFoundException>(() => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesFields()
    {
        var created = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        var updated = await _service.UpdateAsync(
            created.Id,
            new UpdateCustomerRequest { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com", Phone = "555-0200" },
            CancellationToken.None);

        Assert.Equal("Grace", updated.FirstName);
        Assert.Equal("Hopper", updated.LastName);
    }

    [Fact]
    public async Task DeactivateAsync_SetsStatusToInactive()
    {
        var created = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        await _service.DeactivateAsync(created.Id, CancellationToken.None);
        var fetched = await _service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.Equal("Inactive", fetched.Status);
    }

    [Fact]
    public async Task ListAsync_ReturnsPagedResults()
    {
        for (var i = 0; i < 5; i++)
        {
            await _service.CreateAsync(
                new CreateCustomerRequest { FirstName = $"First{i}", LastName = $"Last{i}", Email = $"user{i}@example.com", Phone = "555-0000" },
                CancellationToken.None);
        }

        var page1 = await _service.ListAsync(pageNumber: 1, pageSize: 2, CancellationToken.None);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
    }

    [Fact]
    public async Task ListAsync_WithPageSizeAboveMax_ClampsToMax()
    {
        var result = await _service.ListAsync(pageNumber: 1, pageSize: 500, CancellationToken.None);

        Assert.Equal(100, result.PageSize);
    }
}
