using AutoInsuranceMind.API.Models;

namespace AutoInsuranceMind.API.Data;

public static class MockDataStore
{
    public static List<Customer> Customers { get; } = new()
    {
        new Customer { Id = "cust-001", Name = "John Doe", Email = "john@example.com", PhoneNumber = "+1-555-0101" },
        new Customer { Id = "cust-002", Name = "Jane Smith", Email = "jane@example.com", PhoneNumber = "+1-555-0102" }
    };

    public static List<Policy> Policies { get; } = new()
    {
        new Policy
        {
            Id = "pol-001",
            CustomerId = "cust-001",
            PolicyNumber = "53-KB-z773-3",
            Type = "auto",
            Status = "active",
            Premium = 1200.00m,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2027, 1, 1),
            Coverages = new List<Coverage>
            {
                new Coverage { Id = "cov-001", PolicyId = "pol-001", Type = "liability", Limit = 100000, Deductible = 500, Description = "Bodily injury and property damage liability" },
                new Coverage { Id = "cov-002", PolicyId = "pol-001", Type = "collision", Limit = 50000, Deductible = 1000, Description = "Collision damage coverage" },
                new Coverage { Id = "cov-003", PolicyId = "pol-001", Type = "comprehensive", Limit = 50000, Deductible = 500, Description = "Non-collision damage coverage" }
            }
        },
        new Policy
        {
            Id = "pol-002",
            CustomerId = "cust-001",
            PolicyNumber = "AUTO-2025-002",
            Type = "auto",
            Status = "expired",
            Premium = 950.00m,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2026, 1, 1),
            Coverages = new List<Coverage>
            {
                new Coverage { Id = "cov-004", PolicyId = "pol-002", Type = "liability", Limit = 75000, Deductible = 750, Description = "Bodily injury liability" }
            }
        },
        new Policy
        {
            Id = "pol-003",
            CustomerId = "cust-002",
            PolicyNumber = "AUTO-2026-003",
            Type = "auto",
            Status = "active",
            Premium = 1450.00m,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2027, 2, 1),
            Coverages = new List<Coverage>
            {
                new Coverage { Id = "cov-005", PolicyId = "pol-003", Type = "liability", Limit = 150000, Deductible = 500, Description = "Full liability coverage" },
                new Coverage { Id = "cov-006", PolicyId = "pol-003", Type = "medical", Limit = 25000, Deductible = 250, Description = "Medical payments coverage" }
            }
        }
    };

    public static List<UploadedDocument> Documents { get; } = new();
    public static List<ChatMessage> ChatHistory { get; } = new();
}
