using GIRA.Domain.Base;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Entities.Seguridad;
using GIRA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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

    /// <summary>Solicitudes de recuperación de contraseña pendientes de atender por un Administrador.</summary>
    public DbSet<SolicitudRecuperacionPassword> SolicitudesRecuperacionPassword => Set<SolicitudRecuperacionPassword>();

    /// <summary>Clientes identificados por teléfono.</summary>
    public DbSet<Cliente> Clientes => Set<Cliente>();

    /// <summary>Zonas del restaurante.</summary>
    public DbSet<Zona> Zonas => Set<Zona>();

    /// <summary>Mesas del restaurante.</summary>
    public DbSet<Mesa> Mesas => Set<Mesa>();

    /// <summary>Franjas horarias en las que se puede reservar.</summary>
    public DbSet<FranjaHoraria> FranjasHorarias => Set<FranjaHoraria>();

    /// <summary>Reservas de mesas.</summary>
    public DbSet<Reserva> Reservas => Set<Reserva>();

    /// <summary>Visitas de clientes que llegaron sin reserva previa.</summary>
    public DbSet<VisitaSinReserva> VisitasSinReserva => Set<VisitaSinReserva>();

    /// <summary>Políticas de reserva del restaurante (una sola fila).</summary>
    public DbSet<ConfiguracionRestaurante> ConfiguracionesRestaurante => Set<ConfiguracionRestaurante>();

    /// <summary>Horario de operación, una fila por día de la semana.</summary>
    public DbSet<HorarioOperacion> HorariosOperacion => Set<HorarioOperacion>();

    /// <summary>Fechas concretas en las que el restaurante está cerrado.</summary>
    public DbSet<DiaCerrado> DiasCerrados => Set<DiaCerrado>();

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

        // RowVersion y borrado lógico son responsabilidad de BaseEntity para TODA entidad que
        // herede de ella (hoy, solo las de Reservas); se aplican aquí una sola vez en vez de
        // repetirlos en cada IEntityTypeConfiguration.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            builder.Entity(entityType.ClrType)
                .Property(nameof(BaseEntity.RowVersion))
                .IsRowVersion();

            var parametro = Expression.Parameter(entityType.ClrType, "entidad");
            var propiedad = Expression.Property(parametro, nameof(BaseEntity.IsDeleted));
            var filtro = Expression.Lambda(Expression.Not(propiedad), parametro);

            builder.Entity(entityType.ClrType).HasQueryFilter(filtro);
        }
    }
}
