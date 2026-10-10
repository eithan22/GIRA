using GIRA.Infrastructure;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
    await IdentitySeeder.SeedAsync(roleManager, userManager, app.Configuration);
}

app.UseHttpsRedirection();

app.UseCors("Frontend");
app.UseAuthorization();

app.MapControllers();

app.Run();
