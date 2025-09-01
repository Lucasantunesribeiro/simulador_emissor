using NFe.Core.Entities;
using NFe.Core.Interfaces;
using NFe.Core.DTOs;

namespace NFe.Core.Services;

public class SimulacaoNFeService : INFeService
{
    private readonly IVendaRepository _vendaRepository;
    private readonly IProtocoloRepository _protocoloRepository;

    public SimulacaoNFeService(IVendaRepository vendaRepository, IProtocoloRepository protocoloRepository)
    {
        _vendaRepository = vendaRepository;
        _protocoloRepository = protocoloRepository;
    }

    public async Task<Guid> CriarVendaAsync(VendaCreateDto vendaDto)
    {
        var venda = new Venda
        {
            ClienteNome = vendaDto.ClienteNome,
            ClienteDocumento = vendaDto.ClienteDocumento,
            ClienteEndereco = vendaDto.ClienteEndereco,
            Observacoes = vendaDto.Observacoes,
            Status = "Pendente"
        };

        // Adicionar itens
        foreach (var itemDto in vendaDto.Itens)
        {
            venda.Itens.Add(new ItemVenda
            {
                VendaId = venda.Id,
                Codigo = itemDto.Codigo,
                Descricao = itemDto.Descricao,
                Quantidade = itemDto.Quantidade,
                ValorUnitario = itemDto.ValorUnitario,
                NCM = itemDto.NCM,
                CFOP = itemDto.CFOP,
                UnidadeMedida = itemDto.UnidadeMedida
            });
        }

        return await _vendaRepository.AddAsync(venda);
    }

    public async Task<VendaResponseDto?> ObterVendaAsync(Guid id)
    {
        var venda = await _vendaRepository.GetByIdAsync(id);
        if (venda == null) return null;

        return MapearVendaParaDto(venda);
    }

    public async Task<IEnumerable<VendaResponseDto>> ObterTodasVendasAsync()
    {
        var vendas = await _vendaRepository.GetAllAsync();
        return vendas.Select(MapearVendaParaDto);
    }

    public async Task<IEnumerable<VendaResponseDto>> ObterVendasPendentesAsync()
    {
        var vendas = await _vendaRepository.GetPendentesAsync();
        return vendas.Select(MapearVendaParaDto);
    }

    public async Task<bool> ProcessarVendaAsync(Guid vendaId)
    {
        var venda = await _vendaRepository.GetByIdAsync(vendaId);
        if (venda == null || venda.Status != "Pendente") return false;

        // Simular processamento
        venda.Status = "Processando";
        await _vendaRepository.UpdateAsync(venda);

        // Gerar chave de acesso simulada (44 dígitos)
        var chaveAcesso = GerarChaveAcessoSimulada();
        venda.ChaveAcesso = chaveAcesso;
        venda.NumeroNFe = GerarNumeroNFeSimulado();
        venda.Status = "Autorizada";

        await _vendaRepository.UpdateAsync(venda);

        // Criar protocolo simulado
        var protocolo = new Protocolo
        {
            VendaId = venda.Id,
            ChaveAcesso = chaveAcesso,
            NumeroProtocolo = GerarNumeroProtocoloSimulado(),
            Status = "Autorizada",
            MensagemSefaz = "NFe autorizada com sucesso (SIMULAÇÃO)",
            XmlNFe = GerarXmlNFeSimulado(venda),
            XmlProtocolo = GerarXmlProtocoloSimulado()
        };

        await _protocoloRepository.AddAsync(protocolo);

        return true;
    }

    public async Task<ProtocoloResponseDto?> ObterProtocoloAsync(Guid protocoloId)
    {
        var protocolo = await _protocoloRepository.GetByIdAsync(protocoloId);
        if (protocolo == null) return null;

        return new ProtocoloResponseDto
        {
            Id = protocolo.Id,
            VendaId = protocolo.VendaId,
            ChaveAcesso = protocolo.ChaveAcesso,
            NumeroProtocolo = protocolo.NumeroProtocolo,
            DataProtocolo = protocolo.DataProtocolo,
            Status = protocolo.Status,
            MensagemSefaz = protocolo.MensagemSefaz
        };
    }

    public async Task<ProtocoloResponseDto?> ObterProtocoloPorChaveAsync(string chaveAcesso)
    {
        var protocolo = await _protocoloRepository.GetByChaveAcessoAsync(chaveAcesso);
        if (protocolo == null) return null;

        return new ProtocoloResponseDto
        {
            Id = protocolo.Id,
            VendaId = protocolo.VendaId,
            ChaveAcesso = protocolo.ChaveAcesso,
            NumeroProtocolo = protocolo.NumeroProtocolo,
            DataProtocolo = protocolo.DataProtocolo,
            Status = protocolo.Status,
            MensagemSefaz = protocolo.MensagemSefaz
        };
    }

    public async Task<IEnumerable<ProtocoloResponseDto>> ObterTodosProtocolosAsync()
    {
        var protocolos = await _protocoloRepository.GetAllAsync();
        return protocolos.Select(p => new ProtocoloResponseDto
        {
            Id = p.Id,
            VendaId = p.VendaId,
            ChaveAcesso = p.ChaveAcesso,
            NumeroProtocolo = p.NumeroProtocolo,
            DataProtocolo = p.DataProtocolo,
            Status = p.Status,
            MensagemSefaz = p.MensagemSefaz
        });
    }

    private VendaResponseDto MapearVendaParaDto(Venda venda)
    {
        return new VendaResponseDto
        {
            Id = venda.Id,
            ClienteNome = venda.ClienteNome,
            ClienteDocumento = venda.ClienteDocumento,
            ClienteEndereco = venda.ClienteEndereco,
            ValorTotal = venda.ValorTotal,
            DataVenda = venda.DataVenda,
            Status = venda.Status,
            ChaveAcesso = venda.ChaveAcesso,
            NumeroNFe = venda.NumeroNFe,
            SerieNFe = venda.SerieNFe,
            Observacoes = venda.Observacoes,
            Itens = venda.Itens.Select(i => new ItemVendaResponseDto
            {
                Id = i.Id,
                Codigo = i.Codigo,
                Descricao = i.Descricao,
                Quantidade = i.Quantidade,
                ValorUnitario = i.ValorUnitario,
                ValorTotal = i.ValorTotal,
                NCM = i.NCM,
                CFOP = i.CFOP,
                UnidadeMedida = i.UnidadeMedida
            }).ToList()
        };
    }

    private string GerarChaveAcessoSimulada()
    {
        // Gerar chave de acesso simulada de 44 dígitos
        var random = new Random();
        var chave = "";
        for (int i = 0; i < 44; i++)
        {
            chave += random.Next(0, 10).ToString();
        }
        return chave;
    }

    private string GerarNumeroNFeSimulado()
    {
        var random = new Random();
        return random.Next(1, 999999).ToString("D6");
    }

    private string GerarNumeroProtocoloSimulado()
    {
        var random = new Random();
        return DateTime.Now.ToString("yyyyMMddHHmmss") + random.Next(1000, 9999).ToString();
    }

    private string GerarXmlNFeSimulado(Venda venda)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<NFe xmlns=""http://www.portalfiscal.inf.br/nfe"">
    <infNFe Id=""NFe{venda.ChaveAcesso}"">
        <ide>
            <cUF>35</cUF>
            <cNF>{venda.ChaveAcesso?.Substring(35, 8)}</cNF>
            <natOp>Venda</natOp>
            <mod>55</mod>
            <serie>{venda.SerieNFe}</serie>
            <nNF>{venda.NumeroNFe}</nNF>
            <dhEmi>{venda.DataVenda:yyyy-MM-ddTHH:mm:sszzz}</dhEmi>
            <tpNF>1</tpNF>
            <idDest>1</idDest>
            <cMunFG>3550308</cMunFG>
            <tpImp>1</tpImp>
            <tpEmis>1</tpEmis>
            <cDV>{venda.ChaveAcesso?.Substring(43, 1)}</cDV>
            <tpAmb>2</tpAmb>
            <finNFe>1</finNFe>
            <indFinal>1</indFinal>
            <indPres>1</indPres>
        </ide>
        <dest>
            <xNome>{venda.ClienteNome}</xNome>
            <enderDest>
                <xLgr>{venda.ClienteEndereco}</xLgr>
                <nro>123</nro>
                <xBairro>Centro</xBairro>
                <cMun>3550308</cMun>
                <xMun>São Paulo</xMun>
                <UF>SP</UF>
                <CEP>01000000</CEP>
            </enderDest>
        </dest>
        <total>
            <ICMSTot>
                <vBC>0.00</vBC>
                <vICMS>0.00</vICMS>
                <vICMSDeson>0.00</vICMSDeson>
                <vBCST>0.00</vBCST>
                <vST>0.00</vST>
                <vProd>{venda.ValorTotal:F2}</vProd>
                <vFrete>0.00</vFrete>
                <vSeg>0.00</vSeg>
                <vDesc>0.00</vDesc>
                <vII>0.00</vII>
                <vIPI>0.00</vIPI>
                <vPIS>0.00</vPIS>
                <vCOFINS>0.00</vCOFINS>
                <vOutro>0.00</vOutro>
                <vNF>{venda.ValorTotal:F2}</vNF>
            </ICMSTot>
        </total>
    </infNFe>
</NFe>";
    }

    private string GerarXmlProtocoloSimulado()
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<protNFe xmlns=""http://www.portalfiscal.inf.br/nfe"">
    <infProt>
        <tpAmb>2</tpAmb>
        <verAplic>SP_NFE_PL_008i2</verAplic>
        <chNFe>{GerarChaveAcessoSimulada()}</chNFe>
        <dhRecbto>{DateTime.Now:yyyy-MM-ddTHH:mm:sszzz}</dhRecbto>
        <nProt>{GerarNumeroProtocoloSimulado()}</nProt>
        <digVal>SIMULAÇÃO</digVal>
        <cStat>100</cStat>
        <xMotivo>Autorizado o uso da NF-e (SIMULAÇÃO)</xMotivo>
    </infProt>
</protNFe>";
    }
}