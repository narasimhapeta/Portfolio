namespace GridTrace.Models;

public class NetworkDevice
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty; // SUBSTATION, FEEDER, POLE, TRANSFORMER, METER
    public int? ParentId { get; set; }
    public double PosX { get; set; }
    public double PosY { get; set; }
    public string Status { get; set; } = "NORMAL"; // NORMAL or OUTAGE
}