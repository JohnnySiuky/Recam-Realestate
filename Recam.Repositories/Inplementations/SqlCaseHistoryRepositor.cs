using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;

namespace Recam.Respostitories.Inplementations;

public class SqlCaseHistoryRepository : ICaseHistoryRepository
{
    private readonly RecamDbContext _db;
    public SqlCaseHistoryRepository(RecamDbContext db) => _db = db;

    public Task AddAsync(CaseHistory entity, CancellationToken ct = default)
    { _db.Set<CaseHistory>().Add(entity); return Task.CompletedTask; }

    public Task SaveAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}