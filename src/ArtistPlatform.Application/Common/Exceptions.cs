namespace ArtistPlatform.Application.Common;

/// <summary>Recurso não encontrado → 404.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Sem permissão sobre o recurso → 403.</summary>
public class ForbiddenException(string message = "Acesso negado.") : Exception(message);

/// <summary>Regra de negócio violada (RN001–RN020) → 400/422.</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>Dados inválidos → 400.</summary>
public class ValidationException(string message) : Exception(message);

/// <summary>Credenciais inválidas / auth → 401.</summary>
public class UnauthorizedException(string message = "Credenciais inválidas.") : Exception(message);
