using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PosFiap.Application.Interfaces;
using PosFiap.Application.UseCases.Auth;
using PosFiap.Application.UseCases.StudySessions;
using PosFiap.Domain.Interfaces;
using PosFiap.Infrastructure.Auth;
using PosFiap.Infrastructure.Files;
using PosFiap.Infrastructure.Groq;
using PosFiap.Infrastructure.Persistence;
using PosFiap.Infrastructure.Persistence.Repositories;

namespace PosFiap.Infrastructure;

/// <summary>
/// Ponto único de composição da camada de Infraestrutura + Application (Composition Root).
/// Mantém o Program.cs enxuto e a inversão de dependência explícita.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Persistência
        services.AddDbContext<PosFiapDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PosFiapDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStudySessionRepository, StudySessionRepository>();

        // Auth
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        // Arquivos
        services.AddScoped<IZipExtractor, ZipExtractor>();

        // IA - Groq (modelos abertos, tier gratuito)
        services.Configure<GroqSettings>(configuration.GetSection("Groq"));
        services.AddHttpClient<IStudyMaterialAiService, GroqService>((sp, client) =>
        {
            var settings = configuration.GetSection("Groq").Get<GroqSettings>() ?? new GroqSettings();
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        return services;
    }

    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddScoped<RegisterUseCase>();
        services.AddScoped<LoginUseCase>();
        services.AddScoped<UploadZipAndGenerateStudyMaterialUseCase>();
        services.AddScoped<ListStudySessionsUseCase>();
        services.AddScoped<GetStudySessionDetailUseCase>();
        return services;
    }
}
