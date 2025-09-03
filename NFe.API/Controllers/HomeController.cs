using Microsoft.AspNetCore.Mvc;

namespace NFe.API.Controllers
{
    [ApiController]
    [Route("")]
    public class HomeController : ControllerBase
    {
        /// <summary>
        /// Página inicial da API NFe
        /// </summary>
        [HttpGet]
        public ActionResult GetHome()
        {
            var info = new
            {
                name = "NFe API",
                version = "1.0.0",
                description = "Sistema de emissão de NFe simulada",
                status = "Online",
                endpoints = new
                {
                    health = "/health",
                    swagger = "/swagger",
                    vendas = "/api/v1/vendas",
                    protocolos = "/api/v1/protocolos"
                },
                documentation = "/swagger/index.html"
            };

            return Ok(info);
        }

        /// <summary>
        /// Informações da API
        /// </summary>
        [HttpGet("info")]
        public ActionResult GetInfo()
        {
            return Ok(new
            {
                api = "NFe Emitter API",
                version = "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                timestamp = DateTime.UtcNow,
                server = Environment.MachineName
            });
        }
    }
}