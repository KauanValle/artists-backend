using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Infrastructure.Asaas;
using ArtistPlatform.Infrastructure.Auth;
using ArtistPlatform.Infrastructure.Data;
using ArtistPlatform.Infrastructure.Identity;
using ArtistPlatform.Infrastructure.Services;
using ArtistPlatform.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ArtistPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // ASP.NET Core Identity + JWT + Refresh Tokens (PRD §5)
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Seção 'Jwt' não configurada.");
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton(jwtSettings);
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IdentityService>();
        services.AddScoped<IIdentityService>(sp => sp.GetRequiredService<IdentityService>());
        services.AddScoped<ISubscriptionActivator>(sp => sp.GetRequiredService<IdentityService>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddSingleton<IDateTimeClock, DateTimeClock>();
        // Arquivos: bucket S3 (Railway Objects) quando as credenciais estiverem completas;
        // sem bucket configurado, cai no disco local (wwwroot/uploads) para desenvolvimento.
        var s3 = configuration.GetSection(S3Options.SectionName).Get<S3Options>() ?? new S3Options();
        var useS3 = !string.IsNullOrWhiteSpace(s3.EndpointUrl)
                    && !string.IsNullOrWhiteSpace(s3.BucketName)
                    && !string.IsNullOrWhiteSpace(s3.AccessKeyId)
                    && !string.IsNullOrWhiteSpace(s3.SecretAccessKey);
        if (useS3)
        {
            services.Configure<S3Options>(configuration.GetSection(S3Options.SectionName));
            services.AddScoped<IFileStorage, S3FileStorage>();
        }
        else
        {
            services.AddScoped<IFileStorage, LocalFileStorage>();
        }

        services.AddScoped<DbInitializer>();

        // Assinatura (Asaas): opções do ambiente + cliente HTTP de checkout.
        services.Configure<AsaasOptions>(configuration.GetSection(AsaasOptions.SectionName));
        services.AddHttpClient<IAsaasCheckoutClient, AsaasCheckoutClient>();
        services.AddScoped<IBillingService, BillingService>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
        await initializer.InitializeAsync();
    }
}
