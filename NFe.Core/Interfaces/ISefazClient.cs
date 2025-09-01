using NFe.Core.Models;

namespace NFe.Core.Interfaces;

/// <summary>
/// Interface para comunicação com SEFAZ
/// </summary>
public interface ISefazClient
{
    /// <summary>
    /// Envia NFe para SEFAZ
    /// </summary>
    /// <param name="xmlNFeAssinado">XML da NFe assinado digitalmente</param>
    /// <returns>Resultado do envio</returns>
    Task<SefazEnvioResult> EnviarNFeAsync(string xmlNFeAssinado);
    
    /// <summary>
    /// Consulta status do lote na SEFAZ
    /// </summary>
    /// <param name="recibo">Número do recibo do lote</param>
    /// <returns>Resultado da consulta</returns>
    Task<SefazConsultaResult> ConsultarStatusLoteAsync(string recibo);
    
    /// <summary>
    /// Consulta protocolo de autorização da NFe
    /// </summary>
    /// <param name="chaveAcesso">Chave de acesso da NFe</param>
    /// <returns>Resultado da consulta</returns>
    Task<SefazConsultaResult> ConsultarProtocoloAsync(string chaveAcesso);
    
    /// <summary>
    /// Verifica se o serviço SEFAZ está disponível
    /// </summary>
    /// <returns>True se o serviço está disponível</returns>
    Task<bool> VerificarStatusServicoAsync();
}
