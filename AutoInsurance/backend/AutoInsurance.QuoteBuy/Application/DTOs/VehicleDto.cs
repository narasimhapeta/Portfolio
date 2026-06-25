namespace AutoInsurance.QuoteBuy.Application.DTOs;

public record VehicleDto(
    int Year,
    string Make,
    string Model,
    string Vin,
    string PrimaryUse
);
