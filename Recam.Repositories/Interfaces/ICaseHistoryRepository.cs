using Recam.Models.Entities;

namespace Recam.Respostitories.Interfaces;

public interface ICaseHistoryRepository
{
    Task AddAsync(CaseHistory entity, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}