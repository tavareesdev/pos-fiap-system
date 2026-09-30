using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PosFiap.API.Middlewares;
using PosFiap.Infrastructure;
using PosFiap.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Falha cedo e com mensagem clara se a chave JWT estiver ausente/curta
// (HS256 exige pelo menos 128 bits; sem isso o cadastro/login dá erro 500 só na hora de gerar o token).
var jwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "Jwt:SecretKey ausente ou curta demais: use pelo menos 32 caracteres. " +
        "Defina JWT_SECRET_KEY no .env (docker compose) ou a variável de ambiente Jwt__SecretKey.");
}

// ---------- Serviços ----------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Pós FIAP - Resumo & Prova IA", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    options.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Camadas de Infraestrutura + Application (Composition Root)
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationUseCases();

// Autenticação JWT
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub
    };
});

builder.Services.AddAuthorization();

// CORS - permite o frontend React consumir a API.
// - Cors:AllowedOrigins: origens explícitas (ex.: a URL do frontend em produção).
// - Cors:AllowLocalhost: aceita http(s)://localhost e 127.0.0.1 em QUALQUER porta
//   (útil quando o Vite muda de 5173 para 5174, ou ao abrir por 127.0.0.1). Desligue em produção.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
var allowLocalhost = builder.Configuration.GetValue("Cors:AllowLocalhost", true);

bool IsOriginAllowed(string origin)
{
    if (allowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase)))
        return true;

    if (allowLocalhost && Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        return uri.Host == "localhost" || uri.Host == "127.0.0.1";

    return false;
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.SetIsOriginAllowed(IsOriginAllowed)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// ---------- Migração automática do banco ----------
// Tenta algumas vezes: bancos serverless (Neon) podem levar alguns segundos para "acordar"
// e, no docker compose, o Postgres pode ainda estar terminando de subir.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PosFiapDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    const int maxAttempts = 6;

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            startupLogger.LogWarning(ex,
                "Falha ao aplicar migrations (tentativa {Attempt}/{Max}). Nova tentativa em 5s...",
                attempt, maxAttempts);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}

// ---------- Pipeline HTTP ----------

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Necessário para testes de integração (WebApplicationFactory)
public partial class Program { }
