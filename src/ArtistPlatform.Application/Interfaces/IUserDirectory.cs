namespace ArtistPlatform.Application.Interfaces;

public record UserInfo(Guid UserId, string DisplayName, string? PhotoUrl);

/// <summary>Consulta nomes/fotos de usuários (implementada na Infrastructure sobre o Identity).</summary>
public interface IUserDirectory
{
    Task<Dictionary<Guid, UserInfo>> GetUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}
