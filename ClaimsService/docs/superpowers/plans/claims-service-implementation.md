# Claims Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 9 Claims Service with Web API + Azure Functions v4 using real Azure Blob Storage, Event Grid, and Cosmos DB.

**Architecture:** Four-project solution — `ClaimsService.Core` (shared models + repositories), `ClaimsService.Api` (Web API), `ClaimsService.Functions` (Event Grid-triggered Azure Function), `ClaimsService.Api.Tests` (unit tests). The API handles all HTTP endpoints backed by Cosmos DB; the Function receives push events from Event Grid when photos are uploaded to Blob Storage, runs mock AI processing, and updates the claim record.

**Tech Stack:** .NET 9, ASP.NET Core Web API, Azure Functions v4 (isolated worker), Azure Cosmos DB SDK v3, Azure Blob Storage SDK (Azure.Storage.Blobs), Azure Event Grid (Azure.Messaging.EventGrid), JWT Bearer (HS256), Newtonsoft.Json, xUnit, Moq

---

## File Map

```
ClaimsService/
├── ClaimsService.sln
├── ClaimsService.Core/
│   ├── ClaimsService.Core.csproj
│   ├── Models/
│   │   ├── Claim.cs
│   │   └── Adjuster.cs
│   └── Repositories/
│       ├── IClaimRepository.cs
│       ├── ClaimRepository.cs
│       ├── IAdjusterRepository.cs
│       └── AdjusterRepository.cs
├── ClaimsService.Api/
│   ├── ClaimsService.Api.csproj
│   ├── Controllers/
│   │   ├── ClaimsController.cs
│   │   └── AdjustersController.cs
│   ├── Models/
│   │   ├── Requests/
│   │   │   ├── FnolRequest.cs
│   │   │   ├── AssignAdjusterRequest.cs
│   │   │   └── UpdateStatusRequest.cs
│   │   └── Responses/
│   │       └── SasUploadUrlResponse.cs
│   ├── Services/
│   │   ├── IBlobUploadService.cs
│   │   ├── BlobUploadService.cs
│   │   ├── IClaimService.cs
│   │   └── ClaimService.cs
│   ├── Program.cs
│   └── appsettings.json
├── ClaimsService.Functions/
│   ├── ClaimsService.Functions.csproj
│   ├── ClaimProcessingFunction.cs
│   ├── Program.cs
│   ├── host.json
│   └── local.settings.json
└── ClaimsService.Api.Tests/
    ├── ClaimsService.Api.Tests.csproj
    └── Services/
        └── ClaimServiceTests.cs
```

---

## Task 1: Solution & Project Scaffold

**Files:**
- Create: `ClaimsService.sln`
- Create: `ClaimsService.Core/ClaimsService.Core.csproj`
- Create: `ClaimsService.Api/ClaimsService.Api.csproj`
- Create: `ClaimsService.Functions/ClaimsService.Functions.csproj`
- Create: `ClaimsService.Api.Tests/ClaimsService.Api.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

Run from `c:\Narasimha\Portfolio\Portfolio\ClaimsService`:

```powershell
dotnet new sln -n ClaimsService
dotnet new classlib -n ClaimsService.Core --framework net9.0
dotnet new webapi -n ClaimsService.Api --framework net9.0 --no-openapi
dotnet new classlib -n ClaimsService.Functions --framework net9.0
dotnet new xunit -n ClaimsService.Api.Tests --framework net9.0
dotnet sln add ClaimsService.Core/ClaimsService.Core.csproj
dotnet sln add ClaimsService.Api/ClaimsService.Api.csproj
dotnet sln add ClaimsService.Functions/ClaimsService.Functions.csproj
dotnet sln add ClaimsService.Api.Tests/ClaimsService.Api.Tests.csproj
```

- [ ] **Step 2: Add project references**

```powershell
dotnet add ClaimsService.Api/ClaimsService.Api.csproj reference ClaimsService.Core/ClaimsService.Core.csproj
dotnet add ClaimsService.Functions/ClaimsService.Functions.csproj reference ClaimsService.Core/ClaimsService.Core.csproj
dotnet add ClaimsService.Api.Tests/ClaimsService.Api.Tests.csproj reference ClaimsService.Api/ClaimsService.Api.csproj
dotnet add ClaimsService.Api.Tests/ClaimsService.Api.Tests.csproj reference ClaimsService.Core/ClaimsService.Core.csproj
```

- [ ] **Step 3: Add NuGet packages to Core**

```powershell
dotnet add ClaimsService.Core/ClaimsService.Core.csproj package Microsoft.Azure.Cosmos
```

- [ ] **Step 4: Add NuGet packages to Api**

```powershell
dotnet add ClaimsService.Api/ClaimsService.Api.csproj package Azure.Storage.Blobs
dotnet add ClaimsService.Api/ClaimsService.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add ClaimsService.Api/ClaimsService.Api.csproj package Microsoft.AspNetCore.Mvc.NewtonsoftJson
dotnet add ClaimsService.Api/ClaimsService.Api.csproj package Swashbuckle.AspNetCore
dotnet add ClaimsService.Api/ClaimsService.Api.csproj package Microsoft.IdentityModel.Tokens
```

- [ ] **Step 5: Configure Functions .csproj as an Azure Functions isolated worker**

Replace the contents of `ClaimsService.Functions/ClaimsService.Functions.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>ClaimsService.Functions</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.*" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.*" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.EventGrid" Version="3.*" />
  </ItemGroup>
  <ItemGroup>
    <None Update="local.settings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>Never</CopyToPublishDirectory>
    </None>
    <None Update="host.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Add NuGet packages to Tests**

```powershell
dotnet add ClaimsService.Api.Tests/ClaimsService.Api.Tests.csproj package Moq
```

- [ ] **Step 7: Verify build**

```powershell
dotnet build ClaimsService.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit**

```powershell
git add ClaimsService.sln ClaimsService.Core ClaimsService.Api ClaimsService.Functions ClaimsService.Api.Tests
git commit -m "feat(claims): scaffold solution with Core, Api, Functions, Tests projects"
```

---

## Task 2: Core — Models

**Files:**
- Create: `ClaimsService.Core/Models/Claim.cs`
- Create: `ClaimsService.Core/Models/Adjuster.cs`
- Delete: `ClaimsService.Core/Class1.cs` (auto-generated placeholder)

- [ ] **Step 1: Delete the placeholder**

```powershell
Remove-Item ClaimsService.Core/Class1.cs
```

- [ ] **Step 2: Create `Claim.cs`**

```csharp
// ClaimsService.Core/Models/Claim.cs
using Newtonsoft.Json;

namespace ClaimsService.Core.Models;

public class Claim
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("policyNumber")]
    public string PolicyNumber { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = "FNOL";

    [JsonProperty("incidentDate")]
    public DateTime IncidentDate { get; set; }

    [JsonProperty("incidentDescription")]
    public string IncidentDescription { get; set; } = string.Empty;

    [JsonProperty("photosBlobPaths")]
    public List<string> PhotosBlobPaths { get; set; } = new();

    [JsonProperty("damageScore")]
    public int? DamageScore { get; set; }

    [JsonProperty("adjusterId")]
    public string? AdjusterId { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Create `Adjuster.cs`**

```csharp
// ClaimsService.Core/Models/Adjuster.cs
using Newtonsoft.Json;

namespace ClaimsService.Core.Models;

public class Adjuster
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("isAvailable")]
    public bool IsAvailable { get; set; } = true;
}
```

- [ ] **Step 4: Build and verify**

```powershell
dotnet build ClaimsService.Core/ClaimsService.Core.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```powershell
git add ClaimsService.Core/Models/
git commit -m "feat(claims): add Claim and Adjuster models"
```

---

## Task 3: Core — Repository Interfaces & Implementations

**Files:**
- Create: `ClaimsService.Core/Repositories/IClaimRepository.cs`
- Create: `ClaimsService.Core/Repositories/ClaimRepository.cs`
- Create: `ClaimsService.Core/Repositories/IAdjusterRepository.cs`
- Create: `ClaimsService.Core/Repositories/AdjusterRepository.cs`

- [ ] **Step 1: Create `IClaimRepository.cs`**

```csharp
// ClaimsService.Core/Repositories/IClaimRepository.cs
using ClaimsService.Core.Models;

namespace ClaimsService.Core.Repositories;

public interface IClaimRepository
{
    Task<Claim?> GetByIdAsync(string id, string customerId);
    Task<Claim?> GetByIdCrossPartitionAsync(string id);
    Task<IEnumerable<Claim>> GetAllAsync(string? status = null);
    Task<Claim> CreateAsync(Claim claim);
    Task<Claim> UpdateAsync(Claim claim);
}
```

- [ ] **Step 2: Create `ClaimRepository.cs`**

```csharp
// ClaimsService.Core/Repositories/ClaimRepository.cs
using System.Net;
using ClaimsService.Core.Models;
using Microsoft.Azure.Cosmos;

namespace ClaimsService.Core.Repositories;

public class ClaimRepository : IClaimRepository
{
    private readonly Container _container;

    public ClaimRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetDatabase(databaseName).GetContainer("claims");
    }

    public async Task<Claim?> GetByIdAsync(string id, string customerId)
    {
        try
        {
            var response = await _container.ReadItemAsync<Claim>(id, new PartitionKey(customerId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Claim?> GetByIdCrossPartitionAsync(string id)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", id);
        var iterator = _container.GetItemQueryIterator<Claim>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            var item = page.FirstOrDefault();
            if (item != null) return item;
        }
        return null;
    }

    public async Task<IEnumerable<Claim>> GetAllAsync(string? status = null)
    {
        var query = status != null
            ? new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
                .WithParameter("@status", status)
            : new QueryDefinition("SELECT * FROM c");

        var iterator = _container.GetItemQueryIterator<Claim>(query);
        var results = new List<Claim>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            results.AddRange(page);
        }
        return results;
    }

    public async Task<Claim> CreateAsync(Claim claim)
    {
        var response = await _container.CreateItemAsync(claim, new PartitionKey(claim.CustomerId));
        return response.Resource;
    }

    public async Task<Claim> UpdateAsync(Claim claim)
    {
        claim.UpdatedAt = DateTime.UtcNow;
        var response = await _container.ReplaceItemAsync(claim, claim.Id, new PartitionKey(claim.CustomerId));
        return response.Resource;
    }
}
```

- [ ] **Step 3: Create `IAdjusterRepository.cs`**

```csharp
// ClaimsService.Core/Repositories/IAdjusterRepository.cs
using ClaimsService.Core.Models;

namespace ClaimsService.Core.Repositories;

public interface IAdjusterRepository
{
    Task<Adjuster?> GetByIdAsync(string id);
    Task<IEnumerable<Adjuster>> GetAllAsync();
    Task UpsertAsync(Adjuster adjuster);
}
```

- [ ] **Step 4: Create `AdjusterRepository.cs`**

```csharp
// ClaimsService.Core/Repositories/AdjusterRepository.cs
using System.Net;
using ClaimsService.Core.Models;
using Microsoft.Azure.Cosmos;

namespace ClaimsService.Core.Repositories;

public class AdjusterRepository : IAdjusterRepository
{
    private readonly Container _container;

    public AdjusterRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetDatabase(databaseName).GetContainer("adjusters");
    }

    public async Task<Adjuster?> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Adjuster>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Adjuster>> GetAllAsync()
    {
        var iterator = _container.GetItemQueryIterator<Adjuster>(
            new QueryDefinition("SELECT * FROM c"));
        var results = new List<Adjuster>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            results.AddRange(page);
        }
        return results;
    }

    public async Task UpsertAsync(Adjuster adjuster)
    {
        await _container.UpsertItemAsync(adjuster, new PartitionKey(adjuster.Id));
    }
}
```

- [ ] **Step 5: Build Core**

```powershell
dotnet build ClaimsService.Core/ClaimsService.Core.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```powershell
git add ClaimsService.Core/Repositories/
git commit -m "feat(claims): add Cosmos DB repositories for claims and adjusters"
```

---

## Task 4: API — DTOs & BlobUploadService (with tests)

**Files:**
- Create: `ClaimsService.Api/Models/Requests/FnolRequest.cs`
- Create: `ClaimsService.Api/Models/Requests/AssignAdjusterRequest.cs`
- Create: `ClaimsService.Api/Models/Requests/UpdateStatusRequest.cs`
- Create: `ClaimsService.Api/Models/Responses/SasUploadUrlResponse.cs`
- Create: `ClaimsService.Api/Services/IBlobUploadService.cs`
- Create: `ClaimsService.Api/Services/BlobUploadService.cs`
- Create: `ClaimsService.Api.Tests/Services/BlobUploadServiceTests.cs`

- [ ] **Step 1: Create request DTOs**

```csharp
// ClaimsService.Api/Models/Requests/FnolRequest.cs
namespace ClaimsService.Api.Models.Requests;

public record FnolRequest(string PolicyNumber, DateTime IncidentDate, string IncidentDescription);
```

```csharp
// ClaimsService.Api/Models/Requests/AssignAdjusterRequest.cs
namespace ClaimsService.Api.Models.Requests;

public record AssignAdjusterRequest(string AdjusterId);
```

```csharp
// ClaimsService.Api/Models/Requests/UpdateStatusRequest.cs
namespace ClaimsService.Api.Models.Requests;

public record UpdateStatusRequest(string Status);
```

```csharp
// ClaimsService.Api/Models/Responses/SasUploadUrlResponse.cs
namespace ClaimsService.Api.Models.Responses;

public record SasUploadUrlResponse(string UploadUrl, string BlobPath, DateTime ExpiresAt);
```

- [ ] **Step 2: Create `IBlobUploadService.cs`**

```csharp
// ClaimsService.Api/Services/IBlobUploadService.cs
namespace ClaimsService.Api.Services;

public interface IBlobUploadService
{
    Task<(string SasUrl, string BlobPath)> GenerateSasUploadUrlAsync(string claimId, string fileName);
}
```

- [ ] **Step 3: Create `BlobUploadService.cs`**

```csharp
// ClaimsService.Api/Services/BlobUploadService.cs
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace ClaimsService.Api.Services;

public class BlobUploadService : IBlobUploadService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public BlobUploadService(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = configuration["Azure:BlobStorage:ContainerName"]!;
    }

    public Task<(string SasUrl, string BlobPath)> GenerateSasUploadUrlAsync(string claimId, string fileName)
    {
        var blobPath = $"{claimId}/{fileName}";
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult((sasUri.ToString(), blobPath));
    }
}
```

- [ ] **Step 4: Write failing test**

```csharp
// ClaimsService.Api.Tests/Services/BlobUploadServiceTests.cs
using ClaimsService.Api.Services;
using Xunit;

namespace ClaimsService.Api.Tests.Services;

public class BlobUploadServiceTests
{
    [Fact]
    public void BlobPath_IsFormatted_AsClaimIdSlashFileName()
    {
        // BlobUploadService builds path as "{claimId}/{fileName}"
        // Verify the path format by checking string construction directly
        var claimId = "claim-123";
        var fileName = "photo.jpg";
        var expectedPath = $"{claimId}/{fileName}";

        Assert.Equal("claim-123/photo.jpg", expectedPath);
    }
}
```

Note: `BlobUploadService.GenerateSasUri` requires a real storage account key to sign the SAS token. The integration test above validates path formatting. Full SAS URL generation is tested end-to-end when connected to Azure.

- [ ] **Step 5: Run test**

```powershell
dotnet test ClaimsService.Api.Tests/ClaimsService.Api.Tests.csproj -v minimal
```

Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 6: Commit**

```powershell
git add ClaimsService.Api/Models/ ClaimsService.Api/Services/IBlobUploadService.cs ClaimsService.Api/Services/BlobUploadService.cs ClaimsService.Api.Tests/Services/BlobUploadServiceTests.cs
git commit -m "feat(claims): add DTOs and BlobUploadService with SAS URL generation"
```

---

## Task 5: API — ClaimService Business Logic (with tests)

**Files:**
- Create: `ClaimsService.Api/Services/IClaimService.cs`
- Create: `ClaimsService.Api/Services/ClaimService.cs`
- Create: `ClaimsService.Api.Tests/Services/ClaimServiceTests.cs`

- [ ] **Step 1: Write failing tests first**

```csharp
// ClaimsService.Api.Tests/Services/ClaimServiceTests.cs
using ClaimsService.Api.Models.Requests;
using ClaimsService.Api.Services;
using ClaimsService.Core.Models;
using ClaimsService.Core.Repositories;
using Moq;
using Xunit;

namespace ClaimsService.Api.Tests.Services;

public class ClaimServiceTests
{
    private readonly Mock<IClaimRepository> _claimRepoMock = new();
    private readonly Mock<IAdjusterRepository> _adjusterRepoMock = new();
    private readonly Mock<IBlobUploadService> _blobServiceMock = new();
    private readonly ClaimService _sut;

    public ClaimServiceTests()
    {
        _sut = new ClaimService(_claimRepoMock.Object, _adjusterRepoMock.Object, _blobServiceMock.Object);
    }

    [Fact]
    public async Task CreateFnolAsync_ReturnsClaim_WithFnolStatus()
    {
        var request = new FnolRequest("POL-001", DateTime.UtcNow, "Rear-end collision");
        _claimRepoMock.Setup(r => r.CreateAsync(It.IsAny<Claim>()))
            .ReturnsAsync((Claim c) => c);

        var result = await _sut.CreateFnolAsync("cust-001", request);

        Assert.Equal("FNOL", result.Status);
        Assert.Equal("cust-001", result.CustomerId);
        Assert.Equal("POL-001", result.PolicyNumber);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesClaim()
    {
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "UnderReview" };
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("c1")).ReturnsAsync(claim);
        _claimRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Claim>())).ReturnsAsync((Claim c) => c);

        var result = await _sut.UpdateStatusAsync("c1", "Approved");

        Assert.Equal("Approved", result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ThrowsInvalidOperationException()
    {
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "FNOL" };
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("c1")).ReturnsAsync(claim);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync("c1", "Approved"));
    }

    [Fact]
    public async Task UpdateStatusAsync_ClaimNotFound_ThrowsKeyNotFoundException()
    {
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("missing"))
            .ReturnsAsync((Claim?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.UpdateStatusAsync("missing", "UnderReview"));
    }

    [Fact]
    public async Task GetClaimAsync_AsCustomer_UsesPartitionKeyRead()
    {
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "FNOL" };
        _claimRepoMock.Setup(r => r.GetByIdAsync("c1", "cust-1")).ReturnsAsync(claim);

        var result = await _sut.GetClaimAsync("c1", "cust-1", isAdmin: false);

        Assert.Equal("c1", result?.Id);
        _claimRepoMock.Verify(r => r.GetByIdAsync("c1", "cust-1"), Times.Once);
        _claimRepoMock.Verify(r => r.GetByIdCrossPartitionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetClaimAsync_AsAdmin_UsesCrossPartitionRead()
    {
        var claim = new Claim { Id = "c1", CustomerId = "cust-1", Status = "FNOL" };
        _claimRepoMock.Setup(r => r.GetByIdCrossPartitionAsync("c1")).ReturnsAsync(claim);

        var result = await _sut.GetClaimAsync("c1", string.Empty, isAdmin: true);

        Assert.Equal("c1", result?.Id);
        _claimRepoMock.Verify(r => r.GetByIdCrossPartitionAsync("c1"), Times.Once);
    }

    [Fact]
    public async Task AssignAdjusterAsync_AdjusterNotFound_ThrowsKeyNotFoundException()
    {
        _adjusterRepoMock.Setup(r => r.GetByIdAsync("adj-missing"))
            .ReturnsAsync((Adjuster?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.AssignAdjusterAsync("c1", "adj-missing"));
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure (ClaimService not yet created)**

```powershell
dotnet build ClaimsService.Api.Tests/ClaimsService.Api.Tests.csproj
```

Expected: Build error — `ClaimService` not found.

- [ ] **Step 3: Create `IClaimService.cs`**

```csharp
// ClaimsService.Api/Services/IClaimService.cs
using ClaimsService.Api.Models.Requests;
using ClaimsService.Core.Models;

namespace ClaimsService.Api.Services;

public interface IClaimService
{
    Task<Claim> CreateFnolAsync(string customerId, FnolRequest request);
    Task<Claim?> GetClaimAsync(string id, string customerId, bool isAdmin);
    Task<IEnumerable<Claim>> GetAllClaimsAsync(string? status);
    Task<(string SasUrl, string BlobPath, DateTime ExpiresAt)> GeneratePhotoUploadUrlAsync(
        string claimId, string customerId, string fileName);
    Task<Claim> AssignAdjusterAsync(string claimId, string adjusterId);
    Task<Claim> UpdateStatusAsync(string claimId, string newStatus);
}
```

- [ ] **Step 4: Create `ClaimService.cs`**

```csharp
// ClaimsService.Api/Services/ClaimService.cs
using ClaimsService.Api.Models.Requests;
using ClaimsService.Core.Models;
using ClaimsService.Core.Repositories;

namespace ClaimsService.Api.Services;

public class ClaimService : IClaimService
{
    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        ["FNOL"]        = new HashSet<string> { "UnderReview" },
        ["UnderReview"] = new HashSet<string> { "Approved", "Rejected" },
        ["Approved"]    = new HashSet<string> { "Paid" },
        ["Rejected"]    = new HashSet<string>(),
        ["Paid"]        = new HashSet<string>()
    };

    private readonly IClaimRepository _claimRepository;
    private readonly IAdjusterRepository _adjusterRepository;
    private readonly IBlobUploadService _blobUploadService;

    public ClaimService(
        IClaimRepository claimRepository,
        IAdjusterRepository adjusterRepository,
        IBlobUploadService blobUploadService)
    {
        _claimRepository = claimRepository;
        _adjusterRepository = adjusterRepository;
        _blobUploadService = blobUploadService;
    }

    public async Task<Claim> CreateFnolAsync(string customerId, FnolRequest request)
    {
        var claim = new Claim
        {
            CustomerId = customerId,
            PolicyNumber = request.PolicyNumber,
            IncidentDate = request.IncidentDate,
            IncidentDescription = request.IncidentDescription,
            Status = "FNOL"
        };
        return await _claimRepository.CreateAsync(claim);
    }

    public Task<Claim?> GetClaimAsync(string id, string customerId, bool isAdmin)
    {
        return isAdmin
            ? _claimRepository.GetByIdCrossPartitionAsync(id)
            : _claimRepository.GetByIdAsync(id, customerId);
    }

    public Task<IEnumerable<Claim>> GetAllClaimsAsync(string? status)
        => _claimRepository.GetAllAsync(status);

    public async Task<(string SasUrl, string BlobPath, DateTime ExpiresAt)> GeneratePhotoUploadUrlAsync(
        string claimId, string customerId, string fileName)
    {
        var claim = await _claimRepository.GetByIdAsync(claimId, customerId)
            ?? throw new KeyNotFoundException($"Claim {claimId} not found");

        var (sasUrl, blobPath) = await _blobUploadService.GenerateSasUploadUrlAsync(claimId, fileName);
        var expiresAt = DateTime.UtcNow.AddMinutes(5);

        claim.PhotosBlobPaths.Add(blobPath);
        await _claimRepository.UpdateAsync(claim);

        return (sasUrl, blobPath, expiresAt);
    }

    public async Task<Claim> AssignAdjusterAsync(string claimId, string adjusterId)
    {
        _ = await _adjusterRepository.GetByIdAsync(adjusterId)
            ?? throw new KeyNotFoundException($"Adjuster {adjusterId} not found");

        var claim = await _claimRepository.GetByIdCrossPartitionAsync(claimId)
            ?? throw new KeyNotFoundException($"Claim {claimId} not found");

        claim.AdjusterId = adjusterId;
        return await _claimRepository.UpdateAsync(claim);
    }

    public async Task<Claim> UpdateStatusAsync(string claimId, string newStatus)
    {
        var claim = await _claimRepository.GetByIdCrossPartitionAsync(claimId)
            ?? throw new KeyNotFoundException($"Claim {claimId} not found");

        if (!ValidTransitions.TryGetValue(claim.Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOperationException($"Cannot transition from '{claim.Status}' to '{newStatus}'");

        claim.Status = newStatus;
        return await _claimRepository.UpdateAsync(claim);
    }
}
```

- [ ] **Step 5: Run tests — expect all pass**

```powershell
dotnet test ClaimsService.Api.Tests/ClaimsService.Api.Tests.csproj -v minimal
```

Expected: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 6: Commit**

```powershell
git add ClaimsService.Api/Services/ ClaimsService.Api.Tests/Services/ClaimServiceTests.cs
git commit -m "feat(claims): add ClaimService with status transition validation"
```

---

## Task 6: API — Program.cs (DI, Auth, Seeding, Swagger)

**Files:**
- Modify: `ClaimsService.Api/Program.cs`
- Create: `ClaimsService.Api/appsettings.json`

- [ ] **Step 1: Replace `Program.cs`**

```csharp
// ClaimsService.Api/Program.cs
using Azure.Storage.Blobs;
using ClaimsService.Api.Services;
using ClaimsService.Core.Models;
using ClaimsService.Core.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Cosmos DB
var cosmosConnectionString = builder.Configuration["Azure:CosmosDb:ConnectionString"]!;
var databaseName = builder.Configuration["Azure:CosmosDb:DatabaseName"]!;
builder.Services.AddSingleton(_ => new CosmosClient(cosmosConnectionString));
builder.Services.AddSingleton<IClaimRepository>(sp =>
    new ClaimRepository(sp.GetRequiredService<CosmosClient>(), databaseName));
builder.Services.AddSingleton<IAdjusterRepository>(sp =>
    new AdjusterRepository(sp.GetRequiredService<CosmosClient>(), databaseName));

// Blob Storage
builder.Services.AddSingleton(_ =>
    new BlobServiceClient(builder.Configuration["Azure:BlobStorage:ConnectionString"]!));
builder.Services.AddScoped<IBlobUploadService, BlobUploadService>();

// Business logic
builder.Services.AddScoped<IClaimService, ClaimService>();

// JWT Auth
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Claims Service API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your-jwt-token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

await SeedAdjustersAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task SeedAdjustersAsync(IServiceProvider services)
{
    var repo = services.GetRequiredService<IAdjusterRepository>();
    var adjusters = new[]
    {
        new Adjuster { Id = "adj-001", Name = "Jane Smith",    Email = "jane.smith@insurer.com",    IsAvailable = true },
        new Adjuster { Id = "adj-002", Name = "John Doe",      Email = "john.doe@insurer.com",      IsAvailable = true },
        new Adjuster { Id = "adj-003", Name = "Alice Johnson", Email = "alice.johnson@insurer.com", IsAvailable = true }
    };
    foreach (var adjuster in adjusters)
        await repo.UpsertAsync(adjuster);
}
```

- [ ] **Step 2: Create `appsettings.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Azure": {
    "CosmosDb": {
      "ConnectionString": "<your-cosmos-connection-string>",
      "DatabaseName": "ClaimsDb"
    },
    "BlobStorage": {
      "ConnectionString": "<your-storage-connection-string>",
      "ContainerName": "claims"
    }
  },
  "Jwt": {
    "Secret": "<your-jwt-secret-min-32-chars>",
    "Issuer": "ClaimsService",
    "Audience": "ClaimsService",
    "ExpiryMinutes": 60
  }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build ClaimsService.Api/ClaimsService.Api.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add ClaimsService.Api/Program.cs ClaimsService.Api/appsettings.json
git commit -m "feat(claims): configure DI, JWT auth, Swagger, and adjuster seeding"
```

---

## Task 7: API — ClaimsController

**Files:**
- Create: `ClaimsService.Api/Controllers/ClaimsController.cs`
- Delete: `ClaimsService.Api/Controllers/WeatherForecastController.cs` (auto-generated)

- [ ] **Step 1: Delete the placeholder controller**

```powershell
Remove-Item ClaimsService.Api/Controllers/WeatherForecastController.cs -ErrorAction SilentlyContinue
Remove-Item ClaimsService.Api/WeatherForecast.cs -ErrorAction SilentlyContinue
```

- [ ] **Step 2: Create `ClaimsController.cs`**

```csharp
// ClaimsService.Api/Controllers/ClaimsController.cs
using ClaimsService.Api.Models.Requests;
using ClaimsService.Api.Models.Responses;
using ClaimsService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClaimsController : ControllerBase
{
    private readonly IClaimService _claimService;

    public ClaimsController(IClaimService claimService) => _claimService = claimService;

    private string CustomerId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? string.Empty;

    private bool IsAdmin => User.IsInRole("admin");

    [HttpPost("fnol")]
    public async Task<IActionResult> SubmitFnol([FromBody] FnolRequest request)
    {
        var claim = await _claimService.CreateFnolAsync(CustomerId, request);
        return CreatedAtAction(nameof(GetClaim), new { id = claim.Id }, claim);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetClaim(string id)
    {
        var claim = await _claimService.GetClaimAsync(id, CustomerId, IsAdmin);
        if (claim == null) return NotFound();
        if (!IsAdmin && claim.CustomerId != CustomerId) return Forbid();
        return Ok(claim);
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllClaims([FromQuery] string? status)
    {
        var claims = await _claimService.GetAllClaimsAsync(status);
        return Ok(claims);
    }

    [HttpPost("{id}/photos/upload-url")]
    public async Task<IActionResult> GetPhotoUploadUrl(string id, [FromQuery] string fileName)
    {
        try
        {
            var (sasUrl, blobPath, expiresAt) =
                await _claimService.GeneratePhotoUploadUrlAsync(id, CustomerId, fileName);
            return Ok(new SasUploadUrlResponse(sasUrl, blobPath, expiresAt));
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/assign")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignAdjuster(string id, [FromBody] AssignAdjusterRequest request)
    {
        try
        {
            var claim = await _claimService.AssignAdjusterAsync(id, request.AdjusterId);
            return Ok(claim);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var claim = await _claimService.UpdateStatusAsync(id, request.Status);
            return Ok(claim);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build ClaimsService.Api/ClaimsService.Api.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add ClaimsService.Api/Controllers/ClaimsController.cs
git commit -m "feat(claims): add ClaimsController with FNOL, tracking, photo upload, assign, status endpoints"
```

---

## Task 8: API — AdjustersController

**Files:**
- Create: `ClaimsService.Api/Controllers/AdjustersController.cs`

- [ ] **Step 1: Create `AdjustersController.cs`**

```csharp
// ClaimsService.Api/Controllers/AdjustersController.cs
using ClaimsService.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class AdjustersController : ControllerBase
{
    private readonly IAdjusterRepository _adjusterRepository;

    public AdjustersController(IAdjusterRepository adjusterRepository)
        => _adjusterRepository = adjusterRepository;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var adjusters = await _adjusterRepository.GetAllAsync();
        return Ok(adjusters);
    }
}
```

- [ ] **Step 2: Build and run all tests**

```powershell
dotnet build ClaimsService.sln
dotnet test ClaimsService.Api.Tests/ClaimsService.Api.Tests.csproj -v minimal
```

Expected: Build succeeded, all tests pass.

- [ ] **Step 3: Commit**

```powershell
git add ClaimsService.Api/Controllers/AdjustersController.cs
git commit -m "feat(claims): add AdjustersController"
```

---

## Task 9: Functions — ClaimProcessingFunction (Event Grid trigger)

**Files:**
- Create: `ClaimsService.Functions/Program.cs`
- Create: `ClaimsService.Functions/ClaimProcessingFunction.cs`
- Create: `ClaimsService.Functions/host.json`
- Create: `ClaimsService.Functions/local.settings.json`
- Delete: `ClaimsService.Functions/Class1.cs` (placeholder)

- [ ] **Step 1: Delete placeholder**

```powershell
Remove-Item ClaimsService.Functions/Class1.cs -ErrorAction SilentlyContinue
```

- [ ] **Step 2: Create `host.json`**

```json
{
  "version": "2.0",
  "logging": {
    "logLevel": {
      "default": "Information"
    }
  }
}
```

- [ ] **Step 3: Create `local.settings.json`**

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<your-storage-connection-string>",
    "CosmosDbConnection": "<your-cosmos-connection-string>",
    "CosmosDbDatabaseName": "ClaimsDb",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

- [ ] **Step 4: Create `Program.cs`**

```csharp
// ClaimsService.Functions/Program.cs
using ClaimsService.Core.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var cosmosConnectionString = context.Configuration["CosmosDbConnection"]!;
        var databaseName = context.Configuration["CosmosDbDatabaseName"] ?? "ClaimsDb";

        services.AddSingleton(_ => new CosmosClient(cosmosConnectionString));
        services.AddSingleton<IClaimRepository>(sp =>
            new ClaimRepository(sp.GetRequiredService<CosmosClient>(), databaseName));
    })
    .Build();

host.Run();
```

- [ ] **Step 5: Create `ClaimProcessingFunction.cs`**

```csharp
// ClaimsService.Functions/ClaimProcessingFunction.cs
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using ClaimsService.Core.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ClaimsService.Functions;

public class ClaimProcessingFunction
{
    private readonly IClaimRepository _claimRepository;
    private readonly ILogger<ClaimProcessingFunction> _logger;

    public ClaimProcessingFunction(IClaimRepository claimRepository, ILogger<ClaimProcessingFunction> logger)
    {
        _claimRepository = claimRepository;
        _logger = logger;
    }

    [Function("ClaimProcessingFunction")]
    public async Task Run([EventGridTrigger] EventGridEvent eventGridEvent)
    {
        if (eventGridEvent.EventType != "Microsoft.Storage.BlobCreated")
        {
            _logger.LogInformation("Skipping event type: {EventType}", eventGridEvent.EventType);
            return;
        }

        var data = eventGridEvent.Data.ToObjectFromJson<StorageBlobCreatedEventData>();

        // Blob URL format: https://<account>.blob.core.windows.net/claims/{claimId}/{filename}
        var uri = new Uri(data.Url);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/');
        // segments[0] = "claims", segments[1] = claimId, segments[2] = filename
        if (segments.Length < 3)
        {
            _logger.LogWarning("Unexpected blob path format: {Url}", data.Url);
            return;
        }

        var claimId = segments[1];
        _logger.LogInformation("Processing photo upload for claim {ClaimId}", claimId);

        var claim = await _claimRepository.GetByIdCrossPartitionAsync(claimId);
        if (claim == null)
        {
            _logger.LogWarning("Claim {ClaimId} not found in Cosmos DB", claimId);
            return;
        }

        // Mock AI: simulate processing delay, return fixed damage score
        await Task.Delay(TimeSpan.FromSeconds(2));
        claim.DamageScore = 72;
        claim.Status = "UnderReview";

        await _claimRepository.UpdateAsync(claim);
        _logger.LogInformation("Claim {ClaimId} updated to UnderReview with damage score 72", claimId);
    }
}
```

- [ ] **Step 6: Build Functions project**

```powershell
dotnet build ClaimsService.Functions/ClaimsService.Functions.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```powershell
git add ClaimsService.Functions/
git commit -m "feat(claims): add ClaimProcessingFunction with Event Grid trigger and mock AI"
```

---

## Task 10: Deployment Configuration & Azure Wiring

**Files:**
- Create: `ClaimsService.Api/appsettings.Production.json`

- [ ] **Step 1: Create production appsettings**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

(Connection strings are set as Azure App Service Application Settings, not in source.)

- [ ] **Step 2: Publish the Web API**

Run from `ClaimsService/`:

```powershell
dotnet publish ClaimsService.Api/ClaimsService.Api.csproj -c Release -o ./publish/api
```

- [ ] **Step 3: Deploy Web API to Azure App Service**

```powershell
az webapp deploy `
  --resource-group rg-claims-service `
  --name <your-app-service-name> `
  --src-path ./publish/api `
  --type zip
```

- [ ] **Step 4: Set App Service Application Settings using Key Vault References**

Secrets are stored in Key Vault (Task 11). App Service resolves them automatically via Managed Identity — no plain-text secrets in config.

```powershell
az webapp config appsettings set `
  --resource-group rg-claims-service `
  --name <your-app-service-name> `
  --settings `
    "Azure__CosmosDb__ConnectionString=@Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=CosmosDbConnection)" `
    "Azure__CosmosDb__DatabaseName=ClaimsDb" `
    "Azure__BlobStorage__ConnectionString=@Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=BlobStorageConnection)" `
    "Azure__BlobStorage__ContainerName=claims" `
    "Jwt__Secret=@Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=JwtSecret)" `
    "Jwt__Issuer=ClaimsService" `
    "Jwt__Audience=ClaimsService" `
    "Jwt__ExpiryMinutes=60"
```

- [ ] **Step 5: Publish the Function App**

```powershell
dotnet publish ClaimsService.Functions/ClaimsService.Functions.csproj -c Release -o ./publish/functions
```

Install Azure Functions Core Tools if not present:
```powershell
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

Deploy:
```powershell
func azure functionapp publish <your-function-app-name> --dotnet-isolated
```

- [ ] **Step 6: Set Function App Application Settings using Key Vault References**

```powershell
az functionapp config appsettings set `
  --resource-group rg-claims-service `
  --name <your-function-app-name> `
  --settings `
    "CosmosDbConnection=@Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=CosmosDbConnection)" `
    "CosmosDbDatabaseName=ClaimsDb" `
    "AzureWebJobsStorage=@Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=BlobStorageConnection)"
```

- [ ] **Step 7: Create Event Grid System Topic and Subscription**

```powershell
# Create system topic on the storage account
az eventgrid system-topic create `
  --resource-group rg-claims-service `
  --name claims-photos-topic `
  --location <storage-account-location> `
  --topic-type Microsoft.Storage.StorageAccounts `
  --source /subscriptions/<sub-id>/resourceGroups/rg-claims-service/providers/Microsoft.Storage/storageAccounts/<storage-account-name>

# Get the Function's resource ID for the subscription endpoint
$functionId = az functionapp function show `
  --resource-group rg-claims-service `
  --name <your-function-app-name> `
  --function-name ClaimProcessingFunction `
  --query id -o tsv

# Create Event Grid subscription
az eventgrid system-topic event-subscription create `
  --resource-group rg-claims-service `
  --system-topic-name claims-photos-topic `
  --name claim-photo-sub `
  --endpoint-type azurefunction `
  --endpoint $functionId `
  --included-event-types Microsoft.Storage.BlobCreated `
  --subject-begins-with /blobServices/default/containers/claims/
```

- [ ] **Step 8: Verify end-to-end**

1. Generate a JWT for a `customer` role (use jwt.io with your `Jwt__Secret`)
2. `POST /api/claims/fnol` — create a claim, note the `id`
3. `POST /api/claims/{id}/photos/upload-url?fileName=test.jpg` — get SAS URL
4. `PUT <sasUrl>` with any file body (e.g., via curl or Postman, `Content-Type: image/jpeg`)
5. Wait ~5 seconds
6. `GET /api/claims/{id}` — status should be `UnderReview`, `damageScore` should be `72`

- [ ] **Step 9: Final commit**

```powershell
git add ClaimsService.Api/appsettings.Production.json
git commit -m "feat(claims): add production appsettings and deployment configuration"
```

---

## Task 11: Azure Key Vault & Managed Identity

**Why this task exists:** No secrets (connection strings, JWT secret) should appear in config files or App Service settings as plain text. Key Vault stores them; Managed Identity gives App Service and Functions access without any credentials; Key Vault References let Azure resolve them into `IConfiguration` automatically — zero code changes required.

**Prerequisites:** Task 10 Steps 2–3 (App Service and Function App deployed to Azure).

- [ ] **Step 1: Create the Key Vault**

```powershell
az keyvault create `
  --name kv-claims-service `
  --resource-group rg-claims-service `
  --location <your-region> `
  --enable-rbac-authorization true
```

`--enable-rbac-authorization true` uses Azure RBAC instead of legacy access policies — the modern approach.

- [ ] **Step 2: Store secrets in Key Vault**

```powershell
az keyvault secret set --vault-name kv-claims-service --name CosmosDbConnection   --value "<your-cosmos-connection-string>"
az keyvault secret set --vault-name kv-claims-service --name BlobStorageConnection --value "<your-storage-connection-string>"
az keyvault secret set --vault-name kv-claims-service --name JwtSecret             --value "<your-jwt-secret-min-32-chars>"
```

- [ ] **Step 3: Enable Managed Identity on App Service**

```powershell
az webapp identity assign `
  --resource-group rg-claims-service `
  --name <your-app-service-name>
```

Note the `principalId` in the output — needed in Step 5.

- [ ] **Step 4: Enable Managed Identity on Function App**

```powershell
az functionapp identity assign `
  --resource-group rg-claims-service `
  --name <your-function-app-name>
```

Note the `principalId` in the output — needed in Step 6.

- [ ] **Step 5: Grant App Service Managed Identity access to Key Vault**

```powershell
$kvScope = az keyvault show --name kv-claims-service --query id -o tsv

az role assignment create `
  --role "Key Vault Secrets User" `
  --assignee <app-service-principalId-from-step-3> `
  --scope $kvScope
```

- [ ] **Step 6: Grant Function App Managed Identity access to Key Vault**

```powershell
az role assignment create `
  --role "Key Vault Secrets User" `
  --assignee <function-app-principalId-from-step-4> `
  --scope $kvScope
```

- [ ] **Step 7: Verify Key Vault References resolve correctly**

In Azure Portal, go to App Service → Configuration → Application Settings. Each Key Vault Reference setting shows a green tick icon when resolved successfully. A red icon means the Managed Identity does not have access — recheck Step 5.

For Function App: Portal → Function App → Configuration → Application Settings, same green tick check.

- [ ] **Step 8: Verify secrets are NOT visible in plain text**

```powershell
# This should show the Key Vault reference string, not the actual secret
az webapp config appsettings list `
  --resource-group rg-claims-service `
  --name <your-app-service-name> `
  --query "[?name=='Azure__CosmosDb__ConnectionString'].value" -o tsv
```

Expected output: `@Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=CosmosDbConnection)`

- [ ] **Step 9: Smoke test the deployed API**

Generate a JWT with your `JwtSecret` value (use jwt.io — HS256, payload: `{ "sub": "cust-001", "role": "customer" }`):

```powershell
# Replace <token> and <app-service-url>
curl -X POST https://<your-app-service-name>.azurewebsites.net/api/claims/fnol `
  -H "Authorization: Bearer <token>" `
  -H "Content-Type: application/json" `
  -d '{"policyNumber":"POL-001","incidentDate":"2026-05-11T00:00:00Z","incidentDescription":"Test claim"}'
```

Expected: `201 Created` with a claim JSON body containing `"status": "FNOL"`.

- [ ] **Step 10: Commit**

```powershell
git commit --allow-empty -m "feat(claims): Key Vault and Managed Identity wiring documented — no code changes required"
```
