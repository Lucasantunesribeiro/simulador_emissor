using Microsoft.EntityFrameworkCore;
using NFe.Core.Entities;

namespace NFe.Infrastructure.Data;

public class NFeDbContext : DbContext
{
    public NFeDbContext(DbContextOptions<NFeDbContext> options) : base(options)
    {
    }

    public DbSet<Venda> Vendas { get; set; } = null!;
    public DbSet<ItemVenda> ItensVenda { get; set; } = null!;
    public DbSet<Protocolo> Protocolos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuração da entidade Venda
        modelBuilder.Entity<Venda>(entity =>
        {
            entity.ToTable("vendas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteNome).HasColumnName("cliente_nome").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ClienteDocumento).HasColumnName("cliente_documento").HasMaxLength(20);
            entity.Property(e => e.ClienteEndereco).HasColumnName("cliente_endereco").HasMaxLength(500);
            entity.Property(e => e.ValorTotal).HasColumnName("valor_total").HasColumnType("decimal(10,2)");
            entity.Property(e => e.DataVenda).HasColumnName("data_venda");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.ChaveAcesso).HasColumnName("chave_acesso").HasMaxLength(44);
            entity.Property(e => e.NumeroNFe).HasColumnName("numero_nfe").HasMaxLength(50);
            entity.Property(e => e.SerieNFe).HasColumnName("serie_nfe").HasMaxLength(10);
            entity.Property(e => e.Observacoes).HasColumnName("observacoes").HasMaxLength(1000);
            
            // Relacionamento com itens
            entity.HasMany(e => e.Itens)
                  .WithOne(i => i.Venda)
                  .HasForeignKey(i => i.VendaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuração da entidade ItemVenda
        modelBuilder.Entity<ItemVenda>(entity =>
        {
            entity.ToTable("itens_venda");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VendaId).HasColumnName("venda_id");
            entity.Property(e => e.Codigo).HasColumnName("codigo").HasMaxLength(50);
            entity.Property(e => e.Descricao).HasColumnName("descricao").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Quantidade).HasColumnName("quantidade");
            entity.Property(e => e.ValorUnitario).HasColumnName("valor_unitario").HasColumnType("decimal(10,2)");
            entity.Property(e => e.NCM).HasColumnName("ncm").HasMaxLength(10);
            entity.Property(e => e.CFOP).HasColumnName("cfop").HasMaxLength(10);
            entity.Property(e => e.UnidadeMedida).HasColumnName("unidade_medida").HasMaxLength(10);
            
            // Propriedade calculada não mapeada
            entity.Ignore(e => e.ValorTotal);
        });

        // Configuração da entidade Protocolo
        modelBuilder.Entity<Protocolo>(entity =>
        {
            entity.ToTable("protocolos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VendaId).HasColumnName("venda_id");
            entity.Property(e => e.ChaveAcesso).HasColumnName("chave_acesso").HasMaxLength(44);
            entity.Property(e => e.NumeroProtocolo).HasColumnName("numero_protocolo").HasMaxLength(50);
            entity.Property(e => e.DataProtocolo).HasColumnName("data_protocolo");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.MensagemSefaz).HasColumnName("mensagem_sefaz");
            entity.Property(e => e.XmlNFe).HasColumnName("xml_nfe");
            entity.Property(e => e.XmlProtocolo).HasColumnName("xml_protocolo");
            
            // Relacionamento com venda
            entity.HasOne(e => e.Venda)
                  .WithMany()
                  .HasForeignKey(e => e.VendaId);
        });

        base.OnModelCreating(modelBuilder);
    }
}