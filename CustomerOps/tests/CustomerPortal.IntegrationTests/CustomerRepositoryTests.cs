using CustomerPortal.Domain;
using CustomerPortal.Infrastructure.Persistence;
using Xunit;

namespace CustomerPortal.IntegrationTests;

[Collection(nameof(SqlServerCollection))]
public class CustomerRepositoryTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsPersistedCustomer()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);
        var customer = Customer.Create("Ada", "Lovelace", "ada.repo@example.com", "555-0100");

        await repository.AddAsync(customer, CancellationToken.None);

        await using var readContext = fixture.CreateContext();
        var fetched = await new CustomerRepository(readContext).GetByIdAsync(customer.Id, CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal("Ada", fetched!.FirstName);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchAsync_MatchesByLastNameCaseInsensitive()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);
        await repository.AddAsync(Customer.Create("Grace", "Hopper", "grace.search@example.com", "555-0200"), CancellationToken.None);

        var (items, total) = await repository.SearchAsync("hopper", pageNumber: 1, pageSize: 10, CancellationToken.None);

        Assert.Equal(1, total);
        Assert.Equal("Hopper", items[0].LastName);
    }

    [Fact]
    public async Task ListAsync_ReturnsRequestedPageSize()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);
        for (var i = 0; i < 3; i++)
        {
            await repository.AddAsync(
                Customer.Create($"First{i}", $"ListTest{i}", $"listtest{i}@example.com", "555-0300"),
                CancellationToken.None);
        }

        var (items, total) = await repository.ListAsync(pageNumber: 1, pageSize: 2, CancellationToken.None);

        Assert.True(total >= 3);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);
        var customer = Customer.Create("Ada", "Lovelace", "ada.update@example.com", "555-0100");
        await repository.AddAsync(customer, CancellationToken.None);

        customer.Update("Ada", "King", "ada.king@example.com", "555-0101");
        await repository.UpdateAsync(customer, CancellationToken.None);

        await using var readContext = fixture.CreateContext();
        var fetched = await new CustomerRepository(readContext).GetByIdAsync(customer.Id, CancellationToken.None);
        Assert.Equal("King", fetched!.LastName);
    }
}
