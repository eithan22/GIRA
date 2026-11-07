using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas de la máquina de estados de <see cref="Reserva"/> (GI-156): las transiciones
/// válidas cambian el estado y los datos esperados, y cualquier otra transición lanza
/// <see cref="TransicionReservaInvalidaException"/>.
/// </summary>
public class ReservaTests
{
    [Fact]
    public void Reserva_AlCrearse_EmpiezaEnConfirmada()
    {
        var reserva = new Reserva();

        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    public void HacerCheckIn_DesdeConfirmada_PasaAEnCursoYGuardaDatos()
    {
        var reserva = new Reserva();
        var ahora = new DateTime(2026, 11, 7, 20, 0, 0, DateTimeKind.Utc);

        reserva.HacerCheckIn(personasReales: 3, ahora);

        Assert.Equal(EstadoReserva.EnCurso, reserva.Estado);
        Assert.Equal(3, reserva.PersonasReales);
        Assert.Equal(ahora, reserva.FechaCheckIn);
    }

    [Fact]
    public void HacerCheckIn_DesdeOtroEstado_LanzaExcepcion()
    {
        var reserva = new Reserva();
        reserva.Cancelar(motivo: null);

        Assert.Throws<TransicionReservaInvalidaException>(
            () => reserva.HacerCheckIn(2, DateTime.UtcNow));
    }

    [Fact]
    public void Completar_DesdeEnCurso_PasaACompletada()
    {
        var reserva = new Reserva();
        reserva.HacerCheckIn(2, DateTime.UtcNow);

        reserva.Completar();

        Assert.Equal(EstadoReserva.Completada, reserva.Estado);
    }

    [Fact]
    public void Completar_SinHaberHechoCheckIn_LanzaExcepcion()
    {
        var reserva = new Reserva();

        Assert.Throws<TransicionReservaInvalidaException>(() => reserva.Completar());
    }

    [Fact]
    public void Cancelar_DesdeConfirmadaSinMotivo_PasaACanceladaConMotivoNulo()
    {
        var reserva = new Reserva();

        reserva.Cancelar(motivo: null);

        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
        Assert.Null(reserva.MotivoCancelacion);
    }

    [Fact]
    public void Cancelar_DesdeConfirmadaConMotivo_GuardaElMotivo()
    {
        var reserva = new Reserva();

        reserva.Cancelar(motivo: "El cliente llamó para cancelar");

        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
        Assert.Equal("El cliente llamó para cancelar", reserva.MotivoCancelacion);
    }

    [Fact]
    public void Cancelar_UnaReservaYaCancelada_LanzaExcepcion()
    {
        var reserva = new Reserva();
        reserva.Cancelar(motivo: null);

        Assert.Throws<TransicionReservaInvalidaException>(() => reserva.Cancelar(motivo: null));
    }

    [Fact]
    public void MarcarNoAsistio_DesdeConfirmada_PasaANoAsistio()
    {
        var reserva = new Reserva();

        reserva.MarcarNoAsistio();

        Assert.Equal(EstadoReserva.NoAsistio, reserva.Estado);
    }

    [Fact]
    public void MarcarNoAsistio_DesdeEnCurso_LanzaExcepcion()
    {
        var reserva = new Reserva();
        reserva.HacerCheckIn(2, DateTime.UtcNow);

        Assert.Throws<TransicionReservaInvalidaException>(() => reserva.MarcarNoAsistio());
    }
}
