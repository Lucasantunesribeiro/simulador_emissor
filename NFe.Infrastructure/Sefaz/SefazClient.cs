using System.Net.Http;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;
using NFe.Core.Entities;
using NFe.Core.Interfaces;

namespace NFe.Infrastructure.Sefaz
{
    public class SefazClient : ISefazClient
    {
        private readonly ILogger<SefazClient> _logger;
        private readonly HttpClient _httpClient;
        // Flag de simulação (pode ser lida de config futuramente)
        private readonly bool _simulacao = true; // Troque para false e ajuste URLs para produção

        public SefazClient(ILogger<SefazClient> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<Protocolo> TransmitirNFeAsync(string xmlAssinado)
        {
            if (_simulacao)
            {
                _logger.LogWarning("[SIMULAÇÃO] Transmitindo NF-e para SEFAZ (mock)");
                // Simulação de resposta
                return await Task.FromResult(new Protocolo
                {
                    Id = Guid.NewGuid(),
                    NumeroRecibo = Guid.NewGuid().ToString(),
                    DataAutorizacao = DateTime.Now,
                    Status = "Autorizado",
                    Mensagem = "NF-e autorizada em ambiente de simulação",
                    XmlPath = "/storage/nfe/last.xml",
                    ChaveAcesso = Guid.NewGuid().ToString("N")
                });
            }
            if (string.IsNullOrWhiteSpace(xmlAssinado))
                throw new ArgumentException("XML assinado não pode ser vazio.", nameof(xmlAssinado));

            _logger.LogInformation("Transmitindo NF-e para SEFAZ...");

            try
            {
                // Exemplo de request real (ajuste URL e headers conforme ambiente SEFAZ)
                var request = new HttpRequestMessage(HttpMethod.Post, "https://sefaz.exemplo.gov.br/nfe")
                {
                    Content = new StringContent(xmlAssinado, Encoding.UTF8, "application/xml")
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseXml = await response.Content.ReadAsStringAsync();

                // Parse do XML de resposta (ajuste conforme schema real)
                var protocolo = ParseProtocoloFromXml(responseXml);

                _logger.LogInformation("NF-e transmitida com sucesso. Recibo: {Recibo}", protocolo.NumeroRecibo);

                return protocolo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao transmitir NF-e para SEFAZ");
                throw;
            }
        }

        public async Task<Protocolo> ConsultarReciboAsync(string numeroRecibo)
        {
            if (_simulacao)
            {
                _logger.LogWarning($"[SIMULAÇÃO] Consultando recibo {numeroRecibo} na SEFAZ (mock)");
                return await Task.FromResult(new Protocolo
                {
                    Id = Guid.NewGuid(),
                    NumeroRecibo = numeroRecibo,
                    DataAutorizacao = DateTime.Now,
                    Status = "Autorizado",
                    Mensagem = "Consulta de recibo em ambiente de simulação",
                    XmlPath = "/storage/nfe/last.xml",
                    ChaveAcesso = Guid.NewGuid().ToString("N")
                });
            }
            if (string.IsNullOrWhiteSpace(numeroRecibo))
                throw new ArgumentException("Número do recibo não pode ser vazio.", nameof(numeroRecibo));

            _logger.LogInformation("Consultando recibo {Recibo} na SEFAZ...", numeroRecibo);

            try
            {
                // Exemplo de consulta real (ajuste URL e params conforme ambiente SEFAZ)
                var response = await _httpClient.GetAsync($"https://sefaz.exemplo.gov.br/nfe/recibo/{numeroRecibo}");
                response.EnsureSuccessStatusCode();

                var responseXml = await response.Content.ReadAsStringAsync();

                var protocolo = ParseProtocoloFromXml(responseXml);

                _logger.LogInformation("Consulta concluída. Status: {Status}", protocolo.Status);

                return protocolo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar recibo na SEFAZ");
                throw;
            }
        }

        private Protocolo ParseProtocoloFromXml(string xml)
        {
            // TODO: Implementar parsing real do XML de resposta da SEFAZ
            // Exemplo fictício:
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            // Ajuste os selects conforme o schema real da resposta SEFAZ
            var protocolo = new Protocolo
            {
                Id = Guid.NewGuid(),
                NumeroRecibo = doc.SelectSingleNode("//recibo")?.InnerText ?? "N/A",
                DataAutorizacao = DateTime.Now, // Parse real do XML
                Status = doc.SelectSingleNode("//status")?.InnerText ?? "N/A",
                Mensagem = doc.SelectSingleNode("//mensagem")?.InnerText ?? "N/A",
                XmlPath = "/storage/nfe/last.xml",
                ChaveAcesso = doc.SelectSingleNode("//chaveAcesso")?.InnerText ?? "N/A"
            };

            return protocolo;
        }
    }
}
