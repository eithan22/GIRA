using GIRA.Domain.Base;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Entities.Personal;
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

    /// <summary>Cargos del personal (mesero, cocinero, etc.), módulo Personal y Turnos (GI-309).</summary>
    public DbSet<Cargo> Cargos => Set<Cargo>();

    /// <summary>Empleados del restaurante, módulo Personal y Turnos (GI-295).</summary>
    public DbSet<Empleado> Empleados => Set<Empleado>();

    /// <summary>Bloques de disponibilidad semanal de los empleados (GI-297).</summary>
    public DbSet<Disponibilidad> Disponibilidades => Set<Disponibilidad>();

    /// <summary>Turnos asignados a los empleados (GI-298).</summary>
    public DbSet<Turno> Turnos => Set<Turno>();

    /// <summary>Solicitudes de ausencia de los empleados (GI-298, RN-PER-03).</summary>
    public DbSet<Ausencia> Ausencias => Set<Ausencia>();

    /// <summary>Requerimiento de personal por cargo: comensales por empleado y mínimo por franja (GI-302).</summary>
    public DbSet<RequerimientoPersonal> RequerimientosPersonal => Set<RequerimientoPersonal>();

    /// <summary>Sugerencias de personal generadas por las predicciones de afluencia (GI-303, RF-PER-09).</summary>
    public DbSet<SugerenciaTurno> Sugerencias => Set<SugerenciaTurno>();

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

    /// <summary>Unidades de medida de los insumos.</summary>
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();

    /// <summary>Categorías de insumo.</summary>
    public DbSet<CategoriaInsumo> CategoriasInsumo => Set<CategoriaInsumo>();

    /// <summary>Categorías de plato.</summary>
    public DbSet<CategoriaPlato> CategoriasPlato => Set<CategoriaPlato>();

    /// <summary>Proveedores de insumos.</summary>
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    /// <summary>Insumos con su stock actual.</summary>
    public DbSet<Insumo> Insumos => Set<Insumo>();

    /// <summary>Platos del menú con su porcentaje de demanda.</summary>
    public DbSet<Plato> Platos => Set<Plato>();

    /// <summary>Recetas, una por plato.</summary>
    public DbSet<Receta> Recetas => Set<Receta>();

    /// <summary>Líneas de receta: insumo y cantidad.</summary>
    public DbSet<RecetaDetalle> RecetaDetalles => Set<RecetaDetalle>();

    /// <summary>Movimientos de inventario (kardex).</summary>
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();

    /// <summary>Lotes de insumos perecederos con su vencimiento.</summary>
    public DbSet<LoteInsumo> LotesInsumo => Set<LoteInsumo>();

    /// <summary>Consumo proyectado por fecha e insumo.</summary>
    public DbSet<ConsumoProyectado> ConsumosProyectados => Set<ConsumoProyectado>();

    /// <summary>Alertas de reabastecimiento, vencimiento e inconsistencia.</summary>
    public DbSet<AlertaInventario> AlertasInventario => Set<AlertaInventario>();

    /// <summary>Comensales predichos por Analítica, por fecha.</summary>
    public DbSet<AfluenciaPredicha> AfluenciasPredichas => Set<AfluenciaPredicha>();

    /// <summary>Solicitudes de entrenamiento de modelos de afluencia y sus métricas de validación.</summary>
    public DbSet<EntrenamientoModelo> EntrenamientosModelos => Set<EntrenamientoModelo>();

    /// <summary>Versiones SSA entrenadas y evaluadas para cada franja horaria.</summary>
    public DbSet<ModeloAfluencia> ModelosAfluencia => Set<ModeloAfluencia>();

    /// <summary>Capacidad efectiva capturada para cada día operativo.</summary>
    public DbSet<CapacidadOperativaDiaria> CapacidadesOperativasDiarias => Set<CapacidadOperativaDiaria>();

    /// <summary>Marcaciones reales de entrada y salida del personal.</summary>
    public DbSet<MarcacionTurno> MarcacionesTurnos => Set<MarcacionTurno>();

    /// <summary>Auditoría de activación y rollback manual de modelos.</summary>
    public DbSet<AuditoriaActivacionModelo> AuditoriasActivacionModelo => Set<AuditoriaActivacionModelo>();

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
