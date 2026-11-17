namespace AlanTransfer.Api.Dtos;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Role, string Username);

public record CreateReservationRequest(
    string PickUp,
    string DropOff,
    DateTime PickupAt,
    int PaxCount,
    decimal Price,
    string Currency);

public record AssignRequest(int DriverId);
public record StatusRequest(string Step); // EnRoute, Completed, Cancelled

public record ReservationDto(
    int Id,
    string Code,
    string PickUp,
    string DropOff,
    DateTime PickupAt,
    int PaxCount,
    decimal Price,
    string Currency,
    string Status,
    string AssignmentType,
    string SourceChannel,
    int? AgencyId,
    int? DriverId);
