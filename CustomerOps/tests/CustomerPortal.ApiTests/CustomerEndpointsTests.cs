using System.Net;
using System.Net.Http.Json;
using CustomerPortal.Application.Common;
using CustomerPortal.Application.Customers;
using Xunit;

namespace CustomerPortal.ApiTests;

[Collection(nameof(CustomerApiCollection))]
public class CustomerEndpointsTests(CustomerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateCustomerRequest ValidCreateRequest(string suffix) => new()
    {
        FirstName = "Ada",
        LastName = $"Lovelace{suffix}",
        Email = $"ada.{suffix}@example.com",
        Phone = "555-0100"
    };

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedWithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Create_WithInvalidEmail_ReturnsValidationProblem()
    {
        var invalid = new CreateCustomerRequest { FirstName = "Ada", LastName = "Lovelace", Email = "not-an-email", Phone = "555-0100" };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AfterCreate_ReturnsCustomer()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var response = await _client.GetAsync($"/api/v1/customers/{created!.Id}");
        var fetched = await response.Content.ReadFromJsonAsync<CustomerDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task Update_ChangesCustomerFields()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var updateRequest = new UpdateCustomerRequest { FirstName = "Grace", LastName = "Hopper", Email = created!.Email, Phone = "555-0200" };
        var response = await _client.PutAsJsonAsync($"/api/v1/customers/{created.Id}", updateRequest);
        var updated = await response.Content.ReadFromJsonAsync<CustomerDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Grace", updated!.FirstName);
    }

    [Fact]
    public async Task Deactivate_ReturnsNoContentAndSetsStatusInactive()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/customers/{created!.Id}");
        var getResponse = await _client.GetAsync($"/api/v1/customers/{created.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<CustomerDto>();

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal("Inactive", fetched!.Status);
    }

    [Fact]
    public async Task Search_FiltersByLastName()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            FirstName = "Katherine", LastName = $"Johnson{suffix}", Email = $"katherine.{suffix}@example.com", Phone = "555-0300"
        });

        var response = await _client.GetAsync($"/api/v1/customers/search?query=Johnson{suffix}");
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CustomerDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(result!.Items);
    }
}
