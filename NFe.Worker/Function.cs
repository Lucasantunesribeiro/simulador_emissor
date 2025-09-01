using Amazon.Lambda.Core;
using NFe.Core.Interfaces;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace NFe.Worker;

public class Function
{
    private readonly INFeService? _nfeService;

    public Function()
    {
        // Para Lambda, a injeção de dependência será configurada diferente
    }

    public Function(INFeService nfeService)
    {
        _nfeService = nfeService;
    }

    public async Task<string> FunctionHandler(object input, ILambdaContext context)
    {
        context.Logger.LogInformation("Processando vendas pendentes...");
        
        try
        {
            // Lógica do worker aqui
            // Por enquanto, apenas um placeholder
            await Task.Delay(1000);
            
            context.Logger.LogInformation("Processamento concluído com sucesso");
            return "Processamento concluído";
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Erro no processamento: {ex.Message}");
            throw;
        }
    }
}