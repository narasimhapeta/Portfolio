using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace UCE_ORA.API.Services;

public class SpatialAnalysisService
{
    private readonly GeometryFactory _factory =
        new GeometryFactory(new PrecisionModel(), 4326);

    public bool IsLineAtRisk(string lineWkt, double hazardLat, double hazardLng, double bufferMeters)
    {
        if (string.IsNullOrWhiteSpace(lineWkt))
            throw new ArgumentException("Line WKT cannot be empty.", nameof(lineWkt));

        var reader = new WKTReader(_factory);
        var line = reader.Read(lineWkt);

        var hazardPoint = _factory.CreatePoint(new Coordinate(hazardLng, hazardLat));
        var bufferZone = hazardPoint.Buffer(bufferMeters / 111_000.0);

        return line.Intersects(bufferZone);
    }
}
