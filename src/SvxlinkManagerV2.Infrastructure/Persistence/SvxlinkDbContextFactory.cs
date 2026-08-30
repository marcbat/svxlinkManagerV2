using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Fabrique utilisée uniquement par les outils EF Core (<c>dotnet ef migrations add</c>,
/// <c>dotnet ef migrations script</c>). Elle évite de démarrer l'hôte Blazor de la couche
/// Presentation — et ses services hébergés — pour la simple génération d'une migration.
///
/// La chaîne de connexion est factice : les outils n'ont besoin que du provider SQLite
/// pour déterminer les types de colonnes.
/// </summary>
public class SvxlinkDbContextFactory : IDesignTimeDbContextFactory<SvxlinkDbContext>
{
    public SvxlinkDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SvxlinkDbContext>()
            .UseSqlite("Data Source=svxlinkmanager-design.db")
            .Options;

        return new SvxlinkDbContext(options);
    }
}
