using NFe.Core.Services;
using NFe.Core.Entities;

public class UnitNFeServiceTests
{
    [Fact]
    public async Task GerarXml_DeveConterNomeEValor()
    {
        var service = new NFeService();
        var venda = new Venda
        {
            ClienteDocumento = "12345678901",
            ClienteNome = "João Teste",
            ValorTotal = 123.45m,
            Observacoes = "Teste unitário",
            NumeroNota = "1",
            ChaveAcesso = "CHAVE-TESTE"
        };

        var xml = await service.GerarXml(venda);

        Assert.Contains("João Teste", xml);
        Assert.True(xml.Contains("123.45") || xml.Contains("123,45"), $"Valor total não encontrado no XML: {xml}");
    }
} 