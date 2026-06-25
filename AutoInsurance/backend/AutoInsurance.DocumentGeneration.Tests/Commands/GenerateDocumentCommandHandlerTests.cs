using AutoInsurance.DocumentGeneration.Application.Commands.GenerateDocument;
using AutoInsurance.DocumentGeneration.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Document;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.DocumentGeneration.Tests.Commands;

public class GenerateDocumentCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IBlobService> _blobMock = new();

    private GenerateDocumentCommandHandler CreateHandler() =>
        new(_repoMock.Object, _uowMock.Object, _blobMock.Object);

    [Fact]
    public async Task Handle_InvalidDocumentType_ReturnsFailure()
    {
        var result = await CreateHandler().Handle(
            new GenerateDocumentCommand(Guid.NewGuid(), "InvalidType"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid document type");
    }

    [Fact]
    public async Task Handle_PolicyNotFound_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetPolicyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Policy?)null);

        var result = await CreateHandler().Handle(
            new GenerateDocumentCommand(Guid.NewGuid(), DocumentType.InsuranceCard), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ValidRequest_UploadsAndReturnsDocumentDto()
    {
        var policyId = Guid.NewGuid();
        var policy = new Policy { Id = policyId, PolicyNumber = "POL-TEST-001" };
        const string expectedUrl = "http://localhost:10000/devstoreaccount1/policy-documents/mock.pdf";

        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        _blobMock.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);
        _repoMock.Setup(r => r.AddDocumentAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().Handle(
            new GenerateDocumentCommand(policyId, DocumentType.InsuranceCard), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be(DocumentType.InsuranceCard);
        result.Value.BlobUrl.Should().Be(expectedUrl);
        result.Value.PolicyId.Should().Be(policyId);
        _blobMock.Verify(b => b.UploadAsync("policy-documents", It.IsAny<string>(), It.IsAny<Stream>(), "application/pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DeclarationPage_GeneratesCorrectDocumentType()
    {
        var policyId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Policy { Id = policyId, PolicyNumber = "POL-ABC" });
        _blobMock.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://mock/url");
        _repoMock.Setup(r => r.AddDocumentAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().Handle(
            new GenerateDocumentCommand(policyId, DocumentType.DeclarationPage), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be(DocumentType.DeclarationPage);
    }
}
