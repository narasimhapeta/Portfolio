using CustomerPortal.Domain;
using Xunit;

namespace CustomerPortal.UnitTests.Domain;

public class CustomerTests
{
    [Fact]
    public void Create_SetsPropertiesAndActiveStatus()
    {
        var customer = Customer.Create("Ada", "Lovelace", "ada@example.com", "555-0100");

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Ada", customer.FirstName);
        Assert.Equal("Lovelace", customer.LastName);
        Assert.Equal("ada@example.com", customer.Email);
        Assert.Equal("555-0100", customer.Phone);
        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.Equal(customer.CreatedAt, customer.UpdatedAt);
    }

    [Fact]
    public void Update_ChangesFieldsAndAdvancesUpdatedAt()
    {
        var customer = Customer.Create("Ada", "Lovelace", "ada@example.com", "555-0100");
        var originalUpdatedAt = customer.UpdatedAt;

        customer.Update("Grace", "Hopper", "grace@example.com", "555-0200");

        Assert.Equal("Grace", customer.FirstName);
        Assert.Equal("Hopper", customer.LastName);
        Assert.Equal("grace@example.com", customer.Email);
        Assert.Equal("555-0200", customer.Phone);
        Assert.True(customer.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void Deactivate_SetsStatusInactive()
    {
        var customer = Customer.Create("Ada", "Lovelace", "ada@example.com", "555-0100");

        customer.Deactivate();

        Assert.Equal(CustomerStatus.Inactive, customer.Status);
    }
}
