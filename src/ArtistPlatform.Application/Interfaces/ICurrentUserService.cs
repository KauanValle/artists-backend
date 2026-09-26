using ArtistPlatform.Application.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Application.Interfaces;

/// <summary>Usuário autenticado da requisição atual.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? ArtistId { get; }
    Guid? ContractorId { get; }
    UserRole? Role { get; }
    bool IsInRole(UserRole role);

    /// <summary>Retorna o UserId ou lança 401/403.</summary>
    Guid EnsureAuthenticated();

    /// <summary>Retorna o ArtistId do artista autenticado ou lança 403.</summary>
    Guid EnsureArtist();

    /// <summary>Retorna o ContractorId do contratante autenticado ou lança 403.</summary>
    Guid EnsureContractor();
}

public interface IDateTimeClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

/// <summary>Armazenamento de arquivos (S3 em produção; disco local no MVP).</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    void Delete(string url);
}
