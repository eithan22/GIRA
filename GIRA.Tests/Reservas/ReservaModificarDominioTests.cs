using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;

namespace GIRA.Tests.Reservas;

/// <summary>Pruebas de <see cref="Reserva.Modificar"/> (GI-160): solo una reserva Confirmada se modifica.</summary>
public class ReservaModificarDominioTests
{
    private static Reserva NuevaReserva() => new()
    {
        Id = Guid.NewGuid(), Codigo = "10000001", ClienteId = Guid.NewGuid(), MesaId = Guid.NewGuid(),
        FranjaHorariaId = Guid.NewGuid(), Fecha = new DateOnly(2026, 12, 5), Personas = 2, Origen = OrigenReserva.Telefono
    };

    [Fact]
    public void Modificar_Cambia_Fecha_Franja_Mesa_Y_Personas_Y_Sigue_Confirmada()
    {
        var reserva = NuevaReserva();
        var franja = Guid.NewGuid();
        var mesa = Guid.NewGuid();

        reserva.Modificar(new DateOnly(2026, 12, 6), franja, mesa, 4);

        Assert.Equal(new DateOnly(2026, 12, 6), reserva.Fecha);
        Assert.Equal(franja, reserva.FranjaHorariaId);
        Assert.Equal(mesa, reserva.MesaId);
        Assert.Equal(4, reserva.Personas);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Theory]
    [InlineData(EstadoReserva.EnCurso)]
    [InlineData(EstadoReserva.Completada)]
    [InlineData(EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.NoAsistio)]
    public void Modificar_Fuera_De_Confirmada_Lanza_Y_No_Cambia_Nada(EstadoReserva estado)
    {
        var reserva = NuevaReserva();
        switch (estado)
        {
            case EstadoReserva.EnCurso:
                reserva.HacerCheckIn(2, DateTime.UtcNow);
                break;
            case EstadoReserva.Completada:
                reserva.HacerCheckIn(2, DateTime.UtcNow);
                reserva.Completar();
                break;
            case EstadoReserva.Cancelada:
                reserva.Cancelar("Motivo");
                break;
            case EstadoReserva.NoAsistio:
                reserva.MarcarNoAsistio();
                break;
        }
        var fechaOriginal = reserva.Fecha;

        Assert.Throws<TransicionReservaInvalidaException>(
            () => reserva.Modificar(new DateOnly(2026, 12, 9), Guid.NewGuid(), Guid.NewGuid(), 5));

        Assert.Equal(fechaOriginal, reserva.Fecha);
        Assert.Equal(2, reserva.Personas);
    }
}
