using LogiTrack.Data;
using LogiTrack.Data.Repositories;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

var configuration = builder.Configuration;

// Add services to the container.
services.AddControllers();

services.AddEndpointsApiExplorer();

services.AddSwaggerGen();

var connectionString = configuration.GetConnectionString("LogiTrack");

services.AddSqlite<LogiTrackContext>(connectionString);

services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<LogiTrackContext>();

services.AddAuthorizationBuilder()
    .AddPolicy("ApiAccess", policy => policy.RequireAuthenticatedUser());

services.AddMemoryCache();

services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

services.AddScoped<DbSeeder>();

services.AddScoped<IInventoryRepository, InventoryRepository>();

services.AddScoped<IOrderRepository, OrderRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseResponseCompression();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapIdentityApi<ApplicationUser>();

using var scope = app.Services.CreateScope();

var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();

await seeder.SeedAsync();

await app.RunAsync();

// Make Program accessible for integration tests
public partial class Program { }
