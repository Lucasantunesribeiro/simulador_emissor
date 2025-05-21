using System;
using System.Threading.Tasks;
using NFe.Core.Entities;
using NFe.Core.Interfaces;

namespace NFe.Core.Services
{
    public class NFeService : INFeService
    {
        public Task<string> GerarXml(Venda venda)
        {
            // Implementação baseada no exemplo do arquivo
            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<NFe xmlns=""http://www.portalfiscal.inf.br/nfe"">
    <infNFe Id=""NFe{GerarCodigoUnico()}"">
        <ide>
            <cUF>35</cUF>
            <natOp>Venda</natOp>
            <mod>55</mod>
            <serie>1</serie>
            <nNF>1</nNF>
            <dhEmi>{DateTime.Now:yyyy-MM-ddTHH:mm:sszzz}</dhEmi>
        </ide>
        <emit>
            <CNPJ>12345678000199</CNPJ>
            <xNome>EMPRESA EMITENTE LTDA</xNome>
        </emit>
        <dest>
            <CNPJ>{venda.ClienteDocumento}</CNPJ>
            <xNome>{venda.ClienteNome}</xNome>
        </dest>
        <total>
            <ICMSTot>
                <vNF>{venda.ValorTotal}</vNF>
            </ICMSTot>
        </total>
    </infNFe>
</NFe>";

            return Task.FromResult(xml);
        }

        public Task<string> AssinarXml(string xml)
        {
            // Simulação de assinatura
            return Task.FromResult($"<!-- Assinado digitalmente -->\n{xml}");
        }

        public Task<Protocolo> TransmitirXml(string xml)
        {
            // Simulação de transmissão
            var protocolo = new Protocolo
            {
                Id = Guid.NewGuid(),
                NumeroRecibo = "123",
                DataAutorizacao = DateTime.Now,
                XmlPath = "caminho.xml",
                Status = "Autorizado",
                Mensagem = "Mensagem",
                ChaveAcesso = "CHAVE-GERADA-AQUI" // <-- Adicione isso!
            };

            return Task.FromResult(protocolo);
        }

        public Task<Protocolo> ConsultarProcessamento(string numeroRecibo)
        {
            // Simulação de consulta
            var protocolo = new Protocolo
            {
                Id = Guid.NewGuid(),
                NumeroRecibo = "123",
                DataAutorizacao = DateTime.Now,
                XmlPath = "caminho.xml",
                Status = "Autorizado",
                Mensagem = "Mensagem",
                ChaveAcesso = "CHAVE-GERADA-AQUI" // <-- Adicione isso!
            };

            return Task.FromResult(protocolo);
        }

        private string GerarCodigoUnico()
        {
            return DateTime.Now.Ticks.ToString();
        }
    }
}
