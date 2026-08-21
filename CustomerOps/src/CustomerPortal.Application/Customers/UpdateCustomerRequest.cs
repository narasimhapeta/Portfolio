namespace CustomerPortal.Application.Customers;

public class UpdateCustomerRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
}
