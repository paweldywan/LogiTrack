using LogiTrack.Data;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

var configuration = builder.Configuration;

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
services.AddOpenApi();

var connectionString = configuration.GetConnectionString("LogiTrack");

services.AddSqlite<LogiTrackContext>(connectionString);

services.AddScoped<DbSeeder>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

using var scope = app.Services.CreateScope();

var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();

await seeder.SeedAsync();

await app.RunAsync();
