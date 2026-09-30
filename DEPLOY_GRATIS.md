# Hospedagem 100% gratuita (sem cartão e sem domínio pago)

| Peça | Onde | URL que você ganha |
|---|---|---|
| Banco PostgreSQL | **Neon** (plano Free permanente) | (só a connection string) |
| Backend .NET | **Render** – Web Service (Docker, plano Free) | `https://posfiap-api.onrender.com` |
| Frontend React | **Render** – Static Site (Free) | `https://posfiap-web.onrender.com` |
| IA | **Groq** (chave gratuita) | – |

> O Postgres gratuito do próprio Render **expira em 30 dias**, por isso o banco fica no Neon.

---

## 0. Antes de tudo: troque sua chave da Groq

O histórico do Git deste repositório contém uma chave da Groq (começa com `gsk_wBkQLl…`) que
foi commitada e depois apagada do arquivo. Apagar no commit seguinte **não remove do histórico**.

1. Acesse https://console.groq.com/keys, **revogue** essa chave e crie uma nova.
2. Use só a nova nos passos abaixo (nunca no código; sempre em variável de ambiente).

---

## 1. Corrigindo o "Network Error" no ambiente local

"Network Error" do axios significa: **a requisição não obteve resposta** (API fora do ar / porta
errada) **ou o navegador bloqueou por CORS**. Descubra qual é em 10 segundos:

**Abra http://localhost:8080/health no navegador.**

### Não abriu → o backend não está no ar
```bash
docker compose ps
docker compose logs backend --tail 50
```
Causas mais comuns:

- **Senha do Postgres mudou depois do primeiro `up`.** O volume do banco guarda a senha antiga e o
  backend fica reiniciando sem conseguir conectar. Solução (apaga os dados locais):
  ```bash
  docker compose down -v
  docker compose up --build
  ```
- **`JWT_SECRET_KEY` com menos de 32 caracteres** (o `.env` de exemplo vinha com `sua_chave_aqui`).
  Agora o backend recusa iniciar com uma mensagem clara nos logs. Gere uma chave e ponha no `.env`:
  `openssl rand -base64 48`
- **Rodando com `dotnet run` em vez de Docker:** o `launchSettings.json` sobe a API na porta
  **5000**, mas o front chama a **8080**. Ou crie `frontend/.env` com
  `VITE_API_BASE_URL=http://localhost:5000/api`, ou rode
  `dotnet run --project src/PosFiap.API --urls http://localhost:8080`.
  (Sem Docker você também precisa de um Postgres local e da connection string configurada.)

### Abriu → é CORS
Aperte F12 → Console: aparece `blocked by CORS policy`. Acontece quando o front não está
exatamente em `http://localhost:5173` (ex.: o Vite trocou para `5174` porque a 5173 estava
ocupada, ou você abriu por `127.0.0.1`/IP da rede). Este pacote já libera `localhost` e
`127.0.0.1` em qualquer porta (`Cors:AllowLocalhost`). Reconstrua a imagem:
```bash
docker compose up --build
```

> Depois deste pacote, quando o problema for de conexão, a tela passa a mostrar **para qual URL o
> front tentou ligar e de qual origem**, em vez de só "Network Error".

---

## 2. Deploy gratuito

### 2.1 Banco no Neon
1. Crie conta em https://neon.com (sem cartão) e um projeto (região mais próxima possível).
2. Em **Connect**, **desmarque "Connection pooling"** (use a conexão direta, melhor para migrations)
   e copie a connection string `postgresql://usuario:senha@host/banco?sslmode=require`.
   O backend aceita essa URL como está.

### 2.2 Subir o código para o GitHub
```bash
git add .
git commit -m "Ajustes para deploy gratuito"
git push
```
Confira que o `.env` **não** foi versionado (o `.gitignore` já ignora).

### 2.3 Render (Blueprint)
1. Crie conta em https://render.com (login com GitHub).
2. **New → Blueprint** → escolha o repositório. O Render lê o `render.yaml` e cria os 2 serviços.
3. Ele pede os valores marcados como secretos:

   | Serviço | Variável | Valor |
   |---|---|---|
   | posfiap-api | `ConnectionStrings__DefaultConnection` | a URL do Neon |
   | posfiap-api | `Groq__ApiKey` | sua chave **nova** da Groq |
   | posfiap-api | `Cors__AllowedOrigins__0` | `https://posfiap-web.onrender.com` |
   | posfiap-web | `VITE_API_BASE_URL` | `https://posfiap-api.onrender.com/api` |

   Se os nomes `posfiap-api` / `posfiap-web` já estiverem em uso por outra pessoa, o Render coloca
   um sufixo na URL. Nesse caso, depois do primeiro deploy, veja as URLs reais no painel e
   corrija as duas variáveis de URL acima.
4. **Atenção:** `VITE_API_BASE_URL` é embutida no **build** do frontend. Se você mudar esse valor,
   é preciso um novo deploy do `posfiap-web` (Manual Deploy → Deploy latest commit).

### 2.4 Testar
1. Abra `https://posfiap-api.onrender.com/health` → deve responder `{"status":"healthy"}`
   (o primeiro acesso pode levar cerca de 1 minuto: o serviço estava dormindo).
2. Abra `https://posfiap-web.onrender.com`, crie uma conta e envie um `.zip`.

---

## 3. O que esperar do plano gratuito

- **Cold start:** o backend "dorme" após ~15 min sem acessos; o primeiro acesso demora cerca de 1 min.
  O Neon também suspende o banco após 5 min de inatividade (acorda em instantes).
- **Recursos:** 512 MB de RAM e 0,1 CPU no backend. ZIPs muito grandes podem ficar lentos.
- **Upload síncrono:** o envio do `.zip` só responde quando a IA termina (até 300 s). Se alguma
  camada intermediária cortar requisições longas, o ideal é processar em segundo plano.
- **Groq:** o plano gratuito tem limite de requisições; muitos usuários simultâneos vão esbarrar nele.
- Bom para portfólio/trabalho da pós/demonstração; não é infraestrutura de produção.

## 4. Problemas comuns no deploy

| Sintoma | Causa provável |
|---|---|
| Front mostra "Não foi possível falar com a API em …" | `VITE_API_BASE_URL` errada (falta `/api`? https?) ou CORS: confira `Cors__AllowedOrigins__0` (sem `/` no final) |
| Log: `Jwt:SecretKey ausente ou curta demais` | variável `Jwt__SecretKey` não definida (o Blueprint gera sozinho) |
| Log repete "Falha ao aplicar migrations" | connection string errada; use a URL direta do Neon |
| Render: "no open ports detected" | não defina `ASPNETCORE_URLS`/`PORT` à mão; o `entrypoint.sh` já usa a porta do Render |
| Tela em branco ao recarregar uma rota (`/login`) | falta a regra de rewrite `/*` → `/index.html` no Static Site |
