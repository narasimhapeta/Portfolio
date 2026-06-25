namespace AutoInsurance.QuoteBuy.Application.DTOs;

public record DriverDto(
    string DriverType,
    string FirstName,
    string LastName,
    string DateOfBirth,
    string LicenseNumber,
    string LicenseState
);
