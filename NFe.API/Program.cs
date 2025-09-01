using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using NFe.Core.Interfaces;
using NFe.Core.Services;
using NFe.Core.Extensions;
using NFe.Infrastructure.Repositories;
using NFe.Infrastructure.Data;
using NFe.Infrastructure.Services;
using HealthChecks.UI.Client;
using Amazon.SecretsManager;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Entity Framework
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<NFeDbContext>(options =>
    options.UseNpgsql(connectionString));

// AWS Services
builder.Services.AddAWSService<IAmazonSecretsManager>();

// NFe Services with feature flag
builder.Services.AddNFeServices(builder.Configuration);
builder.Services.AddNFeValidation(builder.Configuration);

// Certificate Service
builder.Services.AddScoped<ICertificateService, CertificateService>();

// Repositories
builder.Services.AddScoped<IVendaRepository, VendaRepositoryEF>();
builder.Services.AddScoped<IProtocoloRepository, ProtocoloRepositoryEF>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddCheck("sefaz", () => HealthCheckResult.Healthy("SEFAZ está disponível"))
    .AddNpgSql(connectionString ?? "", name: "database");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
