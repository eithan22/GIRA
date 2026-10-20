using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Infrastructure.Services.Seguridad
{
    public class HistorialAccesosService : IHistorialAccesosService
    {
        private readonly GiraDbContext _contexto;
        private readonly IValidator<HistorialAccesosRequest> _validator;

        /// <summary>Crea el servicio con el contexto de base de datos y el validador del filtro.</summary>
        public HistorialAccesosService(GiraDbContext contexto, IValidator<HistorialAccesosRequest> validator)
        {
            _contexto = contexto;
            _validator = validator;
        }

        /// <inheritdoc />
        public async Task<OperationResult<HistorialAccesosDto>> ListarAsync(Guid usuarioId, HistorialAccesosRequest filtro, CancellationToken cancellationToken = default)
        {
            var validacion = await _validator.ValidateAsync(filtro, cancellationToken);
            if (!validacion.IsValid)
            {
                return OperationResult.Failure<HistorialAccesosDto>(
                    ErrorType.Validation, string.Join(" ", validacion.Errors.Select(e => e.ErrorMessage)));
            }

            var existe = await _contexto.Users.AnyAsync(u => u.Id == usuarioId, cancellationToken);
            if (!existe)
            {
                return OperationResult.Failure<HistorialAccesosDto>(ErrorType.NotFound, "El usuario no existe.");
            }

            var consulta = _contexto.RegistrosAcceso
                .AsNoTracking()
                .Where(r => r.UsuarioId == usuarioId);

            if (filtro.Exitoso.HasValue)
            {
                var exitoso = filtro.Exitoso.Value;
                consulta = consulta.Where(r => r.Exitoso == exitoso);
            }

            if (filtro.Desde.HasValue)
            {
                var desde = ComoUtc(filtro.Desde.Value);
                consulta = consulta.Where(r => r.Fecha >= desde);
            }

            if (filtro.Hasta.HasValue)
            {
                var hasta = ComoUtc(filtro.Hasta.Value);
                consulta = consulta.Where(r => r.Fecha <= hasta);
            }

            var total = await consulta.CountAsync(cancellationToken);

            // Más reciente primero; el Id desempata para que las páginas no repitan ni salten registros.
            var pagina = await consulta
                .OrderByDescending(r => r.Fecha)
                .ThenByDescending(r => r.Id)
                .Skip((filtro.Page - 1) * filtro.PageSize)
                .Take(filtro.PageSize)
                .ToListAsync(cancellationToken);

            // SQL Server devuelve las fechas sin zona (Kind = Unspecified); se marcan como UTC
            // para que el JSON salga con "Z" y el cliente no las confunda con hora local.
            var items = pagina
                .Select(r => new RegistroAccesoDto(
                    r.Id,
                    r.UsuarioId,
                    r.CorreoIntentado,
                    DateTime.SpecifyKind(r.Fecha, DateTimeKind.Utc),
                    r.Ip,
                    r.Exitoso,
                    r.Motivo))
                .ToList();

            return OperationResult.Success(new HistorialAccesosDto(items, total, filtro.Page, filtro.PageSize));
        }

        // El binding de fechas de ASP.NET Core convierte "…Z" a hora local del servidor; hay que
        // volver a UTC antes de comparar con la columna, que se guarda en UTC.
        private static DateTime ComoUtc(DateTime valor) => valor.Kind switch
        {
            DateTimeKind.Utc => valor,
            DateTimeKind.Local => valor.ToUniversalTime(),
            _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc)
        };
    }

}
