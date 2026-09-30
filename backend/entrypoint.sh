#!/bin/bash
set -e

# Plataformas como o Render informam a porta pela variável PORT.
# Localmente (docker compose) ela não existe e usamos 8080.
export ASPNETCORE_URLS="http://+:${PORT:-8080}"

# Não esperamos mais o banco aqui: o docker-compose já usa depends_on com healthcheck,
# e o Program.cs tenta aplicar as migrations algumas vezes antes de desistir
# (o que também cobre bancos serverless como o Neon "acordando").
echo "Iniciando aplicação na porta ${PORT:-8080}..."
exec dotnet PosFiap.API.dll
