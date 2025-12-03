using LogiTrack.Data;
using LogiTrack.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

var configuration = builder.Configuration;

// Add services to the container.
services.AddControllers();

services.AddEndpointsApiExplorer();

services.AddSwaggerGen();

var connectionString = configuration.GetConnectionString("LogiTrack");

services.AddSqlite<LogiTrackContext>(connectionString);

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

app.MapControllers();

using var scope = app.Services.CreateScope();

var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();

await seeder.SeedAsync();

await app.RunAsync();
