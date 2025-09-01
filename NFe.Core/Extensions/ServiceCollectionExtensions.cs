using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NFe.Core.Configuration;
using NFe.Core.Interfaces;
using NFe.Core.Services;

namespace NFe.Core.Extensions;

/// <summary>
/// Extensions para configuração de serviços NFe
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configura os serviços NFe baseado na feature flag
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection configurado</returns>
    public static IServiceCollection AddNFeServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurar settings SEFAZ
        services.Configure<SefazSettings>(configuration.GetSection("Sefaz"));

        // Feature flag para determinar qual implementação usar
        var useRealNFe = configuration.GetValue<bool>("NFe:UseReal", false);

        if (useRealNFe)
        {
            // Produção - usar integração real com SEFAZ
            services.AddScoped<INFeService, RealNFeService>();
            services.AddScoped<ISefazClient, SefazClient>();
            services.AddScoped<INFeGenerator, NFeGenerator>();
            
            // Log da configuração
            using var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger<ServiceCollectionExtensions>>();
            logger?.LogInformation("NFe configurado para PRODUÇÃO - Integração real com SEFAZ ativa");
        }
        else
        {
            // Desenvolvimento - manter simulação
            services.AddScoped<INFeService, SimulacaoNFeService>();
            
            // Log da configuração  
            using var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger<ServiceCollectionExtensions>>();
            logger?.LogInformation("NFe configurado para DESENVOLVIMENTO - Simulação ativa");
        }

        return services;
    }

    /// <summary>
    /// Adiciona validações de configuração NFe
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection configurado</returns>
    public static IServiceCollection AddNFeValidation(this IServiceCollection services, IConfiguration configuration)
    {
        var useRealNFe = configuration.GetValue<bool>("NFe:UseReal", false);

        if (useRealNFe)
        {
            // Validar configurações obrigatórias para produção
            var sefazSection = configuration.GetSection("Sefaz");
            
            ValidateConfiguration(sefazSection, "CNPJ", "CNPJ do emitente é obrigatório para produção");
            ValidateConfiguration(sefazSection, "RazaoSocial", "Razão social do emitente é obrigatória para produção");
            ValidateConfiguration(sefazSection, "InscricaoEstadual", "Inscrição estadual do emitente é obrigatória para produção");
            ValidateConfiguration(sefazSection, "CertificateSecretName", "Nome do certificado no AWS Secrets Manager é obrigatório");
            
            // Validar ambiente (deve ser homologação = 2)
            var ambiente = sefazSection.GetValue<int>("Ambiente", 2);
            if (ambiente != 2)
            {
                throw new InvalidOperationException("Por segurança, apenas ambiente de homologação (2) é permitido");
            }
        }

        return services;
    }

    private static void ValidateConfiguration(IConfigurationSection section, string key, string errorMessage)
    {
        var value = section.GetValue<string>(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }
}