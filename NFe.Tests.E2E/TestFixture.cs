using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NFe.API;
using NFe.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace NFe.Tests.E2E;

public class TestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer;
    public HttpClient HttpClient { get; private set; } = null!;
    
    public TestFixture()
    {
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("nfedb_test")
            .WithUsername("nfeadmin")
            .WithPassword("nfepass123")
            .WithPortBinding(5433, 5432)
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Homologation");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.Homologation.json", optional: false);
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgreSqlContainer.GetConnectionString(),
                ["NFe:UseReal"] = "true",
                ["Sefaz:Ambiente"] = "2", // Homologação
                ["AWS:Region"] = "us-east-1",
                ["JwtSettings:SecretKey"] = "nfe-jwt-super-secret-key-for-testing-only-256-bits-long-homolog",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove o contexto real e adiciona o de teste
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(NFeDbContext));
            if (descriptor != null) services.Remove(descriptor);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();
        HttpClient = CreateClient();
        
        // Aplicar migrations
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NFeDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        HttpClient.Dispose();
        await _postgreSqlContainer.StopAsync();
        await _postgreSqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
