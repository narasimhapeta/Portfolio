using NetTopologySuite.Geometries;
using UCE_ORA.API.Services;

namespace UCE_ORA.API.Tests;

public class SpatialAnalysisServiceTests
{
    private readonly SpatialAnalysisService _service = new();

    // Tests go here
    [Fact]
    public void LineInsideBuffer_ReturnsTrue()
    {
        var wkt = "LINESTRING(-96.7970 32.7767, -96.8350 32.9529)"; // DFW-C1
        var result = _service.IsLineAtRisk(wkt, hazardLat: 32.7767, hazardLng: -96.7970, bufferMeters: 100);
        Assert.True(result);
    }

    [Fact]
    public void LineFarFromBuffer_ReturnsFalse()
    {
        var wkt = "LINESTRING(-96.7970 32.7767, -96.8350 32.9529)"; // DFW-C1
        var result = _service.IsLineAtRisk(wkt, hazardLat: 33.5000, hazardLng: -97.5000, bufferMeters: 100);
        Assert.False(result);
    }

    [Fact]
    public void BufferRadiusDeterminesRisk()
    {
        var wkt = "LINESTRING(-97.3208 32.7254, -97.7964 32.7590)"; // DFW-W1
                                                                    // Hazard point ~400m directly north of the line's start
        var atRisk = _service.IsLineAtRisk(wkt, hazardLat: 32.7290, hazardLng: -97.3208, bufferMeters: 600);
        var notAtRisk = _service.IsLineAtRisk(wkt, hazardLat: 32.7290, hazardLng: -97.3208, bufferMeters: 200);
        Assert.True(atRisk);
        Assert.False(notAtRisk);
    }


    [Fact]
    public void EmptyWkt_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.IsLineAtRisk(string.Empty, hazardLat: 32.7767, hazardLng: -96.7970, bufferMeters: 100));
    }




}
