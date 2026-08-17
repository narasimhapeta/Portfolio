using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.IO;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using UCE_ORA.API.Data;
using UCE_ORA.API.Models;
using UCE_ORA.API.Services;

namespace UCE_ORA.API.Controllers;

[ApiController]
[Route("api/infrastructure")]
public class InfrastructureController : ControllerBase
{
    private readonly TransmissionLineRepository _repo;
    private readonly SpatialAnalysisService _spatial;
    private readonly GeometryFactory _factory =
        new GeometryFactory(new PrecisionModel(), 4326);

    public InfrastructureController(
        TransmissionLineRepository repo,
        SpatialAnalysisService spatial)
    {
        _repo = repo;
        _spatial = spatial;
    }

    // GET /api/infrastructure/lines
    [HttpGet("lines")]
    public async Task<IActionResult> GetLines()
    {      

        var lines = await _repo.GetAllAsync();
        var json = SerializeToGeoJson(lines);
        return Content(json, "application/json");
    }

    // GET /api/infrastructure/risk-assessment?lat=&lng=&radiusMeters=
    [HttpGet("risk-assessment")]
    public async Task<IActionResult> GetRiskAssessment([FromQuery] RiskRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var allLines = await _repo.GetAllAsync();

        var threatened = allLines
            .Where(l => _spatial.IsLineAtRisk(l.LineWkt, request.Lat, request.Lng, request.RadiusMeters))
            .ToList();

        var result = new RiskResult
        {
            HazardLat = request.Lat,
            HazardLng = request.Lng,
            RadiusMeters = request.RadiusMeters,
            TotalLinesChecked = allLines.Count,
            TotalThreatenedLines = threatened.Count,
            ThreatenedLines = threatened,
            ThreatenedLinesGeoJson = SerializeToGeoJson(threatened)
        };

        return Ok(result);
    }

    private FeatureCollection BuildFeatureCollection(List<TransmissionLine> lines)
    {
        var reader = new WKTReader(_factory);
        var collection = new FeatureCollection();

        foreach (var line in lines)
        {
            var geometry = reader.Read(line.LineWkt);
            var attributes = new AttributesTable();
            attributes.Add("id", line.Id);
            attributes.Add("name", line.Name);
            attributes.Add("voltage_kv", line.VoltageKv);
            collection.Add(new Feature(geometry, attributes));
        }

        return collection;
    }

    private string SerializeToGeoJson(List<TransmissionLine> lines)
    {
        if (!lines.Any()) return "{}";
        var collection = BuildFeatureCollection(lines);
        var serializer = GeoJsonSerializer.Create();
        using var writer = new StringWriter();
        serializer.Serialize(writer, collection);
        return writer.ToString();
    }
}
