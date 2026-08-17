using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UCE_ORA.API.Models;

[Table("TRANSMISSION_LINES")]
public class TransmissionLine
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("NAME")]
    public string Name { get; set; } = string.Empty;

    [Column("VOLTAGE_KV")]
    public decimal VoltageKv { get; set; }

    [Column("LINE_WKT")]
    public string LineWkt { get; set; } = string.Empty;
}
