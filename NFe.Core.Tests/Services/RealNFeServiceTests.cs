using Microsoft.Extensions.Logging;
using Moq;
using NFe.Core.DTOs;
using NFe.Core.Entities;
using NFe.Core.Interfaces;
using NFe.Core.Models;
using NFe.Core.Services;
using Xunit;

namespace NFe.Core.Tests.Services;

public class RealNFeServiceTests
{
    private readonly Mock<IVendaRepository> _mockVendaRepository;
    private readonly Mock<IProtocoloRepository> _mockProtocoloRepository;
    private readonly Mock<INFeGenerator> _mockNFeGenerator;
    private readonly Mock<ICertificateService> _mockCertificateService;
    private readonly Mock<ISefazClient> _mockSefazClient;
    private readonly Mock<ILogger<RealNFeService>> _mockLogger;
    private readonly RealNFeService _service;

    public RealNFeServiceTests()
    {
        _mockVendaRepository = new Mock<IVendaRepository>();
        _mockProtocoloRepository = new Mock<IProtocoloRepository>();
        _mockNFeGenerator = new Mock<INFeGenerator>();
        _mockCertificateService = new Mock<ICertificateService>();
        _mockSefazClient = new Mock<ISefazClient>();
        _mockLogger = new Mock<ILogger<RealNFeService>>();

        _service = new RealNFeService(
            _mockVendaRepository.Object,
            _mockProtocoloRepository.Object,
            _mockNFeGenerator.Object,
            _mockCertificateService.Object,
            _mockSefazClient.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CriarVendaAsync_DeveRetornarGuidValido()
    {
        // Arrange
        var vendaDto = new VendaCreateDto
        {
            ClienteNome = "Cliente Teste",
            ClienteDocumento = "12345678901",
            ClienteEndereco = "Rua Teste, 123",
            Itens = new List<ItemVendaCreateDto>
            {
                new ItemVendaCreateDto
                {
                    Codigo = "PROD001",
                    Descricao = "Produto Teste",
                    Quantidade = 1,
                    ValorUnitario = 100.00m,
                    NCM = "12345678",
                    CFOP = "5102"
                }
            }
        };

        var vendaId = Guid.NewGuid();
        _mockVendaRepository.Setup(x => x.AddAsync(It.IsAny<Venda>()))
            .ReturnsAsync(vendaId);

        // Act
        var resultado = await _service.CriarVendaAsync(vendaDto);

        // Assert
        Assert.Equal(vendaId, resultado);
        _mockVendaRepository.Verify(x => x.AddAsync(It.IsAny<Venda>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarVendaAsync_ComVendaInexistente_DeveRetornarFalse()
    {
        // Arrange
        var vendaId = Guid.NewGuid();
        _mockVendaRepository.Setup(x => x.GetByIdAsync(vendaId))
            .ReturnsAsync((Venda?)null);

        // Act
        var resultado = await _service.ProcessarVendaAsync(vendaId);

        // Assert
        Assert.False(resultado);
    }

    [Fact]
    public async Task ProcessarVendaAsync_ComVendaNaoPendente_DeveRetornarFalse()
    {
        // Arrange
        var vendaId = Guid.NewGuid();
        var venda = new Venda { Id = vendaId, Status = "Autorizada" };
        
        _mockVendaRepository.Setup(x => x.GetByIdAsync(vendaId))
            .ReturnsAsync(venda);

        // Act
        var resultado = await _service.ProcessarVendaAsync(vendaId);

        // Assert
        Assert.False(resultado);
    }

    [Fact]
    public async Task ProcessarVendaAsync_ComSefazIndisponivel_DeveRetornarFalse()
    {
        // Arrange
        var vendaId = Guid.NewGuid();
        var venda = new Venda 
        { 
            Id = vendaId, 
            Status = "Pendente",
            ClienteNome = "Teste",
            ClienteDocumento = "12345678901",
            ClienteEndereco = "Rua Teste, 123"
        };
        
        _mockVendaRepository.Setup(x => x.GetByIdAsync(vendaId))
            .ReturnsAsync(venda);
        
        _mockSefazClient.Setup(x => x.VerificarStatusServicoAsync())
            .ReturnsAsync(false);

        // Act
        var resultado = await _service.ProcessarVendaAsync(vendaId);

        // Assert
        Assert.False(resultado);
        Assert.Equal(StatusSefaz.Erro, venda.Status);
        _mockVendaRepository.Verify(x => x.UpdateAsync(It.IsAny<Venda>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessarVendaAsync_ComDadosValidos_DeveProcessarComSucesso()
    {
        // Arrange
        var vendaId = Guid.NewGuid();
        var venda = new Venda 
        { 
            Id = vendaId, 
            Status = "Pendente",
            ClienteNome = "Cliente Teste",
            ClienteDocumento = "12345678901",
            ClienteEndereco = "Rua Teste, 123",
            Itens = new List<ItemVenda>
            {
                new ItemVenda
                {
                    Codigo = "PROD001",
                    Descricao = "Produto Teste",
                    Quantidade = 1,
                    ValorUnitario = 100.00m,
                    NCM = "12345678",
                    CFOP = "5102"
                }
            }
        };
        
        var xmlNFe = "<xml>NFe válido</xml>";
        var xmlAssinado = "<xml>NFe assinado</xml>";
        var resultadoEnvio = new SefazEnvioResult
        {
            Success = true,
            ChaveAcesso = "35200114200166000177550010000000001234567890",
            NumeroNFe = "1",
            NumeroProtocolo = "135200000000001",
            Status = StatusSefaz.Autorizada,
            MensagemSefaz = "Autorizado o uso da NF-e"
        };

        _mockVendaRepository.Setup(x => x.GetByIdAsync(vendaId))
            .ReturnsAsync(venda);
        
        _mockSefazClient.Setup(x => x.VerificarStatusServicoAsync())
            .ReturnsAsync(true);
        
        _mockNFeGenerator.Setup(x => x.ValidarDadosVenda(venda))
            .Returns(new List<string>());
        
        _mockNFeGenerator.Setup(x => x.GerarXmlNFeAsync(venda))
            .ReturnsAsync(xmlNFe);
        
        _mockCertificateService.Setup(x => x.AssinarXmlAsync(xmlNFe))
            .ReturnsAsync(xmlAssinado);
        
        _mockSefazClient.Setup(x => x.EnviarNFeAsync(xmlAssinado))
            .ReturnsAsync(resultadoEnvio);

        // Act
        var resultado = await _service.ProcessarVendaAsync(vendaId);

        // Assert
        Assert.True(resultado);
        Assert.Equal(StatusSefaz.Autorizada, venda.Status);
        Assert.Equal(resultadoEnvio.ChaveAcesso, venda.ChaveAcesso);
        Assert.Equal(resultadoEnvio.NumeroNFe, venda.NumeroNFe);
        
        _mockVendaRepository.Verify(x => x.UpdateAsync(It.IsAny<Venda>()), Times.AtLeastOnce);
        _mockProtocoloRepository.Verify(x => x.AddAsync(It.IsAny<Protocolo>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarVendaAsync_ComErroValidacao_DeveRetornarFalse()
    {
        // Arrange
        var vendaId = Guid.NewGuid();
        var venda = new Venda 
        { 
            Id = vendaId, 
            Status = "Pendente",
            ClienteNome = "Cliente Teste",
            ClienteDocumento = "12345678901",
            ClienteEndereco = "Rua Teste, 123"
        };
        
        var errosValidacao = new List<string> { "CNPJ inválido", "NCM obrigatório" };

        _mockVendaRepository.Setup(x => x.GetByIdAsync(vendaId))
            .ReturnsAsync(venda);
        
        _mockSefazClient.Setup(x => x.VerificarStatusServicoAsync())
            .ReturnsAsync(true);
        
        _mockNFeGenerator.Setup(x => x.ValidarDadosVenda(venda))
            .Returns(errosValidacao);

        // Act
        var resultado = await _service.ProcessarVendaAsync(vendaId);

        // Assert
        Assert.False(resultado);
        Assert.Equal(StatusSefaz.Erro, venda.Status);
        _mockVendaRepository.Verify(x => x.UpdateAsync(It.IsAny<Venda>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessarVendaAsync_ComNFeRejeitada_DeveRetornarFalse()
    {
        // Arrange
        var vendaId = Guid.NewGuid();
        var venda = new Venda 
        { 
            Id = vendaId, 
            Status = "Pendente",
            ClienteNome = "Cliente Teste",
            ClienteDocumento = "12345678901",
            ClienteEndereco = "Rua Teste, 123",
            Itens = new List<ItemVenda>
            {
                new ItemVenda
                {
                    Codigo = "PROD001",
                    Descricao = "Produto Teste",
                    Quantidade = 1,
                    ValorUnitario = 100.00m,
                    NCM = "12345678",
                    CFOP = "5102"
                }
            }
        };
        
        var xmlNFe = "<xml>NFe válido</xml>";
        var xmlAssinado = "<xml>NFe assinado</xml>";
        var resultadoEnvio = new SefazEnvioResult
        {
            Success = false,
            ChaveAcesso = "35200114200166000177550010000000001234567890",
            Status = StatusSefaz.Rejeitada,
            MensagemSefaz = "Rejeição: CNPJ do emitente inválido",
            CodigoStatus = 999
        };

        _mockVendaRepository.Setup(x => x.GetByIdAsync(vendaId))
            .ReturnsAsync(venda);
        
        _mockSefazClient.Setup(x => x.VerificarStatusServicoAsync())
            .ReturnsAsync(true);
        
        _mockNFeGenerator.Setup(x => x.ValidarDadosVenda(venda))
            .Returns(new List<string>());
        
        _mockNFeGenerator.Setup(x => x.GerarXmlNFeAsync(venda))
            .ReturnsAsync(xmlNFe);
        
        _mockCertificateService.Setup(x => x.AssinarXmlAsync(xmlNFe))
            .ReturnsAsync(xmlAssinado);
        
        _mockSefazClient.Setup(x => x.EnviarNFeAsync(xmlAssinado))
            .ReturnsAsync(resultadoEnvio);

        // Act
        var resultado = await _service.ProcessarVendaAsync(vendaId);

        // Assert
        Assert.False(resultado);
        Assert.Equal(StatusSefaz.Rejeitada, venda.Status);
        Assert.Equal(resultadoEnvio.ChaveAcesso, venda.ChaveAcesso);
        
        _mockVendaRepository.Verify(x => x.UpdateAsync(It.IsAny<Venda>()), Times.AtLeastOnce);
        _mockProtocoloRepository.Verify(x => x.AddAsync(It.IsAny<Protocolo>()), Times.Once);
    }
}