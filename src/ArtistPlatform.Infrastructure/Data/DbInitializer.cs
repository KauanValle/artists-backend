using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArtistPlatform.Infrastructure.Data;

/// <summary>Migrations + seed (roles, categorias e admin) na inicialização.</summary>
public class DbInitializer(
    AppDbContext db,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IHostEnvironment environment,
    ILogger<DbInitializer> logger)
{
    public async Task InitializeAsync()
    {
        await db.Database.MigrateAsync();

        await EnsureRoleAsync(UserRole.Artist);
        await EnsureRoleAsync(UserRole.Contractor);
        await EnsureRoleAsync(UserRole.Admin);

        await SeedCategoriesAsync();
        await SeedAdminAsync();
    }

    private async Task EnsureRoleAsync(UserRole role)
    {
        var name = role.ToString();
        if (!await roleManager.RoleExistsAsync(name))
            await roleManager.CreateAsync(new IdentityRole<Guid>(name));
    }

    private async Task SeedCategoriesAsync()
    {
        if (await db.ArtistCategories.AnyAsync()) return;

        string[] categories = ["Cantor", "Banda", "DJ", "Comediante", "Stand-up", "Músico", "Ator", "Dançarino", "Mágico", "Outro"];
        foreach (var name in categories)
        {
            db.ArtistCategories.Add(new ArtistCategory
            {
                Name = name,
                Slug = ArtistService.Slugify(name)
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Categorias de artista semeadas.");
    }

    private async Task SeedAdminAsync()
    {
        const string email = "admin@artistplatform.com";
        if (await userManager.FindByEmailAsync(email) is not null) return;

        var admin = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Administrador",
            Role = UserRole.Admin,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(admin, "Admin@123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, UserRole.Admin.ToString());
            logger.LogInformation("Usuário admin semeado ({Email}).", email);
        }
    }
}
