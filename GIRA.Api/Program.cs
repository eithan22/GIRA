using GIRA.Api.Authorization;
using GIRA.Api.Common;
using GIRA.Api.Common.Reservas;
using GIRA.Api.Common.Seguridad;
using GIRA.Application;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// OpenAPI con esquema Bearer para probar con el token en /swagger (GI-125)
builder.Services.AddOpenApiConBearer();

builder.Services.AddExceptionHandler<ManejadorExcepcionesGlobal>();
builder.Services.AddProblemDetails();

builder.Services.AddGiraCors(builder.Configuration);

// Servicios de Application e Infrastructure.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Autenticación JWT y límite de intentos del login (GI-95).
builder.Services.AddAutenticacionJwt(builder.Configuration);
builder.Services.AddLimiteLogin();

// Límite de solicitudes de recuperación de contraseña (GI-215).
builder.Services.AddLimiteOlvidePassword();

// Límite del endpoint público de disponibilidad (GI-157).
builder.Services.AddLimiteReservas();

//servicios de autorización con políticas y roles.
builder.Services.AddAutorizacionGira();


var app = builder.Build();

// Al inicio de la cadena: cualquier excepción no controlada de lo que sigue queda cubierta.
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerGira();

    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
    await IdentitySeeder.SeedAsync(roleManager, userManager, app.Configuration);

    await ReservasSeeder.SeedAsync(dbContext);
}

// En Development no se redirige a https para que Swagger UI funcione con cualquier perfil de launchSettings; en el resto de entornos se mantiene.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(CorsExtensions.Politica);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();
