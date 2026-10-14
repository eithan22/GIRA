using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Interfaces.Services.Seguridad
{
    public interface IRegistroAccesoService
    {
        /// <summary>
        /// Guarda un intento de inicio de sesión en la auditoría.
        /// </summary>
        /// <param name="usuarioId">Usuario del intento; null si el correo no corresponde a ningún usuario.</param>
        /// <param name="correo">Correo tal como se escribió en el intento.</param>
        /// <param name="ip">Dirección IP desde la que se intentó.</param>
        /// <param name="exitoso">Indica si el inicio de sesión fue exitoso.</param>
        /// <param name="motivo">Motivo del fallo; null si fue exitoso.</param>
        Task RegistrarAsync(Guid? usuarioId, string correo, string ip, bool exitoso, string? motivo, CancellationToken cancellationToken = default);
    }
}
