using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// HEALTH CHECKS
// Um health check é uma pergunta simples que o orquestrador (Kubernetes, Azure
// Container Apps, load balancer...) faz ao app: "você está bem?".
// Cada checagem ganha uma TAG para podermos separar duas perguntas diferentes:
//   - live  (liveness):  "o processo está vivo?"  -> se não, reinicie o app.
//   - ready (readiness): "dá para mandar tráfego?" -> se não, só pare de mandar.
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    // "self": só prova que o processo responde. Sempre saudável.
    .AddCheck("self", () => HealthCheckResult.Healthy("O processo está respondendo."), tags: ["live"])
    // "fake-database": simula uma dependência externa (o estado é controlado
    // pelas rotas /simulate). Fica só no "ready": se o banco cai, não adianta
    // reiniciar o app, apenas parar de enviar tráfego para ele.
    .AddCheck<FakeDatabaseHealthCheck>("fake-database", tags: ["ready"]);

// ---------------------------------------------------------------------------
// OPENAPI + SWAGGER UI
// Desde o .NET 9 o template não traz mais a Swagger UI. O OpenAPI nativo gera o
// JSON (/openapi/v1.json) e usamos o Swashbuckle apenas para a tela visual.
// ---------------------------------------------------------------------------
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

// GET /health/live: roda só as checagens com tag "live".
// Responde: "o processo está vivo?"
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// GET /health/ready: roda só as checagens com tag "ready".
// Responde: "dá para receber tráfego agora?"
//
// Códigos HTTP (padrão do .NET, configurável em ResultStatusCodes):
//   Healthy   -> 200
//   Degraded  -> 200 (funciona, mas com problema: o orquestrador ainda manda tráfego)
//   Unhealthy -> 503
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// GET /health: roda TODAS as checagens e devolve um relatório em JSON.
// O writer padrão só devolve o texto "Healthy"/"Unhealthy", então escrevemos o nosso.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds
            })
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }));
    }
});

// POST /simulate/database/{state}: muda o estado do banco de mentira.
// Valores aceitos: online, slow, down.
app.MapPost("/simulate/database/{state}", (string state) =>
{
    if (!Enum.TryParse<DatabaseState>(state, ignoreCase: true, out var newState))
        return Results.BadRequest(new { error = "Use online, slow ou down." });

    FakeDatabaseHealthCheck.State = newState;
    return Results.Ok(new { database = newState.ToString() });
});

app.Run();

// Os três estados possíveis do banco de mentira.
enum DatabaseState { Online, Slow, Down }

// Uma checagem é só uma classe que implementa IHealthCheck e devolve um resultado.
// Num caso real, aqui você faria um "SELECT 1" no banco, um PING no Redis etc.
class FakeDatabaseHealthCheck : IHealthCheck
{
    // Estado estático para ser simples: as rotas /simulate alteram este valor.
    public static DatabaseState State { get; set; } = DatabaseState.Online;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = State switch
        {
            DatabaseState.Online => HealthCheckResult.Healthy("Banco online."),
            DatabaseState.Slow => HealthCheckResult.Degraded("Banco lento."),
            _ => HealthCheckResult.Unhealthy("Banco fora do ar.")
        };

        return Task.FromResult(result);
    }
}
