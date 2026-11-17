using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>Implementación de <see cref="IClienteService"/>.</summary>
public class ClienteService : IClienteService
{
    private readonly IClienteRepository _clientes;
    private readonly IValidator<BuscarClientesRequest> _validador;

    /// <summary>Crea el servicio con el repositorio de clientes y el validador de la búsqueda.</summary>
    public ClienteService(IClienteRepository clientes, IValidator<BuscarClientesRequest> validador)
    {
        _clientes = clientes;
        _validador = validador;
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedResult<ClienteDto>>> BuscarAsync(
        BuscarClientesRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<PagedResult<ClienteDto>>(ErrorType.Validation, validacion.ToString());
        }

        var pagina = await _clientes.BuscarAsync(request.Buscar!, request.Page, request.PageSize, cancellationToken);
        var items = pagina.Items.Select(c => new ClienteDto(c.Id, c.Nombre, c.Telefono)).ToList();

        return OperationResult.Success(new PagedResult<ClienteDto>(items, pagina.Total, pagina.Page, pagina.PageSize));
    }
}
