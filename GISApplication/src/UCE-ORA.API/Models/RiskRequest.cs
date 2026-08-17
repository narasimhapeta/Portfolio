using System.ComponentModel.DataAnnotations;

namespace UCE_ORA.API.Models;

public class RiskRequest
{
    [Required]
    public double Lat { get; set; }

    [Required]
    public double Lng { get; set; }

    [Required]
    [Range(10, 50000, ErrorMessage = "Radius must be between 10 and 50,000 meters.")]
    public double RadiusMeters { get; set; }
}
