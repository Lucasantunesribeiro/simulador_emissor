using System.ComponentModel.DataAnnotations;

namespace NFe.Core.DTOs
{
    public class VendaCreateDto
    {
        [Required(ErrorMessage = "Nome do cliente é obrigatório")]
        public string ClienteNome { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Documento do cliente é obrigatório")]
        public string ClienteDocumento { get; set; } = string.Empty;
        
        public string ClienteEndereco { get; set; } = string.Empty;
        
        public string Observacoes { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Pelo menos um item é obrigatório")]
        public List<ItemVendaCreateDto> Itens { get; set; } = new List<ItemVendaCreateDto>();
    }
    
    public class ItemVendaCreateDto
    {
        public string Codigo { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Descrição do item é obrigatória")]
        public string Descricao { get; set; } = string.Empty;
        
        [Range(1, int.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero")]
        public int Quantidade { get; set; }
        
        [Range(0.01, double.MaxValue, ErrorMessage = "Valor unitário deve ser maior que zero")]
        public decimal ValorUnitario { get; set; }
        
        public string NCM { get; set; } = "00000000";
        public string CFOP { get; set; } = "5102";
        public string UnidadeMedida { get; set; } = "UN";
    }
}