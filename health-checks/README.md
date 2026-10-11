# health-checks

Health checks em uma Minimal API do .NET 10: liveness x readiness, relatório em JSON e uma dependência caindo, tudo sem Docker.

## 1. O que é

Health check é um endpoint que responde "estou bem?". Quem pergunta é a infraestrutura (orquestrador, load balancer), não o usuário. Este exemplo tem um banco de dados de mentira (`FakeDatabaseHealthCheck`) que você derruba e levanta por uma rota, e três endpoints: `/health/live`, `/health/ready` e `/health` (relatório completo em JSON).

## 2. Quando usar e quando não usar

**Use** em qualquer API que roda em contêiner, em Kubernetes, Azure Container Apps ou atrás de um load balancer. É o jeito de a plataforma saber se deve reiniciar o app ou parar de mandar tráfego.

**Não use** como monitoramento de negócio ("quantos chamados abertos?") nem como substituto de métricas e logs. Health check responde sim/não, rápido.

## 3. Como rodar

```bash
cd health-checks
dotnet run
```

Abra `http://localhost:5102/swagger` ou use o arquivo `health-checks.http`. Os endpoints de health check são `GET /health`, `/health/live` e `/health/ready`; para simular o banco use `POST /simulate/database/{state}` com `online`, `slow` ou `down`.

## 4. O que observar

- Com tudo online, os três endpoints devolvem **200**.
- Chame `POST /simulate/database/down`: `/health/live` continua **200**, mas `/health/ready` vira **503**.
- Em `/health`, o JSON mostra qual checagem falhou e por quê.
- Chame `POST /simulate/database/slow`: `/health` mostra `Degraded`, mas o código HTTP continua **200**.
- Volte com `POST /simulate/database/online` e tudo normaliza.

## 5. FAQ

**Quem chama esses endpoints?**
Kubernetes (liveness e readiness probes), Azure Container Apps, load balancers e o `HEALTHCHECK` do Docker. Você configura a URL e o intervalo lá.

**Por que separar live de ready?**
São perguntas diferentes com reações diferentes. Se o app travou (live falha), o certo é reiniciar. Se o banco caiu (ready falha), reiniciar não resolve: o certo é só parar de mandar tráfego até ele voltar.

**Existem checagens prontas para dependências reais?**
Sim, os pacotes `AspNetCore.HealthChecks.*` (Postgres, Redis, RabbitMQ, SQL Server e outros). Aqui usamos um banco de mentira para não depender de Docker.

## 6. Pulos do gato

- **Não coloque dependência externa no liveness.** Se o banco cair e o liveness depender dele, o orquestrador reinicia o app em loop sem resolver nada.
- **Checagem lenta derruba o endpoint.** O orquestrador tem timeout; se a sua checagem demora, ele considera que falhou. Mantenha tudo rápido.
- **Não exponha detalhes internos publicamente.** O `/health` completo mostra nomes de dependências e mensagens de erro. Proteja com autenticação ou rede interna, ou devolva menos informação.
- **`Degraded` ainda devolve 200.** Para o orquestrador, "degradado" é "funcionando". Se quiser outro comportamento, mude `ResultStatusCodes`.
- **O writer padrão só devolve texto** (`Healthy`, `Unhealthy`). Para JSON com detalhes você precisa escrever o seu `ResponseWriter`, como neste exemplo.

## 7. No Plantão

Em breve: [github.com/Agonxx/plantao](https://github.com/Agonxx/plantao).
