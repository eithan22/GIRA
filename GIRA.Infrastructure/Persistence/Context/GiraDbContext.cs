using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Seguridad;
using GIRA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Context;

/// <summary>
/// Contexto único de EF Core de GIRA. Hereda de <see cref="IdentityDbContext{TUser, TRole, TKey}"/>
/// para obtener las tablas de ASP.NET Core Identity (usuarios, roles y sus relaciones)
/// y agrega las entidades propias de seguridad que Identity no maneja.
/// </summary>
public class GiraDbContext : IdentityDbContext<Usuario, Rol, Guid>
{
    /// <summary>Crea el contexto con las opciones configuradas en GIRA.Infrastructure.DependencyInjection.</summary>
    public GiraDbContext(DbContextOptions<GiraDbContext> options) : base(options)
    {
    }

    /// <summary>Tokens de renovación de sesión emitidos a los usuarios.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Historial de intentos de inicio de sesión, exitosos o no.</summary>
    public DbSet<RegistroAcceso> RegistrosAcceso => Set<RegistroAcceso>();

    /// <summary>Registros históricos de afluencia de comensales.</summary>
    public DbSet<HistorialAfluencia> HistorialAfluencia => Set<HistorialAfluencia>();

    /// <summary>Resultados generados por los modelos de predicción de afluencia.</summary>
    public DbSet<PrediccionResultado> PrediccionesAfluencia => Set<PrediccionResultado>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity, por defecto, mapea sus tablas a "AspNetUsers", "AspNetRoles", etc. en
        // el esquema "dbo". Las reubicamos en el esquema "seguridad" con nombres en español
        // para que convivan con el resto del modelo del proyecto (un esquema por módulo).
        builder.Entity<Usuario>(entity => entity.ToTable("Usuario", "seguridad"));
        builder.Entity<Rol>(entity => entity.ToTable("Rol", "seguridad"));
        builder.Entity<IdentityUserRole<Guid>>(entity => entity.ToTable("UsuarioRol", "seguridad"));
        builder.Entity<IdentityUserClaim<Guid>>(entity => entity.ToTable("UsuarioClaim", "seguridad"));
        builder.Entity<IdentityUserLogin<Guid>>(entity => entity.ToTable("UsuarioLogin", "seguridad"));
        builder.Entity<IdentityUserToken<Guid>>(entity => entity.ToTable("UsuarioToken", "seguridad"));
        builder.Entity<IdentityRoleClaim<Guid>>(entity => entity.ToTable("RolClaim", "seguridad"));

        builder.ApplyConfigurationsFromAssembly(typeof(GiraDbContext).Assembly);
    }
}
