using System.Text.Json;
using Microsoft.Extensions.Logging;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class SqlCaseHistoryService : ICaseHistoryService
{
    private readonly ICaseHistoryRepository _repo;
    private readonly ILogger<SqlCaseHistoryService> _log;

    public SqlCaseHistoryService(ICaseHistoryRepository repo, ILogger<SqlCaseHistoryService> log)
    { _repo = repo; _log = log; }

    public async Task CreatedAsync(int listingId, string actorUserId, object? payload = null, CancellationToken ct = default)
    {
        try
        {
            var entity = new CaseHistory
            {
                ListingCaseId = listingId,
                Event = "CREATED",
                ActorUserId = actorUserId,
                AtUtc = DateTime.UtcNow,
                PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload)
            };
            await _repo.AddAsync(entity, ct);
            await _repo.SaveAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Write CaseHistory failed. ListingCaseId={Id}", listingId);
            // dont stop the main progerss
        }
    }
    
    public async Task UpdatedAsync(int listingId, string actorUserId, object? payload = null, CancellationToken ct = default)
    {
        try
        {
            var entity = new CaseHistory
            {
                ListingCaseId = listingId,
                Event = "UPDATED",
                ActorUserId = actorUserId,
                AtUtc = DateTime.UtcNow,
                PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload)
            };
            await _repo.AddAsync(entity, ct);
            await _repo.SaveAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Write CaseHistory failed. ListingCaseId={Id}", listingId);
        }
    }
    
    public async Task DeletedAsync(int listingId, string actorUserId, object? payload = null, CancellationToken ct = default)
    {
        try
        {
            var entity = new CaseHistory
            {
                ListingCaseId = listingId,
                Event         = "DELETED",
                ActorUserId   = actorUserId,
                AtUtc         = DateTime.UtcNow,
                PayloadJson   = payload is null ? null : JsonSerializer.Serialize(payload)
            };
            await _repo.AddAsync(entity, ct);
            await _repo.SaveAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Write CaseHistory failed. ListingCaseId={Id}", listingId);
        }
    }
}