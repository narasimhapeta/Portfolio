namespace AutoInsurance.QuoteBuy.Application.DTOs;

public record QuoteReviewDto(
    Guid QuoteId,
    string QuoteNumber,
    string Status,
    string ZipCode,
    string DraftStateJson,
    List<DriverReviewDto> Drivers,
    List<VehicleReviewDto> Vehicles,
    List<CoverageReviewDto> Coverages,
    decimal TotalAnnualPremium,
    decimal TotalMonthlyPremium
);

public record DriverReviewDto(
    Guid Id,
    string DriverType,
    string FirstName,
    string LastName,
    string DateOfBirth,
    string LicenseNumber,
    string LicenseState
);

public record VehicleReviewDto(
    Guid Id,
    int Year,
    string Make,
    string Model,
    string Vin,
    string PrimaryUse
);

public record CoverageReviewDto(
    int CoverageTypeId,
    string Code,
    string Description,
    string LimitOption,
    decimal Deductible,
    decimal AnnualPremium
);
