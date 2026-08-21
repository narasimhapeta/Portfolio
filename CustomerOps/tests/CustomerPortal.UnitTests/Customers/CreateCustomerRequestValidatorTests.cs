using CustomerPortal.Application.Customers;
using Xunit;

namespace CustomerPortal.UnitTests.Customers;

public class CreateCustomerRequestValidatorTests
{
    private readonly CreateCustomerRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_HasNoErrors()
    {
        var request = new CreateCustomerRequest { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", Phone = "555-0100" };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithMissingFirstName_HasError()
    {
        var request = new CreateCustomerRequest { FirstName = "", LastName = "Lovelace", Email = "ada@example.com", Phone = "555-0100" };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerRequest.FirstName));
    }

    [Fact]
    public void Validate_WithInvalidEmail_HasError()
    {
        var request = new CreateCustomerRequest { FirstName = "Ada", LastName = "Lovelace", Email = "not-an-email", Phone = "555-0100" };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerRequest.Email));
    }
}
