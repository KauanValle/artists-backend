using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IArtistService, ArtistService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IFeeService, FeeService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IProposalService, ProposalService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IFinancialService, FinancialService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IContractorService, ContractorService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
