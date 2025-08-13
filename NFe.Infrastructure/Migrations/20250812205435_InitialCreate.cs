using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NFe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    cliente_documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cliente_endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    data_venda = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    chave_acesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    numero_nfe = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    serie_nfe = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "itens_venda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    valor_unitario = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ncm = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cfop = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    unidade_medida = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_venda", x => x.id);
                    table.ForeignKey(
                        name: "FK_itens_venda_vendas_venda_id",
                        column: x => x.venda_id,
                        principalTable: "vendas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "protocolos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_acesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    numero_protocolo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_protocolo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mensagem_sefaz = table.Column<string>(type: "text", nullable: false),
                    xml_nfe = table.Column<string>(type: "text", nullable: false),
                    xml_protocolo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_protocolos", x => x.id);
                    table.ForeignKey(
                        name: "FK_protocolos_vendas_venda_id",
                        column: x => x.venda_id,
                        principalTable: "vendas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_itens_venda_venda_id",
                table: "itens_venda",
                column: "venda_id");

            migrationBuilder.CreateIndex(
                name: "IX_protocolos_venda_id",
                table: "protocolos",
                column: "venda_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_venda");

            migrationBuilder.DropTable(
                name: "protocolos");

            migrationBuilder.DropTable(
                name: "vendas");
        }
    }
}
