using NFe.Core.Interfaces;

namespace NFe.Worker;

public class NFeWorker : BackgroundService
{
    private readonly ILogger<NFeWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public NFeWorker(ILogger<NFeWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Worker NFe executando em: {time}", DateTimeOffset.Now);

                using var scope = _serviceProvider.CreateScope();
                var nfeService = scope.ServiceProvider.GetRequiredService<INFeService>();

                // Buscar vendas pendentes
                var vendasPendentes = await nfeService.ObterVendasPendentesAsync();

                foreach (var venda in vendasPendentes)
                {
                    try
                    {
                        _logger.LogInformation("Processando venda {VendaId}", venda.Id);

                        // Processar venda (gerar NFe simulada)
                        var sucesso = await nfeService.ProcessarVendaAsync(venda.Id);
                        
                        if (sucesso)
                        {
                            _logger.LogInformation("Venda {VendaId} processada com sucesso", venda.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Não foi possível processar venda {VendaId}", venda.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar venda {VendaId}", venda.Id);
                    }
                }

                if (vendasPendentes.Any())
                {
                    _logger.LogInformation("Processadas {Count} vendas pendentes", vendasPendentes.Count());
                }
                else
                {
                    _logger.LogInformation("Nenhuma venda pendente encontrada");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no worker NFe");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}