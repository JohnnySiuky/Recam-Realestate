namespace Recam.Services.Interfaces;

public interface IMediaService
{
    Task<bool> DeleteAsync(int mediaId, string operatorUserId, CancellationToken ct);
}