namespace AutoInsurance.Claims.Application.DTOs;

public record ClaimDto(Guid Id, Guid PolicyId, DateOnly IncidentDate, string Description, string Status, DateTime CreatedAt);

public record ClaimDetailDto(
    Guid Id, Guid PolicyId, DateOnly IncidentDate, string Description, string Status,
    DateTime CreatedAt, List<ClaimDocumentDto> Documents);

public record ClaimDocumentDto(Guid Id, string Type, string BlobUrl, DateTime UploadedAt);
