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
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    postgresql-client \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Não é necessário copiar a pasta Migrations nem instalar a CLI dotnet-ef aqui:
# as migrations do EF Core são compiladas dentro da própria PosFiap.Infrastructure.dll,
# e o Program.cs aplica elas automaticamente via db.Database.Migrate() no startup.

COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]