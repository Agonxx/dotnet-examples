using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Lê a seção "Jwt" do appsettings. Em Development ela vem do appsettings.Development.json.
var jwt = builder.Configuration.GetSection("Jwt");
var issuer = jwt["Issuer"]!;
var audience = jwt["Audience"]!;
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
var expirationMinutes = int.Parse(jwt["ExpirationMinutes"]!);

// ---------------------------------------------------------------------------
// AUTENTICAÇÃO: "quem é você?"
// Aqui ensinamos a API a VALIDAR o token que chega no header
// "Authorization: Bearer <token>".
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // PULO DO GATO: por padrão o .NET renomeia as claims do token (ex.: "role" vira
        // um nome longo de URI). Desligando isso, as claims chegam com o mesmo
        // nome que estão no token, o que é bem mais fácil de entender.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,            // quem emitiu o token é quem eu espero?
            ValidIssuer = issuer,
            ValidateAudience = true,          // o token foi feito para esta API?
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,  // a assinatura confere com a minha chave?
            IssuerSigningKey = key,
            ValidateLifetime = true,          // o token ainda não expirou?

            // Diz ao .NET qual claim guarda o papel e qual guarda o nome.
            // Sem isso, RequireRole("admin") nunca encontraria o papel,
            // porque, como desligamos o mapeamento, a claim se chama só "role".
            RoleClaimType = "role",
            NameClaimType = "name"
        };
    });

// ---------------------------------------------------------------------------
// AUTORIZAÇÃO: "o que você pode fazer?"
// Autenticação e autorização são coisas diferentes. Aqui só registramos o serviço;
// as regras ficam em cada rota (RequireAuthorization).
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// OPENAPI + SWAGGER UI
// Desde o .NET 9 o template não traz mais a Swagger UI. O OpenAPI nativo gera o
// JSON (/openapi/v1.json) e usamos o Swashbuckle apenas para a tela visual.
// O "transformer" abaixo adiciona o botão Authorize (esquema Bearer) na tela.
// ---------------------------------------------------------------------------
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        // Declara que existe um esquema de segurança chamado "Bearer" (HTTP + JWT).
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Cole aqui o accessToken devolvido por POST /login."
        };

        // Diz que, por padrão, as rotas usam esse esquema.
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

// Ordem importa: primeiro descobrimos QUEM é (autenticação), depois decidimos
// o que PODE fazer (autorização). No .NET 8+ com minimal APIs o framework já
// adiciona esses dois middlewares sozinho quando você registra os serviços,
// mas deixar explícito ajuda a entender o fluxo.
app.UseAuthentication();
app.UseAuthorization();

// Usuários fixos em memória. Senha em texto puro é SÓ para o exemplo:
// em um sistema real você guarda o HASH da senha (ex.: PasswordHasher, BCrypt).
var users = new List<User>
{
    new("ana", "123", "agent"),
    new("bruno", "123", "admin")
};

// POST /login (anônima): confere usuário e senha e devolve o token.
app.MapPost("/login", (LoginRequest request) =>
{
    var user = users.FirstOrDefault(u => u.Username == request.Username && u.Password == request.Password);
    if (user is null)
        return Results.Unauthorized(); // 401: não sei quem você é

    var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

    // O "descriptor" descreve o token: quem emitiu, para quem, o que ele afirma
    // (claims), até quando vale e com qual chave é assinado.
    var descriptor = new SecurityTokenDescriptor
    {
        Issuer = issuer,
        Audience = audience,
        Expires = expiresAt,
        Subject = new ClaimsIdentity(
        [
            new Claim("sub", user.Username),   // identificador do usuário
            new Claim("name", user.Username),  // nome para exibição
            new Claim("role", user.Role)       // papel, usado na autorização
        ]),
        // HMAC-SHA256: a mesma chave assina e valida (chave simétrica).
        SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
    };

    // Gera a string final "xxxxx.yyyyy.zzzzz" (cabeçalho.payload.assinatura).
    var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);

    return Results.Ok(new { accessToken, expiresAt });
})
.AllowAnonymous();

// GET /public (anônima): qualquer um acessa.
app.MapGet("/public", () => Results.Ok(new { message = "Rota pública: não precisa de token." }))
   .AllowAnonymous();

// GET /me: exige estar autenticado (qualquer papel). Sem token -> 401.
app.MapGet("/me", (ClaimsPrincipal principal) => Results.Ok(new
{
    name = principal.Identity?.Name,
    role = principal.FindFirstValue("role"),
    claims = principal.Claims.Select(c => new { c.Type, c.Value })
}))
.RequireAuthorization();

// GET /admin: exige o papel "admin".
// Sem token -> 401 (não sei quem é você). Com token de outro papel -> 403
// (eu sei quem você é, mas você não tem permissão).
app.MapGet("/admin", () => Results.Ok(new { message = "Área administrativa: bem-vindo, admin." }))
   .RequireAuthorization(p => p.RequireRole("admin"));

app.Run();

// Tipos simples usados nas rotas acima.
record LoginRequest(string Username, string Password);
record User(string Username, string Password, string Role);
