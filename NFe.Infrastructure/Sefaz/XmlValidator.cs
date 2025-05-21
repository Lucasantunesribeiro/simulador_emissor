using System.Xml.Schema;
using System.Xml.Linq;

namespace NFe.Infrastructure.Sefaz
{
    public class XmlValidator
    {
        public static void ValidateXml(string xml, string schemaPath)
        {
            var schemas = new XmlSchemaSet();
            schemas.Add("http://www.portalfiscal.inf.br/nfe", schemaPath);
            
            var doc = XDocument.Parse(xml);
            doc.Validate(schemas, (o, e) => 
            {
                throw new Exception($"Erro de validação XML: {e.Message}");
            });
        }
    }
}
