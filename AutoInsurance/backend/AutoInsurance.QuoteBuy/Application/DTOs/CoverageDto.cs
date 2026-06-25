namespace AutoInsurance.QuoteBuy.Application.DTOs;

public record CoverageDto(
    int CoverageTypeId,
    string LimitOption,
    decimal Deductible
);
