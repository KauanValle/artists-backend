using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace ArtistPlatform.Infrastructure.Identity;

/// <summary>Usuário do Identity (AspNetUsers) — PRD §26 "User": identidade, autenticação e role.</summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? ArtistId { get; set; }
    public Guid? ContractorId { get; set; }

    /// <summary>Cargo de acesso: Gratuito (padrão, funções bloqueadas) ou Assinante (acesso total).</summary>
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Gratuito;

    /// <summary>Tutorial de onboarding concluído (segue a conta em qualquer dispositivo).</summary>
    public bool TutorialFeito { get; set; }

    /// <summary>Ids dos passos já concluídos do tutorial (JSON).</summary>
    public string TutorialPassos { get; set; } = "[]";

    /// <summary>Conta desativada pelo administrador — bloqueia o acesso ao painel.</summary>
    public bool IsDeactivated { get; set; }

    /// <summary>Mensagem personalizada exibida ao usuário quando a conta está desativada.</summary>
    public string? DeactivationMessage { get; set; }
}
