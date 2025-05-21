using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NFe.Core.Interfaces;
using NFe.Core.Entities;
using System.Linq;

namespace NFe.Worker
{
    public class NFeWorker : BackgroundService
    {
        private readonly ILogger<NFeWorker> _logger;
        private readonly IVendaRepository _vendaRepository;
        private readonly INFeService _nfeService;
        private readonly IProtocoloRepository _protocoloRepository;
        
        public NFeWorker(
            ILogger<NFeWorker> logger,
            IVendaRepository vendaRepository,
            INFeService nfeService,
            IProtocoloRepository protocoloRepository)
        {
            _logger = logger;
            _vendaRepository = vendaRepository;
            _nfeService = nfeService;
            _protocoloRepository = protocoloRepository;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serviço de processamento de NF-e iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Verificando vendas pendentes");
                
                try
                {
                    var vendasPendentes = await _vendaRepository.GetPendentesAsync();
                    
                    foreach (var venda in vendasPendentes)
                    {
                        await ProcessarVenda(venda);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar vendas pendentes");
                }
                
                await Task.Delay(10000, stoppingToken); // Aguarda 10 segundos antes da próxima verificação
            }
        }
        
        private async Task ProcessarVenda(Venda venda)
        {
            _logger.LogInformation($"Processando venda {venda.Id}");
            
            try
            {
                // Gerar XML
                var xml = await _nfeService.GerarXml(venda);
                
                // Assinar XML
                var xmlAssinado = await _nfeService.AssinarXml(xml);
                
                // Transmitir para SEFAZ
                var protocolo = await _nfeService.TransmitirXml(xmlAssinado);
                
                // Salvar protocolo
                await _protocoloRepository.AddAsync(protocolo);
                
                // Atualizar venda
                venda.Processada = true;
                venda.NumeroNota = "1"; // Em um cenário real, seria extraído do XML
                venda.ChaveAcesso = protocolo.ChaveAcesso;
                
                await _vendaRepository.UpdateAsync(venda);
                
                _logger.LogInformation($"Venda {venda.Id} processada com sucesso. Protocolo: {protocolo.NumeroRecibo}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar venda {venda.Id}");
            }
        }
    }
}
