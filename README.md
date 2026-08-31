# Pós FIAP · Resumo & Prova com IA

Sistema que recebe um `.zip` com os PDFs das aulas de uma disciplina de pós-graduação,
envia o conteúdo para uma IA (via **Groq**, modelos abertos) e gera automaticamente:

- Um **resumo consolidado** (Markdown) de todas as aulas.
- Uma **prova de 20 questões de múltipla escolha** cobrindo todo o conteúdo.

## Arquitetura

**Backend** — C# / .NET 8, seguindo **Clean Architecture + DDD**:

```
backend/src/
├── PosFiap.Domain          # Entidades, Value Objects, regras de negócio puras
├── PosFiap.Application     # Casos de uso (orquestração), DTOs, interfaces (portas)
├── PosFiap.Infrastructure  # EF Core/Postgres, cliente Groq, JWT, hashing, zip
└── PosFiap.API             # Controllers, autenticação, composition root
```

- `Domain` não depende de nenhuma outra camada.
- `Application` depende só de `Domain` (define as *interfaces* que a Infra implementa —
  Dependency Inversion).
- `Infrastructure` implementa as portas definidas em `Application`.
- `API` compõe tudo via injeção de dependência (`DependencyInjection.cs`).

Aggregate root central: **`StudySession`** — representa um envio de `.zip`, contém as
`Lecture`s extraídas e, ao final do processamento, o `Summary` e o `Exam` (regra de
negócio: a prova deve ter exatamente 20 questões válidas).

**Frontend** — React + TypeScript (Vite):

```
frontend/src/
├── pages/       # Login, Register, Dashboard
├── components/  # DropZone, ExamViewer, SummaryViewer, SessionHistoryList
├── context/     # AuthContext (JWT em localStorage)
├── services/    # cliente axios da API
└── hooks/       # polling do status de processamento
```

## Rodando localmente com Docker Compose

1. Copie o arquivo de variáveis de ambiente:
   ```bash
   cp .env.example .env
   ```
2. Edite `.env` e preencha `GROQ_API_KEY` (obtenha grátis em https://console.groq.com/keys),
   além de `POSTGRES_PASSWORD` e `JWT_SECRET_KEY`.
3. Suba tudo:
   ```bash
   docker compose up --build
   ```
4. Acesse:
   - Frontend: http://localhost:5173
   - API (Swagger): http://localhost:8080/swagger

O backend aplica as *migrations* do EF Core automaticamente ao iniciar.

## Rodando sem Docker (desenvolvimento)

**Backend**
```bash
cd backend
dotnet restore
dotnet ef database update -p src/PosFiap.Infrastructure -s src/PosFiap.API   # cria o schema
dotnet run --project src/PosFiap.API
```
Configure `Groq:ApiKey` e a connection string em
`backend/src/PosFiap.API/appsettings.Development.json` ou via `dotnet user-secrets`.

> Nota: o projeto não inclui a migration inicial gerada (`dotnet ef migrations add InitialCreate`),
> pois isso depende do ambiente onde for gerada. Gere-a localmente antes do primeiro `dotnet ef database update`.

**Frontend**
```bash
cd frontend
npm install
cp .env.example .env   # ajuste VITE_API_BASE_URL se necessário
npm run dev
```

## Deploy em Kubernetes

Manifests em `k8s/` (também disponíveis via Kustomize):

```bash
# 1. Publique as imagens em um registry acessível pelo cluster
docker build -t <seu-registry>/posfiap-backend:latest ./backend
docker build -t <seu-registry>/posfiap-frontend:latest ./frontend \
  --build-arg VITE_API_BASE_URL=https://posfiap.exemplo.com/api
docker push <seu-registry>/posfiap-backend:latest
docker push <seu-registry>/posfiap-frontend:latest

# 2. Atualize as imagens referenciadas em k8s/04-backend.yaml e k8s/05-frontend.yaml

# 3. Edite k8s/01-secrets.yaml com valores reais (ou substitua por um cofre de secrets)
#    e k8s/06-ingress.yaml com seu domínio real

# 4. Aplique
kubectl apply -k k8s/
```

Recursos criados: `Namespace`, `Secret`, `ConfigMap`, `StatefulSet` do Postgres com
`PersistentVolumeClaim`, `Deployment`s do backend (com `HorizontalPodAutoscaler`) e
frontend, `Service`s e um `Ingress` com TLS (via cert-manager, opcional).

⚠️ **Importante**: `k8s/01-secrets.yaml` está com valores de exemplo em texto puro apenas
para fins didáticos. Em produção, use Sealed Secrets, External Secrets Operator ou um
cofre gerenciado (Vault, AWS/GCP Secrets Manager) e nunca versione segredos reais.

## Fluxo de uso

1. Usuário cria conta / faz login (`/api/auth/register`, `/api/auth/login` → JWT).
2. Na tela principal, arrasta o `.zip` com os PDFs das aulas para a *drop zone*.
3. O backend extrai os PDFs, extrai o texto de cada um localmente (PdfPig) e envia
   para a Groq (modelo aberto, via prompt) para gerar a saída em JSON estruturado
   (resumo + 20 questões).
4. O frontend faz *polling* do status (`Recebido` → `ExtraindoArquivos` →
   `ProcessandoComIA` → `Concluido`/`Falhou`) e exibe o resumo e a prova interativa
   assim que prontos.
5. Histórico de envios anteriores fica disponível na barra lateral.

## Principais decisões técnicas

- **DDD**: `StudySession` como aggregate root garante que `Lecture`, `Summary` e `Exam`
  só sejam modificados de forma consistente (ex.: impossível marcar como `Concluido`
  sem uma prova com exatamente 20 questões válidas — validado em `Exam.EnsureIsValid()`).
- **Clean Architecture**: a regra de dependência aponta sempre para dentro
  (`API → Infrastructure → Application → Domain`), permitindo trocar o provedor de IA,
  o banco de dados ou o mecanismo de autenticação sem tocar no domínio.
- **Groq**: chamado via REST (`/chat/completions`, compatível com o formato OpenAI),
  usando um modelo aberto (Llama). O texto de cada PDF é extraído localmente com
  PdfPig (Groq não recebe PDF nativamente) e enviado como parte do prompt, com
  `response_format=json_object` e instruções explícitas de schema para obter JSON
  confiável.
- **Segurança**: senha com BCrypt (work factor 12), autenticação JWT Bearer, containers
  rodando como usuário não-root, `Secret`s separados de `ConfigMap`s no Kubernetes.
