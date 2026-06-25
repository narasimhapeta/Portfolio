namespace AutoInsurance.CustomerService.Application.DTOs;

public record PolicySummaryDto(
    Guid Id,
    string PolicyNumber,
    string Status,
    DateOnly EffectiveDate,
    DateOnly ExpirationDate,
    decimal TotalAnnualPremium
);

public record PolicyDetailDto(
    Guid Id,
    string PolicyNumber,
    string Status,
    DateOnly EffectiveDate,
    DateOnly ExpirationDate,
    decimal TotalAnnualPremium,
    List<PolicyDriverDto> Drivers,
    List<PolicyVehicleDto> Vehicles,
    List<PolicyCoverageDto> Coverages,
    List<EndorsementDto> Endorsements
);

public record PolicyDriverDto(Guid Id, string DriverType, string FirstName, string LastName, string DateOfBirth, string LicenseState);
public record PolicyVehicleDto(Guid Id, int Year, string Make, string Model, string Vin, string PrimaryUse);
public record PolicyCoverageDto(int CoverageTypeId, string LimitOption, decimal Deductible, decimal AnnualPremium);
public record EndorsementDto(Guid Id, string Type, string Status, DateOnly EffectiveDate, DateTime CreatedAt);

public record DocumentDto(Guid Id, string Type, string BlobUrl, DateTime GeneratedAt);

public record AccountDto(Guid Id, string B2CObjectId, string Email, Guid PolicyId);

public record CoverageChangeDto(int CoverageTypeId, string LimitOption, decimal Deductible);
