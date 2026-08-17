namespace UCE_ORA.API.Models;

public class RiskResult
{
    public double HazardLat { get; set; }
    public double HazardLng { get; set; }
    public double RadiusMeters { get; set; }
    public int TotalLinesChecked { get; set; }
    public int TotalThreatenedLines { get; set; }
    public List<TransmissionLine> ThreatenedLines { get; set; } = new();
    public string ThreatenedLinesGeoJson { get; set; } = string.Empty;
}
