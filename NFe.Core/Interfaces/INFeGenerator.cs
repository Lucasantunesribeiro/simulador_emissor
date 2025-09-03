using NFe.Core.Entities;

namespace NFe.Core.Interfaces;

/// <summary>
/// Interface para geração de XML NFe
/// </summary>
public interface INFeGenerator
{
    /// <summary>
    /// Gera XML NFe a partir dos dados da venda
    /// </summary>
    /// <param name="venda">Dados da venda</param>
    /// <returns>XML NFe válido conforme layout 4.00</returns>
    Task<string> GerarXmlNFeAsync(Venda venda);
    
    /// <summary>
    /// Calcula e gera chave de acesso de 44 dígitos
    /// </summary>
    /// <param name="venda">Dados da venda</param>
    /// <returns>Chave de acesso de 44 dígitos</returns>
    string GerarChaveAcesso(Venda venda);
    
    /// <summary>
    /// Valida dados obrigatórios da venda antes de gerar NFe
    /// </summary>
    /// <param name="venda">Dados da venda</param>
    /// <returns>Lista de erros de validação (vazia se válido)</returns>
    List<string> ValidarDadosVenda(Venda venda);
}