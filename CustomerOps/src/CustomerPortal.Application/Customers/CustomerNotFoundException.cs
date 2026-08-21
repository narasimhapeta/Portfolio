namespace CustomerPortal.Application.Customers;

public class CustomerNotFoundException(Guid id) : Exception($"Customer '{id}' was not found.");
