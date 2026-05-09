using AutoInsuranceMind.API.Data;
using AutoInsuranceMind.API.Models;
using Azure;
using Azure.AI.DocumentIntelligence;
using UglyToad.PdfPig;
using System.Text;

namespace AutoInsuranceMind.API.Services;

public class DocumentService
{
    private readonly string _uploadFolder;
    private readonly ILogger<DocumentService> _logger;
    private readonly AzureBlobService _blobService;
    private readonly EmbeddingService _embeddingService;
    private readonly AzureSearchService _searchService;
    private readonly DocumentIntelligenceClient? _docIntelligenceClient;
    private readonly bool _useAzurePipeline;

    public DocumentService(
        ILogger<DocumentService> logger,
        IConfiguration config,
        AzureBlobService blobService,
        EmbeddingService embeddingService,
        AzureSearchService searchService)
    {
        _logger = logger;
        _blobService = blobService;
        _embeddingService = embeddingService;
        _searchService = searchService;

        _uploadFolder = config["UploadFolder"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(_uploadFolder);

        var docIntelEndpoint = config["AzureDocumentIntelligence:Endpoint"] ?? string.Empty;
        var docIntelKey = config["AzureDocumentIntelligence:ApiKey"] ?? string.Empty;
        bool isPlaceholder(string s) => string.IsNullOrWhiteSpace(s) || s.StartsWith("YOUR_");

        if (!isPlaceholder(docIntelEndpoint) && !isPlaceholder(docIntelKey))
        {
            _docIntelligenceClient = new DocumentIntelligenceClient(
                new Uri(docIntelEndpoint), new AzureKeyCredential(docIntelKey));
        }

        // Full Azure pipeline requires: Blob + Embeddings + Search
        _useAzurePipeline = blobService.IsConfigured && embeddingService.IsConfigured && searchService.IsConfigured;

        _logger.LogInformation(
            "DocumentService mode: {Mode} | DocIntelligence: {DocIntel}",
            _useAzurePipeline ? "Azure" : "Local",
            _docIntelligenceClient != null ? "enabled" : "local PdfPig");
    }

    public async Task<UploadedDocument> ProcessUploadAsync(
        IFormFile file, string customerId = "cust-001", string? policyId = null)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        var allowedTypes = new[] { ".pdf", ".txt", ".docx", ".doc" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedTypes.Contains(ext))
            throw new ArgumentException($"File type '{ext}' not supported. Use PDF, DOCX, or TXT.");

        if (file.Length > 10 * 1024 * 1024)
            throw new ArgumentException("File exceeds 10 MB limit.");

        return _useAzurePipeline
            ? await ProcessWithAzurePipelineAsync(file, customerId, policyId, ext)
            : await ProcessLocallyAsync(file, customerId, policyId, ext);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AZURE PIPELINE: Blob Storage → Document Intelligence → Embeddings → Search
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<UploadedDocument> ProcessWithAzurePipelineAsync(
        IFormFile file, string customerId, string? policyId, string ext)
    {
        var documentId = Guid.NewGuid().ToString();
        var blobName = $"{customerId}/{documentId}{ext}";

        _logger.LogInformation("Starting Azure pipeline for {FileName}", file.FileName);

        // Buffer file bytes FIRST — IFormFile stream can only be read once
        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            fileBytes = ms.ToArray();
        }
        _logger.LogInformation("Buffered {Bytes} bytes for {FileName}", fileBytes.Length, file.FileName);

        // Step 1: Upload to Blob Storage
        await _blobService.EnsureContainerExistsAsync();
        string blobUrl;
        using (var blobStream = new MemoryStream(fileBytes))
        {
            blobUrl = await _blobService.UploadAsync(blobStream, blobName, file.ContentType ?? "application/octet-stream");
        }
        _logger.LogInformation("Step 1 complete — blob uploaded: {Url}", blobUrl);

        // Step 2: Extract text (Document Intelligence if available, else local PdfPig)
        var extractedText = await ExtractTextWithAzureOrFallbackAsync(fileBytes, ext, file.FileName);
        _logger.LogInformation("Step 2 complete — extracted {Chars} chars", extractedText.Length);

        // Step 3: Chunk text
        var textChunks = _embeddingService.ChunkText(extractedText);
        _logger.LogInformation("Step 3 complete — {Count} chunks created", textChunks.Count);

        // Step 4: Generate embeddings for each chunk
        await _searchService.EnsureIndexExistsAsync(_embeddingService.Dimensions);
        var chunksWithVectors = new List<(string Text, float[] Vector, int Index)>();
        for (var i = 0; i < textChunks.Count; i++)
        {
            var vector = await _embeddingService.GenerateEmbeddingAsync(textChunks[i]);
            chunksWithVectors.Add((textChunks[i], vector, i));
        }
        _logger.LogInformation("Step 4 complete — embeddings generated for {Count} chunks", chunksWithVectors.Count);

        // Step 5: Index in Azure Cognitive Search
        await _searchService.IndexChunksAsync(documentId, file.FileName, customerId, chunksWithVectors);
        _logger.LogInformation("Step 5 complete — indexed in Azure Cognitive Search");

        var document = new UploadedDocument
        {
            Id = documentId,
            CustomerId = customerId,
            PolicyId = policyId,
            FileName = file.FileName,
            FileType = ext,
            FilePath = blobUrl,   // blob URL instead of local path
            FileSize = file.Length,
            ExtractedText = extractedText[..Math.Min(5000, extractedText.Length)], // preview for diagnostics
            UploadedAt = DateTime.UtcNow,
            Status = "indexed"
        };

        MockDataStore.Documents.Add(document);
        return document;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LOCAL PIPELINE: Local disk → PdfPig → In-memory keyword store
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<UploadedDocument> ProcessLocallyAsync(
        IFormFile file, string customerId, string? policyId, string ext)
    {
        var safeFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(_uploadFolder, safeFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var extractedText = await ExtractTextLocalAsync(filePath, ext);
        _logger.LogInformation("Local extraction: {Chars} chars from {File}", extractedText.Length, file.FileName);

        var document = new UploadedDocument
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            PolicyId = policyId,
            FileName = file.FileName,
            FileType = ext,
            FilePath = filePath,
            FileSize = file.Length,
            ExtractedText = extractedText,
            UploadedAt = DateTime.UtcNow,
            Status = "indexed"
        };

        MockDataStore.Documents.Add(document);
        return document;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TEXT EXTRACTION HELPERS
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<string> ExtractTextWithAzureOrFallbackAsync(byte[] fileBytes, string ext, string fileName)
    {
        if (_docIntelligenceClient != null)
        {
            try
            {
                var operation = await _docIntelligenceClient.AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    "prebuilt-layout",
                    BinaryData.FromBytes(fileBytes));

                var result = operation.Value;

                // Build text from paragraphs first (preserves structure better than raw Content)
                var sb = new StringBuilder();
                if (result.Paragraphs?.Count > 0)
                {
                    foreach (var para in result.Paragraphs)
                        sb.AppendLine(para.Content);
                }

                // Fall back to page-level lines if no paragraphs
                if (sb.Length == 0 && result.Pages?.Count > 0)
                {
                    foreach (var page in result.Pages)
                        foreach (var line in page.Lines ?? Enumerable.Empty<Azure.AI.DocumentIntelligence.DocumentLine>())
                            sb.AppendLine(line.Content);
                }

                // Final fallback: raw Content field
                if (sb.Length == 0)
                    sb.Append(result.Content ?? string.Empty);

                var text = sb.ToString().Trim();
                _logger.LogInformation(
                    "Azure Document Intelligence extracted {Chars} chars from {File} (paragraphs={Paras}, pages={Pages})",
                    text.Length, fileName, result.Paragraphs?.Count ?? 0, result.Pages?.Count ?? 0);
                _logger.LogInformation("Extracted text preview: {Preview}",
                    text.Length > 500 ? text[..500] + "…" : text);
                return text;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Document Intelligence failed for {File}: {Error}. Falling back to local.", fileName, ex.Message);
            }
        }

        // Fallback: local extraction
        var tmpPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");
        await File.WriteAllBytesAsync(tmpPath, fileBytes);
        try
        {
            return await ExtractTextLocalAsync(tmpPath, ext);
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }

    private async Task<string> ExtractTextLocalAsync(string filePath, string ext)
    {
        if (ext == ".txt")
            return await File.ReadAllTextAsync(filePath);

        if (ext == ".pdf")
            return await Task.Run(() => ExtractPdfText(filePath));

        if (ext is ".docx" or ".doc")
            return $"[Word document: {Path.GetFileName(filePath)}] For full DOCX extraction enable Azure Document Intelligence.";

        return "Document content could not be extracted.";
    }

    private string ExtractPdfText(string filePath)
    {
        try
        {
            using var pdf = PdfDocument.Open(filePath);
            var sb = new StringBuilder();
            foreach (var page in pdf.GetPages())
                sb.AppendLine(string.Join(" ", page.GetWords().Select(w => w.Text)));
            var text = sb.ToString().Trim();
            return text.Length > 0 ? text : "[PDF had no extractable text — may be a scanned image. Enable Azure Document Intelligence for OCR.]";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PdfPig extraction failed: {Error}", ex.Message);
            return $"[PDF extraction failed: {ex.Message}]";
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CRUD
    // ──────────────────────────────────────────────────────────────────────────
    public List<UploadedDocument> GetDocumentsByCustomer(string customerId)
        => MockDataStore.Documents.Where(d => d.CustomerId == customerId).ToList();

    public UploadedDocument? GetDocument(string id)
        => MockDataStore.Documents.FirstOrDefault(d => d.Id == id);

    public async Task<bool> DeleteDocumentAsync(string id)
    {
        var doc = MockDataStore.Documents.FirstOrDefault(d => d.Id == id);
        if (doc == null) return false;

        if (_useAzurePipeline)
        {
            // Delete from Azure Search
            await _searchService.DeleteDocumentChunksAsync(id);
            // Delete from Blob Storage (extract blob name from URL)
            try
            {
                var blobName = new Uri(doc.FilePath).AbsolutePath.TrimStart('/').Replace($"{_blobService.GetType().Name}/", "");
                await _blobService.DeleteAsync(blobName);
            }
            catch { /* blob name extraction may vary — log and continue */ }
        }
        else
        {
            if (File.Exists(doc.FilePath)) File.Delete(doc.FilePath);
        }

        MockDataStore.Documents.Remove(doc);
        return true;
    }

    // Keep synchronous overload for backward compatibility with UploadController
    public bool DeleteDocument(string id) => DeleteDocumentAsync(id).GetAwaiter().GetResult();
}
