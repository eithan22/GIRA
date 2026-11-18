using GIRA.Application.Common;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Enums.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Seed;

/// <summary>
/// Datos de ejemplo de Inventario para Development: unidades, categorías, un proveedor, insumos con su stock
/// cargado por movimientos de entrada (RN-INV-01), y platos con receta. Solo siembra si no hay ninguna unidad
/// (cuenta también las borradas), así nunca pisa lo que el equipo haya cargado.
/// </summary>
public static class InventarioSeeder
{
    /// <summary>Siembra los datos de ejemplo si el inventario está vacío.</summary>
    public static async Task SembrarAsync(GiraDbContext contexto, CancellationToken cancellationToken = default)
    {
        if (await contexto.UnidadesMedida.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return;
        }

        var hoy = TimeProvider.System.HoyLocal();
        var ahora = DateTime.UtcNow;

        var kg = Unidad("Kilogramo", "kg");
        var litro = Unidad("Litro", "L");
        var unidad = Unidad("Unidad", "u");
        contexto.UnidadesMedida.AddRange(kg, litro, unidad);

        var carnes = new CategoriaInsumo { Id = Guid.NewGuid(), Nombre = "Carnes" };
        var granos = new CategoriaInsumo { Id = Guid.NewGuid(), Nombre = "Granos y abarrotes" };
        var frescos = new CategoriaInsumo { Id = Guid.NewGuid(), Nombre = "Vegetales y lácteos" };
        contexto.CategoriasInsumo.AddRange(carnes, granos, frescos);

        var fuertes = new CategoriaPlato { Id = Guid.NewGuid(), Nombre = "Platos fuertes" };
        var ligeros = new CategoriaPlato { Id = Guid.NewGuid(), Nombre = "Ligeros" };
        contexto.CategoriasPlato.AddRange(fuertes, ligeros);

        var proveedor = new Proveedor { Id = Guid.NewGuid(), Nombre = "Distribuidora Central", Telefono = "8095550101" };
        contexto.Proveedores.Add(proveedor);

        var pollo = Insumo("Pollo", carnes, kg, reorden: 10, costo: 180, stock: 25, vence: hoy.AddDays(4), proveedor);
        var arroz = Insumo("Arroz", granos, kg, reorden: 15, costo: 45, stock: 40, vence: null, proveedor);
        var habichuelas = Insumo("Habichuelas rojas", granos, kg, reorden: 8, costo: 90, stock: 12, vence: null, proveedor);
        var aceite = Insumo("Aceite vegetal", granos, litro, reorden: 5, costo: 120, stock: 10, vence: null, proveedor);
        var tomate = Insumo("Tomate", frescos, kg, reorden: 5, costo: 60, stock: 6, vence: hoy.AddDays(2), null);
        var queso = Insumo("Queso blanco", frescos, kg, reorden: 4, costo: 250, stock: 3, vence: hoy.AddDays(6), null);
        var pan = Insumo("Pan de agua", granos, unidad, reorden: 30, costo: 8, stock: 50, vence: hoy.AddDays(1), null);

        Plato("La bandera", fuertes, precio: 450, demanda: 50, porciones: 10,
            (arroz, 1.5m), (habichuelas, 0.8m), (pollo, 2.5m), (aceite, 0.3m));
        Plato("Pollo a la plancha con ensalada", fuertes, precio: 520, demanda: 30, porciones: 4,
            (pollo, 1m), (tomate, 0.6m), (aceite, 0.1m));
        Plato("Sándwich de queso", ligeros, precio: 250, demanda: 20, porciones: 1,
            (pan, 1m), (queso, 0.08m), (tomate, 0.05m));

        await contexto.SaveChangesAsync(cancellationToken);
        return;

        static UnidadMedida Unidad(string nombre, string abreviatura) =>
            new() { Id = Guid.NewGuid(), Nombre = nombre, Abreviatura = abreviatura };

        Insumo Insumo(string nombre, CategoriaInsumo categoria, UnidadMedida medida, decimal reorden, decimal costo, decimal stock,
            DateOnly? vence, Proveedor? deQuien)
        {
            var insumo = new Insumo
            {
                Id = Guid.NewGuid(),
                Nombre = nombre,
                CategoriaInsumo = categoria,
                UnidadMedida = medida,
                PuntoReorden = reorden,
                CostoUnitario = costo,
                Perecedero = vence is not null,
                Proveedor = deQuien
            };
            insumo.AplicarMovimiento(stock);
            contexto.Insumos.Add(insumo);
            contexto.MovimientosInventario.Add(new MovimientoInventario
            {
                Id = Guid.NewGuid(),
                InsumoId = insumo.Id,
                Tipo = TipoMovimientoInventario.Entrada,
                Cantidad = stock,
                CostoUnitario = costo,
                Fecha = ahora,
                Referencia = "Carga inicial (datos de ejemplo)",
                SaldoResultante = stock
            });
            if (vence is { } fecha)
            {
                contexto.LotesInsumo.Add(new LoteInsumo
                {
                    Id = Guid.NewGuid(), InsumoId = insumo.Id, Cantidad = stock, FechaEntrada = hoy, FechaVencimiento = fecha
                });
            }

            return insumo;
        }

        void Plato(string nombre, CategoriaPlato categoria, decimal precio, decimal demanda, int porciones,
            params (Insumo Insumo, decimal Cantidad)[] lineas)
        {
            var plato = new Plato { Id = Guid.NewGuid(), Nombre = nombre, CategoriaPlato = categoria, Precio = precio, PorcentajeDemanda = demanda };
            var receta = new Receta { Id = Guid.NewGuid(), PlatoId = plato.Id, Porciones = porciones };
            receta.Detalles.AddRange(lineas.Select(l => new RecetaDetalle
            {
                Id = Guid.NewGuid(), RecetaId = receta.Id, InsumoId = l.Insumo.Id, Cantidad = l.Cantidad
            }));
            contexto.Platos.Add(plato);
            contexto.Recetas.Add(receta);
        }
    }
}
