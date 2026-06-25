namespace AutoInsurance.DocumentGeneration.Application.DTOs;

public record DocumentDto(Guid Id, Guid PolicyId, string Type, string BlobUrl, DateTime GeneratedAt);
