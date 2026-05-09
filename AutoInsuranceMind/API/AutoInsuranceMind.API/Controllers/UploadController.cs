using AutoInsuranceMind.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsuranceMind.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly DocumentService _documentService;
    private readonly ILogger<UploadController> _logger;

    public UploadController(DocumentService documentService, ILogger<UploadController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> UploadDocument(
        IFormFile file,
        [FromQuery] string customerId = "cust-001",
        [FromQuery] string? policyId = null)
    {
        try
        {
            var document = await _documentService.ProcessUploadAsync(file, customerId, policyId);
            return Ok(new
            {
                success = true,
                message = "Document uploaded and indexed successfully",
                document = new
                {
                    document.Id,
                    document.FileName,
                    document.FileType,
                    document.FileSize,
                    document.Status,
                    document.UploadedAt
                }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for file {FileName}: {Error}", file?.FileName, ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message, step = ex.InnerException?.Message });
        }
    }

    [HttpGet("documents")]
    public IActionResult GetDocuments([FromQuery] string customerId = "cust-001")
    {
        var documents = _documentService.GetDocumentsByCustomer(customerId);
        return Ok(new { documents, total = documents.Count });
    }

    [HttpGet("documents/{id}")]
    public IActionResult GetDocument(string id)
    {
        var document = _documentService.GetDocument(id);
        return document == null
            ? NotFound(new { message = "Document not found" })
            : Ok(document);
    }

    [HttpDelete("documents/{id}")]
    public IActionResult DeleteDocument(string id)
    {
        var deleted = _documentService.DeleteDocument(id);
        return deleted
            ? Ok(new { success = true, message = "Document deleted" })
            : NotFound(new { message = "Document not found" });
    }

    [HttpGet("documents/{id}/text")]
    public IActionResult GetDocumentText(string id)
    {
        var document = _documentService.GetDocument(id);
        if (document == null)
            return NotFound(new { message = "Document not found" });

        return Ok(new
        {
            document.Id,
            document.FileName,
            document.Status,
            extractedTextLength = document.ExtractedText?.Length ?? 0,
            extractedText = document.ExtractedText
        });
    }
}
