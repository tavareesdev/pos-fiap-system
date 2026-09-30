# Pós FIAP · Resumo & Simulado com IA

Aplicação full-stack que recebe um `.zip` com os PDFs das aulas de uma disciplina de
pós-graduação e usa IA (**Groq**, modelos abertos) para gerar automaticamente:

- um **resumo consolidado** (Markdown) de todas as aulas;
- uma **prova de 20 questões de múltipla escolha** cobrindo o conteúdo, com gabarito e explicação.

**Demo:** https://pos-system-cv.vercel.app
> O backend roda em plano gratuito e "dorme" quando fica parado: o primeiro acesso pode levar cerca de 1 minuto.

## Stack

| Camada | Tecnologias |
|---|---|
| Backend | C# · .NET 8 · ASP.NET Core Web API · Entity Framework Core 8 · Swagger |
| Arquitetura | Clean Architecture + DDD (aggregate root, value objects, casos de uso) |
| Banco | PostgreSQL (Npgsql) |
| Autenticação | JWT Bearer · senhas com BCrypt (work factor 12) |
| IA | Groq API (`/chat/completions`, formato OpenAI) · modelo `openai/gpt-oss-120b` |
| PDFs | PdfPig (extração de texto local) |
| Frontend | React 18 · TypeScript · Vite · React Router · Axios · react-markdown |
| DevOps | Docker (multi-stage) · Docker Compose · Nginx · Kubernetes (Kustomize, HPA, Ingress) |
| Deploy gratuito | Render (API + site estático) · Neon (PostgreSQL) · Blueprint `render.yaml` |

## Como a IA é usada

O plano gratuito da Groq tem um limite baixo de tokens por minuto (inclusive por requisição),
então mandar todos os PDFs de uma vez estoura o limite. A solução é uma estratégia **map-reduce**:

1. **Map:** para cada PDF, o texto é extraído localmente (PdfPig, limitado a 10 mil caracteres por
   aula) e a IA gera um resumo condensado daquele PDF isolado.
2. **Reduce:** os resumos condensados são combinados em uma única chamada final que devolve, em
   JSON estruturado (`response_format=json_object`), o resumo consolidado e as 20 questões.

Detalhes de robustez: pausa entre chamadas para respeitar o limite por minuto, até 4 tentativas
com ajuste de `max_tokens`/temperatura quando a resposta vem truncada ou inválida, e um resumo de
contingência gerado a partir das notas individuais se a consolidação falhar. Na camada de domínio,
`Exam.EnsureIsValid()` garante que uma sessão só vira `Concluido` com exatamente 20 questões válidas.

## Arquitetura

**Backend**, com a regra de dependência apontando sempre para dentro:

```
backend/src/
├── PosFiap.Domain          # Entidades, Value Objects, regras de negócio puras
├── PosFiap.Application     # Casos de uso (orquestração), DTOs, interfaces (portas)
├── PosFiap.Infrastructure  # EF Core/Postgres, cliente Groq, JWT, hashing, zip
└── PosFiap.API             # Controllers, autenticação, composition root
```

- `Domain` não depende de nenhuma outra camada.
- `Application` depende só de `Domain` e define as *interfaces* que a Infra implementa (Dependency Inversion).
- `Infrastructure` implementa essas portas; `API` compõe tudo via injeção de dependência (`DependencyInjection.cs`).
- Aggregate root central: **`StudySession`** (um envio de `.zip`), que contém as `Lecture`s extraídas e,
  ao final, o `Summary` e o `Exam`.

**Frontend:**

```
frontend/src/
├── pages/       # Login, Register, Dashboard
├── components/  # DropZone, ExamViewer, SummaryViewer, SessionHistoryList
├── context/     # AuthContext (JWT em localStorage)
├── services/    # cliente axios da API
└── hooks/       # polling do status de processamento
```

## API

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/auth/register` | Cria conta e retorna o JWT |
| POST | `/api/auth/login` | Autentica e retorna o JWT (validade de 8 h) |
| POST | `/api/study-sessions/upload` | Envia o `.zip` (até 100 MB) e gera resumo + prova 🔒 |
| GET | `/api/study-sessions` | Histórico de envios do usuário 🔒 |
| GET | `/api/study-sessions/{id}` | Resumo e prova de uma sessão 🔒 |
| GET | `/health` | Health check |

🔒 = exige `Authorization: Bearer <token>`. Swagger em `/swagger`.

## Rodando localmente com Docker Compose

1. Copie as variáveis de ambiente:
   ```bash
   cp .env.example .env
   ```
2. Edite o `.env`: `GROQ_API_KEY` (grátis em https://console.groq.com/keys), `POSTGRES_PASSWORD` e
   `JWT_SECRET_KEY` (**mínimo de 32 caracteres**; gere uma com `openssl rand -base64 48`).
3. Suba tudo:
   ```bash
   docker compose up --build
   ```
4. Acesse:
   - Frontend: http://localhost:5173
   - API (Swagger): http://localhost:8080/swagger

As *migrations* do EF Core são aplicadas automaticamente ao iniciar o backend.

> Se você mudar `POSTGRES_PASSWORD` depois do primeiro `up`, rode `docker compose down -v` (apaga os
> dados locais), pois o volume do banco guarda a senha antiga.

### Sem Docker (desenvolvimento)

Precisa de um PostgreSQL local. Configure `ConnectionStrings:DefaultConnection`, `Groq:ApiKey` e
`Jwt:SecretKey` em `appsettings.Development.json` ou com `dotnet user-secrets`.

```bash
# Backend (o launchSettings usa a porta 5000)
cd backend
dotnet run --project src/PosFiap.API

# Frontend
cd frontend
npm install
cp .env.example .env   # ajuste VITE_API_BASE_URL (ex.: http://localhost:5000/api)
npm run dev
```

## Deploy gratuito (sem cartão e sem domínio pago)

Backend e frontend no **Render**, banco no **Neon**, tudo com subdomínios gratuitos. O arquivo
[`render.yaml`](render.yaml) descreve os dois serviços e o passo a passo completo está em
[`DEPLOY_GRATIS.md`](DEPLOY_GRATIS.md).

Variáveis principais do backend: `ConnectionStrings__DefaultConnection` (aceita a URL
`postgresql://...` do Neon), `Jwt__SecretKey`, `Groq__ApiKey` e `Cors__AllowedOrigins__0`
(URL do frontend). No frontend: `VITE_API_BASE_URL` (URL da API + `/api`, embutida no build).

## Deploy em Kubernetes

Manifests em `k8s/` (também via Kustomize): `Namespace`, `Secret`, `ConfigMap`, `StatefulSet` do
Postgres com `PersistentVolumeClaim`, `Deployment`s do backend (com `HorizontalPodAutoscaler`, 2 a 6
réplicas) e do frontend, `Service`s e `Ingress` com TLS opcional (cert-manager).

```bash
docker build -t <seu-registry>/posfiap-backend:latest ./backend
docker build -t <seu-registry>/posfiap-frontend:latest ./frontend \
  --build-arg VITE_API_BASE_URL=https://posfiap.exemplo.com/api
docker push <seu-registry>/posfiap-backend:latest
docker push <seu-registry>/posfiap-frontend:latest

# Atualize as imagens em k8s/04-backend.yaml e k8s/05-frontend.yaml,
# edite k8s/01-secrets.yaml e o domínio em k8s/06-ingress.yaml, e aplique:
kubectl apply -k k8s/
```

⚠️ `k8s/01-secrets.yaml` traz valores de exemplo em texto puro só para fins didáticos. Em produção use
Sealed Secrets, External Secrets Operator ou um cofre gerenciado, e nunca versione segredos reais.

## Fluxo de uso

1. O usuário cria conta ou faz login (JWT).
2. Arrasta o `.zip` com os PDFs das aulas para a *drop zone*.
3. O backend extrai os PDFs e o texto de cada um, e a IA gera o resumo e a prova (map-reduce, acima).
4. A interface exibe o resumo e a prova interativa; o status (`Recebido` → `ExtraindoArquivos` →
   `ProcessandoComIA` → `Concluido`/`Falhou`) pode ser acompanhado por *polling*.
5. O histórico de envios fica disponível na barra lateral.

## Decisões técnicas

- **DDD:** `StudySession` como aggregate root mantém `Lecture`, `Summary` e `Exam` consistentes.
- **Clean Architecture:** dá para trocar o provedor de IA, o banco ou a autenticação sem tocar no domínio.
- **Map-reduce na IA:** contorna o limite de tokens do tier gratuito e mantém cada chamada pequena.
- **Segurança:** senhas com BCrypt, JWT com validação de tamanho mínimo da chave no startup,
  CORS restrito às origens configuradas em produção, segredos fora do código (variáveis de ambiente).

## Limitações conhecidas e próximos passos

- O envio do `.zip` é **síncrono**: a requisição só responde quando a IA termina. O próximo passo natural
  é processar em segundo plano (fila/worker) e responder na hora.
- PDFs escaneados (só imagem) não têm texto extraível; não há OCR.
- Cada aula é limitada a 10 mil caracteres no resumo individual.
- O tier gratuito da Groq limita requisições e tokens por minuto.
- Ainda não há testes automatizados.