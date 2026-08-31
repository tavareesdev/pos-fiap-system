#!/bin/bash
set -e

echo "Aguardando banco de dados..."
until pg_isready -h postgres -U posfiap; do
  echo "Aguardando PostgreSQL..."
  sleep 2
done

echo "PostgreSQL pronto!"

# As migrations do EF Core já estão compiladas dentro da própria aplicação
# (Migrations é código C#, não arquivos que precisam existir separadamente em runtime).
# O Program.cs chama db.Database.Migrate() no startup, então não é necessário
# nem correto usar a CLI `dotnet ef` aqui dentro do container publicado.

echo "Iniciando aplicação (as migrações serão aplicadas automaticamente no startup)..."
exec dotnet PosFiap.API.dll
