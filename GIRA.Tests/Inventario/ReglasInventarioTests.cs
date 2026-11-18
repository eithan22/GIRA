using GIRA.Application.Common.Inventario;
using GIRA.Application.Services.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Enums.Inventario;

namespace GIRA.Tests.Inventario;

public class ReglasInventarioTests
{
    [Theory]
    [InlineData(11, 10, "Normal")]
    [InlineData(10, 10, "Bajo")]
    [InlineData(6, 10, "Bajo")]
    [InlineData(5, 10, "Critico")]
    [InlineData(0, 10, "Critico")]
    public void Nivel_Corta_En_El_Punto_De_Reorden_Y_En_Su_Mitad(decimal stock, decimal reorden, string esperado)
    {
        Assert.Equal(esperado, ReglasInventario.Nivel(stock, reorden));
    }

    [Theory]
    [InlineData(100, true, false)]
    [InlineData(95, true, false)]
    [InlineData(105, true, false)]
    [InlineData(94.99, true, true)]
    [InlineData(105.01, true, true)]
    [InlineData(0, false, false)]
    public void Advertencia_De_Demanda_Fuera_De_95_A_105(decimal suma, bool hayActivos, bool hayAviso)
    {
        Assert.Equal(hayAviso, ReglasInventario.AdvertenciaDemanda(suma, hayActivos) is not null);
    }

    [Fact]
    public void AplicarMovimiento_No_Deja_El_Stock_Negativo()
    {
        var insumo = new Insumo();
        insumo.AplicarMovimiento(5);

        Assert.Throws<InvalidOperationException>(() => insumo.AplicarMovimiento(-6));
        Assert.Equal(5, insumo.CantidadStock);
        insumo.AplicarMovimiento(-5);
        Assert.Equal(0, insumo.CantidadStock);
    }

    [Fact]
    public void Alerta_Solo_Se_Cierra_Una_Vez()
    {
        var alerta = new AlertaInventario();
        var usuario = Guid.NewGuid();

        alerta.Cerrar(EstadoAlertaInventario.Atendida, usuario, DateTime.UtcNow);

        Assert.Equal(EstadoAlertaInventario.Atendida, alerta.Estado);
        Assert.Equal(usuario, alerta.AtendidaPor);
        Assert.Throws<InvalidOperationException>(() => alerta.Cerrar(EstadoAlertaInventario.Descartada, usuario, DateTime.UtcNow));
    }

    [Fact]
    public void Factores_Suman_Demanda_Por_Cantidad_Por_Porcion_Solo_De_Platos_Activos_Con_Receta()
    {
        var arroz = Guid.NewGuid();
        var pollo = Guid.NewGuid();
        var platos = new[]
        {
            // 50 % de los comensales; 2 kg de arroz rinden 10 porciones → 0.5 × 2 / 10 = 0.1 kg por persona.
            Plato(50, activo: true, porciones: 10, (arroz, 2m), (pollo, 3m)),
            // 25 % de los comensales; 1 kg de arroz para 1 porción → 0.25 kg por persona.
            Plato(25, activo: true, porciones: 1, (arroz, 1m)),
            // Inactivo: no cuenta.
            Plato(25, activo: false, porciones: 1, (arroz, 100m))
        };

        var factores = CalculadoraConsumo.Factores(platos);

        Assert.Equal(0.35m, factores[arroz]);
        Assert.Equal(0.15m, factores[pollo]);
    }

    private static Plato Plato(decimal demanda, bool activo, int porciones, params (Guid InsumoId, decimal Cantidad)[] lineas) => new()
    {
        Id = Guid.NewGuid(),
        PorcentajeDemanda = demanda,
        Activo = activo,
        Receta = new Receta
        {
            Porciones = porciones,
            Detalles = lineas.Select(l => new RecetaDetalle { InsumoId = l.InsumoId, Cantidad = l.Cantidad }).ToList()
        }
    };
}
