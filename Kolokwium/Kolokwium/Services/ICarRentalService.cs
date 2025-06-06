using Kolokwium.Models.DTOs;

namespace Kolokwium.Services;

public interface ICarRentalService
{
    Task<ClientRentalsDto> GetClientRentalsAsync(int clientId);
    Task<int> AddClientWithRentalAsync(NewClientRentalDto newClientRental);
}