using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace GIRA.Tests.Seguridad
{
    public class AutorizacionPoliticasTests : IAsyncLifetime
    {
        private const string Password = "Clave-Segura1";
        private const string SoloAdministrador = "/api/prueba-politicas/solo-administrador";
        private const string GerenteOAdministrador = "/api/prueba-politicas/gerente-o-administrador";
        private const string CualquierRol = "/api/prueba-politicas/cualquier-rol";

        private readonly GiraApiFactoryConPruebas _factory = new();
        private HttpClient _cliente = null!;

        public async Task InitializeAsync()
        {
            _cliente = _factory.CreateClient();
            await _factory.CrearUsuarioAsync("admin@gira.local", Password, true, Roles.Administrador);
            await _factory.CrearUsuarioAsync("gerente@gira.local", Password, true, Roles.Gerente);
            await _factory.CrearUsuarioAsync("supervisor@gira.local", Password, true, Roles.Supervisor);
            await _factory.CrearUsuarioAsync("mesero@gira.local", Password, true, Roles.Mesero);
            await _factory.CrearUsuarioAsync("cocina@gira.local", Password, true, Roles.Cocina);
        }

        public async Task DisposeAsync()
        {
            _cliente.Dispose();
            await _factory.DisposeAsync();
        }

        private async Task<HttpResponseMessage> GetComoAsync(string correo, string ruta)
        {
            _cliente.UsarToken(await _cliente.ObtenerTokenAsync(correo, Password));
            return await _cliente.GetAsync(ruta);
        }

        [Theory]
        [InlineData(SoloAdministrador)]
        [InlineData(GerenteOAdministrador)]
        [InlineData(CualquierRol)]
        public async Task SinToken_Devuelve401ConProblemDetails(string ruta)
        {
            var respuesta = await _cliente.GetAsync(ruta);

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
            var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(401, problema!.Status);
            Assert.Equal("No autenticado", problema.Title);
        }

        [Fact]
        public async Task TokenInvalido_Devuelve401()
        {
            _cliente.UsarToken("esto.no.es-un-token");

            var respuesta = await _cliente.GetAsync(CualquierRol);

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }

        [Theory]
        [InlineData("gerente@gira.local", SoloAdministrador)]
        [InlineData("supervisor@gira.local", SoloAdministrador)]
        [InlineData("mesero@gira.local", SoloAdministrador)]
        [InlineData("cocina@gira.local", SoloAdministrador)]
        [InlineData("supervisor@gira.local", GerenteOAdministrador)]
        [InlineData("mesero@gira.local", GerenteOAdministrador)]
        [InlineData("cocina@gira.local", GerenteOAdministrador)]
        public async Task RolSinPermiso_Devuelve403ConProblemDetails(string correo, string ruta)
        {
            var respuesta = await GetComoAsync(correo, ruta);

            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
            Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
            var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(403, problema!.Status);
            Assert.Equal("Acceso denegado", problema.Title);
        }

        [Theory]
        [InlineData("admin@gira.local", SoloAdministrador)]
        [InlineData("admin@gira.local", GerenteOAdministrador)]
        [InlineData("gerente@gira.local", GerenteOAdministrador)]
        [InlineData("admin@gira.local", CualquierRol)]
        [InlineData("gerente@gira.local", CualquierRol)]
        [InlineData("supervisor@gira.local", CualquierRol)]
        [InlineData("mesero@gira.local", CualquierRol)]
        [InlineData("cocina@gira.local", CualquierRol)]
        public async Task RolConPermiso_Devuelve200(string correo, string ruta)
        {
            var respuesta = await GetComoAsync(correo, ruta);

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
    }

}
