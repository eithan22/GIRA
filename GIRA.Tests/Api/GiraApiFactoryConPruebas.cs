using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Tests.Api
{
    /// </summary>
    public class GiraApiFactoryConPruebas : GiraApiFactory
    {
        /// <inheritdoc />
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
                services.AddControllers().AddApplicationPart(typeof(PruebaPoliticasController).Assembly));
        }
    }

    /// <summary>Endpoints de ejemplo, uno por cada forma de política: solo Administrador, Gerente o superior, y todo el personal.</summary>
    [ApiController]
    [Route("api/prueba-politicas")]
    public class PruebaPoliticasController : ControllerBase
    {
        /// <summary>Política <see cref="Politicas.GestionarUsuarios"/>: solo Administrador.</summary>
        [HttpGet("solo-administrador")]
        [Authorize(Policy = Politicas.GestionarUsuarios)]
        public IActionResult SoloAdministrador() => Ok();

        /// <summary>Política <see cref="Politicas.GestionarInventario"/>: Administrador y Gerente.</summary>
        [HttpGet("gerente-o-administrador")]
        [Authorize(Policy = Politicas.GestionarInventario)]
        public IActionResult GerenteOAdministrador() => Ok();

        /// <summary>Política <see cref="Politicas.ConsultarMisTurnos"/>: cualquier rol del personal.</summary>
        [HttpGet("cualquier-rol")]
        [Authorize(Policy = Politicas.ConsultarMisTurnos)]
        public IActionResult CualquierRol() => Ok();

        /// <summary>Sin [Authorize] ni [AllowAnonymous]: debe quedar protegido solo por el FallbackPolicy.</summary>
        [HttpGet("sin-proteger")]
        public IActionResult SinProteger() => Ok();

        /// <summary>Lanza una excepción para probar el manejador global; anónimo para aislar la prueba de la autenticación.</summary>
        [HttpGet("lanza-excepcion")]
        [AllowAnonymous]
        public IActionResult LanzaExcepcion() =>
            throw new InvalidOperationException("mensaje-interno-de-prueba-nunca-debe-salir-al-cliente");
    }

}
