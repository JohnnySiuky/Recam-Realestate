namespace Recam.Services.Interfaces;

public interface ICaseHistoryService
{
    Task CreatedAsync(int listingId, string actorUserId, object? payload = null, CancellationToken ct = default);
    Task UpdatedAsync(int listingId, string actorUserId, object? payload = null, CancellationToken ct = default);
    
    Task DeletedAsync(int listingId, string actorUserId, object? payload = null, CancellationToken ct = default);
}