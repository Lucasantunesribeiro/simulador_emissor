using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using NFe.Core.Interfaces;
using NFe.Core.Services;
using NFe.Core.Extensions;
using NFe.Infrastructure.Repositories;
using NFe.Infrastructure.Data;
using NFe.Infrastructure.Services;
using HealthChecks.UI.Client;
using Amazon.SecretsManager;
using Amazon.SQS;
using NFe.API.Configuration;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Entity Framework
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<NFeDbContext>(options =>
    options.UseNpgsql(connectionString));

// AWS Services
builder.Services.AddAWSService<IAmazonSecretsManager>();

// AWS SQS
builder.Services.Configure<SqsSettings>(builder.Configuration.GetSection(SqsSettings.SectionName));
builder.Services.AddSingleton<IAmazonSQS>(provider =>
{
    var sqsSettings = builder.Configuration.GetSection(SqsSettings.SectionName).Get<SqsSettings>();
    return new AmazonSQSClient(Amazon.RegionEndpoint.GetBySystemName(sqsSettings?.Region ?? "us-east-1"));
});

// NFe Services with feature flag
builder.Services.AddNFeServices(builder.Configuration);

// Certificate Service (Dual-Mode: Production=AWS Secrets Manager, Development=Self-Signed)
builder.Services.AddScoped<ICertificateService, DualModeCertificateService>();
builder.Services.AddScoped<NFe.Core.Services.CertificateValidator>();

// Repositories
builder.Services.AddScoped<IVendaRepository, VendaRepositoryEF>();
builder.Services.AddScoped<IProtocoloRepository, ProtocoloRepositoryEF>();
builder.Services.AddScoped<IProcessamentoRepository, ProcessamentoRepositoryEF>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepositoryEF>();

// Auth Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// SQS Services
builder.Services.AddScoped<ISqsMessageService, SqsMessageService>();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey não configurado");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("Falha na autenticação JWT: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Debug("Token JWT validado para usuario: {UserId}", 
                    context.Principal?.FindFirst("user_id")?.Value);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("AdminOrOperator", policy => policy.RequireRole("Admin", "Operator"));
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
});

// Rate Limiting
var rateLimitSettings = builder.Configuration.GetSection("RateLimiting:AuthPolicy");
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("AuthPolicy", rateLimitOptions =>
    {
        rateLimitOptions.PermitLimit = int.Parse(rateLimitSettings["PermitLimit"] ?? "10");
        rateLimitOptions.Window = TimeSpan.FromMinutes(int.Parse(rateLimitSettings["WindowMinutes"] ?? "1"));
        rateLimitOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        rateLimitOptions.QueueLimit = 2;
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("NFePolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001", "https://localhost:3001") // Frontend URLs
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddCheck("sefaz", () => HealthCheckResult.Healthy("SEFAZ está disponível"))
    .AddNpgSql(connectionString ?? "", name: "database");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Disable HTTPS redirection in Development to avoid 3xx to https when running in containers
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Enable CORS
app.UseCors("NFePolicy");

// Enable rate limiting
app.UseRateLimiter();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Initialize database with seed data
try
{
    using (var scope = app.Services.CreateScope())
    {
        await NFe.Infrastructure.Data.SeedData.InitializeAsync(scope.ServiceProvider);
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Erro durante inicialização do banco de dados");
    throw;
}

app.Run();

// Expose Program class for WebApplicationFactory in tests
public partial class Program { }
