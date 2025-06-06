namespace Kolokwium.Models.DTOs;

public class RentalDto
{
    public string Vin { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int TotalPrice { get; set; }
}