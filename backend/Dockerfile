# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/PosFiap.Domain/PosFiap.Domain.csproj src/PosFiap.Domain/
COPY src/PosFiap.Application/PosFiap.Application.csproj src/PosFiap.Application/
COPY src/PosFiap.Infrastructure/PosFiap.Infrastructure.csproj src/PosFiap.Infrastructure/
COPY src/PosFiap.API/PosFiap.API.csproj src/PosFiap.API/
RUN dotnet restore src/PosFiap.API/PosFiap.API.csproj

COPY src/ src/
RUN dotnet publish src/PosFiap.API/PosFiap.API.csproj -c Release -o /app/publish --no-restore

# ---------- Runtime stage ----------
# Imagem "aspnet" (só runtime) é bem menor que a SDK: deploy mais rápido e menos memória,
# o que importa nos planos gratuitos (512 MB no Render).
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# As migrations do EF Core são compiladas dentro da PosFiap.Infrastructure.dll e o
# Program.cs aplica elas automaticamente no startup (db.Database.Migrate()).

COPY entrypoint.sh /app/entrypoint.sh
# sed remove CRLF caso o arquivo tenha sido salvo/clonado no Windows (senão o container não inicia)
RUN sed -i 's/\r$//' /app/entrypoint.sh && chmod +x /app/entrypoint.sh

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:${PORT:-8080}/health || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]
