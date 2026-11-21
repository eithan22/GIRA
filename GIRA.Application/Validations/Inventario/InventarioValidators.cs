using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.Common.Inventario;
using GIRA.Application.DTOs.Inventario;
using GIRA.Domain.Enums.Inventario;

namespace GIRA.Application.Validations.Inventario;

/// <summary>Nombre y abreviatura obligatorios.</summary>
public class GuardarUnidadMedidaRequestValidator : AbstractValidator<GuardarUnidadMedidaRequest>
{
    /// <summary>Define las reglas.</summary>
    public GuardarUnidadMedidaRequestValidator()
    {
        RuleFor(r => r.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre no puede pasar de 50 caracteres.");
        RuleFor(r => r.Abreviatura).NotEmpty().WithMessage("La abreviatura es obligatoria.")
            .MaximumLength(10).WithMessage("La abreviatura no puede pasar de 10 caracteres.");
    }
}

/// <summary>Nombre obligatorio (categorías de insumo y de plato).</summary>
public class GuardarCategoriaRequestValidator : AbstractValidator<GuardarCategoriaRequest>
{
    /// <summary>Define las reglas.</summary>
    public GuardarCategoriaRequestValidator()
    {
        RuleFor(r => r.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede pasar de 100 caracteres.");
    }
}

/// <summary>Nombre obligatorio y teléfono dominicano válido (GI-417).</summary>
public class GuardarProveedorRequestValidator : AbstractValidator<GuardarProveedorRequest>
{
    /// <summary>Define las reglas.</summary>
    public GuardarProveedorRequestValidator()
    {
        RuleFor(r => r.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede pasar de 150 caracteres.");
        RuleFor(r => r.Telefono).NotEmpty().WithMessage("El teléfono es obligatorio.")
            .Must(t => TelefonoRd.EsValido(TelefonoRd.Normalizar(t))).WithMessage("El teléfono debe tener 10 dígitos y empezar por 809, 829 u 849.");
    }
}

/// <summary>RF-INV-01: nombre, categoría y unidad obligatorios; punto de reorden y costo no negativos.</summary>
public class GuardarInsumoRequestValidator : AbstractValidator<GuardarInsumoRequest>
{
    /// <summary>Define las reglas.</summary>
    public GuardarInsumoRequestValidator()
    {
        RuleFor(r => r.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede pasar de 100 caracteres.");
        RuleFor(r => r.CategoriaInsumoId).NotEmpty().WithMessage("Elige una categoría.");
        RuleFor(r => r.UnidadMedidaId).NotEmpty().WithMessage("Elige una unidad de medida.");
        RuleFor(r => r.PuntoReorden).GreaterThanOrEqualTo(0).WithMessage("El punto de reorden no puede ser negativo.");
        RuleFor(r => r.CostoUnitario).GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.");
    }
}

/// <summary>Paginación (máximo 200) y nivel válido.</summary>
public class ListarInsumosRequestValidator : AbstractValidator<ListarInsumosRequest>
{
    /// <summary>Define las reglas.</summary>
    public ListarInsumosRequestValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(r => r.PageSize).InclusiveBetween(1, 200).WithMessage("El tamaño de página debe estar entre 1 y 200.");
        RuleFor(r => r.Buscar).MaximumLength(100).WithMessage("La búsqueda no puede pasar de 100 caracteres.");
        RuleFor(r => r.Nivel)
            .Must(n => string.IsNullOrWhiteSpace(n) || new[] { ReglasInventario.NivelNormal, ReglasInventario.NivelBajo, ReglasInventario.NivelCritico }
                .Contains(n.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("El nivel debe ser Normal, Bajo o Critico.");
    }
}

/// <summary>RF-INV-03: precio mayor que cero y porcentaje de demanda entre 0 y 100.</summary>
public class GuardarPlatoRequestValidator : AbstractValidator<GuardarPlatoRequest>
{
    /// <summary>Define las reglas.</summary>
    public GuardarPlatoRequestValidator()
    {
        RuleFor(r => r.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede pasar de 150 caracteres.");
        RuleFor(r => r.CategoriaPlatoId).NotEmpty().WithMessage("Elige una categoría.");
        RuleFor(r => r.Precio).GreaterThan(0).WithMessage("El precio debe ser mayor que cero.");
        RuleFor(r => r.PorcentajeDemanda).InclusiveBetween(0, 100).WithMessage("El porcentaje de demanda debe estar entre 0 y 100.");
    }
}

/// <summary>RF-INV-04: al menos una línea, cada una con insumo y cantidad mayor que cero, sin insumos repetidos.</summary>
public class GuardarRecetaRequestValidator : AbstractValidator<GuardarRecetaRequest>
{
    /// <summary>Define las reglas.</summary>
    public GuardarRecetaRequestValidator()
    {
        RuleFor(r => r.Porciones).GreaterThanOrEqualTo(1).WithMessage("Las porciones deben ser 1 o más.");
        RuleFor(r => r.Instrucciones).MaximumLength(2000).WithMessage("Las instrucciones no pueden pasar de 2000 caracteres.");
        RuleFor(r => r.Lineas).NotEmpty().WithMessage("La receta necesita al menos un insumo.");
        RuleForEach(r => r.Lineas).ChildRules(linea =>
        {
            linea.RuleFor(l => l.InsumoId).NotEmpty().WithMessage("Elige el insumo de cada línea.");
            linea.RuleFor(l => l.Cantidad).GreaterThan(0).WithMessage("La cantidad de cada insumo debe ser mayor que cero.");
        });
        RuleFor(r => r.Lineas)
            .Must(lineas => lineas is null || lineas.Select(l => l.InsumoId).Distinct().Count() == lineas.Count)
            .WithMessage("Un insumo no puede repetirse en la receta.");
    }
}

/// <summary>RF-INV-05 y RN-INV-03: tipo válido, cantidad con sentido y motivo en mermas y ajustes.</summary>
public class RegistrarMovimientoRequestValidator : AbstractValidator<RegistrarMovimientoRequest>
{
    /// <summary>Define las reglas.</summary>
    public RegistrarMovimientoRequestValidator()
    {
        RuleFor(r => r.InsumoId).NotEmpty().WithMessage("Elige un insumo.");
        RuleFor(r => r.Tipo)
            .Must(t => ReglasInventario.EsUno(t, TipoMovimientoInventario.Entrada, TipoMovimientoInventario.Merma, TipoMovimientoInventario.Ajuste))
            .WithMessage("El tipo debe ser Entrada, Merma o Ajuste.");
        When(r => ReglasInventario.EsUno(r.Tipo, TipoMovimientoInventario.Ajuste),
            () => RuleFor(r => r.Cantidad).NotEqual(0).WithMessage("El ajuste no puede ser cero."))
            .Otherwise(() => RuleFor(r => r.Cantidad).GreaterThan(0).WithMessage("La cantidad debe ser mayor que cero."));
        RuleFor(r => r.CostoUnitario).GreaterThanOrEqualTo(0).When(r => r.CostoUnitario.HasValue)
            .WithMessage("El costo no puede ser negativo.");
        RuleFor(r => r.Motivo).NotEmpty()
            .When(r => ReglasInventario.EsUno(r.Tipo, TipoMovimientoInventario.Merma, TipoMovimientoInventario.Ajuste))
            .WithMessage("Las mermas y los ajustes necesitan un motivo.");
        RuleFor(r => r.Motivo).MaximumLength(250).WithMessage("El motivo no puede pasar de 250 caracteres.");
        RuleFor(r => r.Referencia).MaximumLength(100).WithMessage("La referencia no puede pasar de 100 caracteres.");
    }
}

/// <summary>Insumo obligatorio, fechas en orden y como máximo un año.</summary>
public class KardexRequestValidator : AbstractValidator<KardexRequest>
{
    /// <summary>Define las reglas.</summary>
    public KardexRequestValidator()
    {
        RuleFor(r => r.InsumoId).NotEmpty().WithMessage("Elige un insumo.");
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(r => r.PageSize).InclusiveBetween(1, 200).WithMessage("El tamaño de página debe estar entre 1 y 200.");
        RuleFor(r => r).Must(r => r.Desde is null || r.Hasta is null || r.Desde <= r.Hasta)
            .WithMessage("La fecha inicial no puede ser posterior a la final.");
        RuleFor(r => r).Must(r => r.Desde is null || r.Hasta is null || r.Hasta.Value.DayNumber - r.Desde.Value.DayNumber <= 366)
            .WithMessage("El período no puede pasar de un año.");
    }
}

/// <summary>RF-INV-11: motivo obligatorio y cantidades contadas no negativas, sin insumos repetidos.</summary>
public class ConteoFisicoRequestValidator : AbstractValidator<ConteoFisicoRequest>
{
    /// <summary>Define las reglas.</summary>
    public ConteoFisicoRequestValidator()
    {
        RuleFor(r => r.Motivo).NotEmpty().WithMessage("Escribe el motivo de los ajustes.")
            .MaximumLength(250).WithMessage("El motivo no puede pasar de 250 caracteres.");
        RuleFor(r => r.Lineas).NotEmpty().WithMessage("Escribe al menos una cantidad contada.");
        RuleForEach(r => r.Lineas).ChildRules(linea =>
        {
            linea.RuleFor(l => l.InsumoId).NotEmpty().WithMessage("Falta el insumo de una línea.");
            linea.RuleFor(l => l.CantidadContada).GreaterThanOrEqualTo(0).WithMessage("Lo contado no puede ser negativo.");
        });
        RuleFor(r => r.Lineas)
            .Must(lineas => lineas is null || lineas.Select(l => l.InsumoId).Distinct().Count() == lineas.Count)
            .WithMessage("Un insumo no puede contarse dos veces.");
    }
}

/// <summary>Estado válido (o vacío) y paginación.</summary>
public class ListarAlertasRequestValidator : AbstractValidator<ListarAlertasRequest>
{
    /// <summary>Define las reglas.</summary>
    public ListarAlertasRequestValidator()
    {
        RuleFor(r => r.Estado)
            .Must(e => string.IsNullOrWhiteSpace(e) || ReglasInventario.EsUno(e,
                EstadoAlertaInventario.Abierta, EstadoAlertaInventario.Atendida, EstadoAlertaInventario.Descartada))
            .WithMessage("El estado debe ser Abierta, Atendida o Descartada.");
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(r => r.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}

/// <summary>Solo se puede pasar a Atendida o Descartada.</summary>
public class CambiarEstadoAlertaRequestValidator : AbstractValidator<CambiarEstadoAlertaRequest>
{
    /// <summary>Define las reglas.</summary>
    public CambiarEstadoAlertaRequestValidator()
    {
        RuleFor(r => r.Estado)
            .Must(e => ReglasInventario.EsUno(e, EstadoAlertaInventario.Atendida, EstadoAlertaInventario.Descartada))
            .WithMessage("El estado debe ser Atendida o Descartada.");
    }
}
