namespace Kolokwium.Models.DTOs;

public class NewClientRentalDto
{
    public ClientDto Client { get; set; } = new ClientDto();
    public int CarId { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
}