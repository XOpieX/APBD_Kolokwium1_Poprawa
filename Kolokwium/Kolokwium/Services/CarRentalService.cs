using Kolokwium.Exceptions;
using Kolokwium.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace Kolokwium.Services;

public class CarRentalService : ICarRentalService
{
    private readonly string _connectionString;

    public CarRentalService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default") ?? string.Empty;
    }

    public async Task<ClientRentalsDto> GetClientRentalsAsync(int clientId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
                SELECT c.ID, c.FirstName, c.LastName, c.Address,
                       car.VIN, col.Name AS Color, m.Name AS Model,
                       cr.DateFrom, cr.DateTo, cr.TotalPrice
                FROM clients c
                LEFT JOIN car_rentals cr ON c.ID = cr.ClientID
                LEFT JOIN cars car ON cr.CarID = car.ID
                LEFT JOIN colors col ON car.ColorID = col.ID
                LEFT JOIN models m ON car.ModelID = m.ID
                WHERE c.ID = @ClientId";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@ClientId", clientId);

        await using var reader = await command.ExecuteReaderAsync();

        ClientRentalsDto? result = null;

        while (await reader.ReadAsync())
        {
            if (result == null)
            {
                result = new ClientRentalsDto
                {
                    Id = reader.GetInt32(0),
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2),
                    Address = reader.GetString(3),
                    Rentals = new List<RentalDto>()
                };
            }

            if (!reader.IsDBNull(4))
            {
                result.Rentals.Add(new RentalDto
                {
                    Vin = reader.GetString(4),
                    Color = reader.GetString(5),
                    Model = reader.GetString(6),
                    DateFrom = reader.GetDateTime(7),
                    DateTo = reader.GetDateTime(8),
                    TotalPrice = reader.GetInt32(9)
                });
            }
        }

        if (result == null)
        {
            throw new NotFoundException($"Client with ID {clientId} not found");
        }

        return result;
    }

    public async Task<int> AddClientWithRentalAsync(NewClientRentalDto newClientRental)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var carCheckQuery = "SELECT PricePerDay FROM cars WHERE ID = @CarId";
            await using var carCheckCommand = new SqlCommand(carCheckQuery, connection, (SqlTransaction)transaction);
            carCheckCommand.Parameters.AddWithValue("@CarId", newClientRental.CarId);

            var pricePerDay = await carCheckCommand.ExecuteScalarAsync();
            if (pricePerDay == null)
            {
                throw new NotFoundException($"Car with ID {newClientRental.CarId} not found");
            }

            var insertClientQuery = @"
                    INSERT INTO clients (FirstName, LastName, Address)
                    OUTPUT INSERTED.ID
                    VALUES (@FirstName, @LastName, @Address)";

            await using var insertClientCommand =
                new SqlCommand(insertClientQuery, connection, (SqlTransaction)transaction);
            insertClientCommand.Parameters.AddWithValue("@FirstName", newClientRental.Client.FirstName);
            insertClientCommand.Parameters.AddWithValue("@LastName", newClientRental.Client.LastName);
            insertClientCommand.Parameters.AddWithValue("@Address", newClientRental.Client.Address);

            var clientId = (int)await insertClientCommand.ExecuteScalarAsync();

            var days = (newClientRental.DateTo - newClientRental.DateFrom).Days;
            var totalPrice = days * (int)pricePerDay;

            var insertRentalQuery = @"
                    INSERT INTO car_rentals (ClientID, CarID, DateFrom, DateTo, TotalPrice)
                    VALUES (@ClientID, @CarID, @DateFrom, @DateTo, @TotalPrice)";

            await using var insertRentalCommand =
                new SqlCommand(insertRentalQuery, connection, (SqlTransaction)transaction);
            insertRentalCommand.Parameters.AddWithValue("@ClientID", clientId);
            insertRentalCommand.Parameters.AddWithValue("@CarID", newClientRental.CarId);
            insertRentalCommand.Parameters.AddWithValue("@DateFrom", newClientRental.DateFrom);
            insertRentalCommand.Parameters.AddWithValue("@DateTo", newClientRental.DateTo);
            insertRentalCommand.Parameters.AddWithValue("@TotalPrice", totalPrice);

            await insertRentalCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return clientId;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}