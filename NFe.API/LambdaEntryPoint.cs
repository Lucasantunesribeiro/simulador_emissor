using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using NFe.Core.Interfaces;
using NFe.Core.Services;
using NFe.Infrastructure.Repositories;
using NFe.Infrastructure.Data;
using HealthChecks.UI.Client;

namespace NFe.API;

public class LambdaEntryPoint : APIGatewayProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            // Entity Framework
            var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<NFeDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Services
            services.AddScoped<INFeService, SimulacaoNFeService>();
            
            // Repositories
            services.AddScoped<IVendaRepository, VendaRepositoryEF>();
            services.AddScoped<IProtocoloRepository, ProtocoloRepositoryEF>();
            


            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddCheck("sefaz", () => HealthCheckResult.Healthy("SEFAZ está disponível"))
                .AddNpgSql(connectionString ?? "", name: "database");
        })
        .Configure(app =>
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseAuthorization();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health", new HealthCheckOptions
                {
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });
            });
        });
    }
}