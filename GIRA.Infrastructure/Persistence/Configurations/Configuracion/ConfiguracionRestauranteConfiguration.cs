using GIRA.Domain.Entities.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GIRA.Infrastructure.Persistence.Configurations.Configuracion;

/// <summary>Configuración Fluent API de <see cref="ConfiguracionRestaurante"/> (tabla de una sola fila).</summary>
public class ConfiguracionRestauranteConfiguration : IEntityTypeConfiguration<ConfiguracionRestaurante>
{
    /// <summary>Configura la tabla, la fila única y los rangos válidos de cada parámetro.</summary>
    public void Configure(EntityTypeBuilder<ConfiguracionRestaurante> builder)
    {
        builder.ToTable("ConfiguracionRestaurante", "configuracion", tabla =>
        {
            // Segunda barrera: el validador de la API es la primera. Las comparaciones entre
            // parámetros (mínima < máxima, etc.) solo se revisan en el validador.
            tabla.HasCheckConstraint("CK_ConfiguracionRestaurante_FilaUnica",
                "[Id] = '00000000-0000-0000-0000-0000000000c1'");
            tabla.HasCheckConstraint("CK_ConfiguracionRestaurante_AntelacionMinima", "[AntelacionMinimaMinutos] >= 0");
            tabla.HasCheckConstraint("CK_ConfiguracionRestaurante_AntelacionMaxima", "[AntelacionMaximaDias] BETWEEN 1 AND 365");
            tabla.HasCheckConstraint("CK_ConfiguracionRestaurante_LimiteCancelacion", "[LimiteCancelacionMinutos] >= 0");
            tabla.HasCheckConstraint("CK_ConfiguracionRestaurante_VentanaCheckIn", "[VentanaCheckInMinutos] BETWEEN 0 AND 120");
            tabla.HasCheckConstraint("CK_ConfiguracionRestaurante_MaxVigentes", "[MaxReservasVigentesPorTelefono] BETWEEN 1 AND 10");
            tabla.HasCheckConstraint("CK_ConfiguracionRestaurante_MaxPersonas", "[MaxPersonasPorReserva] BETWEEN 1 AND 50");
        });

        builder.HasKey(c => c.Id);
    }
}
