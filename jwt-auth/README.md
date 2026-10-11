# jwt-auth

Autenticação com JWT em uma Minimal API do .NET 10: emitir o token, validar o token e entender a diferença entre 401 e 403.

## 1. O que é

JWT (JSON Web Token) é um texto assinado que o servidor entrega no login e que o cliente devolve a cada requisição, no header `Authorization: Bearer <token>`. Dentro dele vão as *claims* (quem é o usuário, qual o papel). Como o token é assinado, a API confia nele sem precisar consultar banco ou guardar sessão.

Este exemplo tem dois usuários fixos em memória (`ana` com papel `agent` e `bruno` com papel `admin`, ambos com senha `123`) e quatro rotas: `POST /login`, `GET /public`, `GET /me` e `GET /admin`.

## 2. Quando usar e quando não usar

**Use** em APIs consumidas por SPAs, apps mobile ou outros serviços, quando você quer uma API sem estado (stateless) e que escala sem sessão compartilhada.

**Não use** (ou pense duas vezes) se você tem só um site renderizado no servidor: cookie de sessão costuma ser mais simples e mais fácil de revogar. Também não é o caminho para login com Google, Microsoft etc.: aí o certo é OpenID Connect com um provedor de identidade.

## 3. Como rodar

```bash
cd jwt-auth
dotnet run
```

Abra `http://localhost:5101/swagger`. Faça `POST /login`, copie o `accessToken`, clique em **Authorize** e cole o token. Se preferir, use o arquivo `jwt-auth.http` (Visual Studio, VS Code com REST Client ou Rider).

## 4. O que observar

- `GET /me` sem token devolve **401**.
- Faça login com a `ana` e chame `/me`: **200**, com as claims do token.
- Com o token da `ana`, chame `/admin`: **403**. Com o do `bruno`: **200**.
- Cole o token em [jwt.io](https://jwt.io) e veja o payload. Ele é legível por qualquer um.
- Troque uma letra do token e tente de novo: **401** (assinatura inválida).

## 5. FAQ

**Qual a diferença entre JWT e sessão/cookie?**
Na sessão, o servidor guarda o estado e o cliente leva só um identificador. No JWT, o estado (as claims) viaja dentro do token e o servidor só valida a assinatura. O preço é que revogar um JWT antes de expirar é difícil.

**Onde guardar o token no front?**
Não há resposta perfeita. Em memória é o mais seguro contra XSS, mas some ao recarregar a página. Cookie `HttpOnly` protege contra XSS, mas exige cuidado com CSRF. Evite `localStorage` para tokens de longa duração.

**O que acontece quando o token expira?**
A API devolve 401 e o cliente precisa fazer login de novo (ou usar um refresh token).

**Por que não tem refresh token?**
Para manter o exemplo bizarramente simples. Refresh token envolve guardar e revogar tokens no servidor, o que merece um exemplo próprio.

**Existe uma forma rápida de gerar tokens para teste?**
Sim: a ferramenta `dotnet user-jwts` (`dotnet user-jwts create`) gera um token de desenvolvimento e já configura o projeto.

## 6. Pulos do gato

- **401 x 403:** 401 significa "não sei quem você é" (sem token, token inválido ou expirado). 403 significa "sei quem você é, mas você não pode isso" (papel insuficiente).
- **Mapeamento de claims:** por padrão o .NET renomeia claims do token (`MapInboundClaims = true`). Aqui desligamos e dizemos `RoleClaimType = "role"` e `NameClaimType = "name"`. Sem isso, `RequireRole` e `User.Identity.Name` não funcionam como esperado.
- **Tamanho da chave:** HMAC-SHA256 pede chave de pelo menos 32 caracteres (256 bits). Chave menor dá erro.
- **Ordem dos middlewares:** `UseAuthentication` vem antes de `UseAuthorization`. No .NET 8+ com minimal APIs o framework adiciona os dois sozinho, mas o exemplo deixa explícito para você enxergar o fluxo.
- **Diferença de relógio:** o `ClockSkew` padrão é de 5 minutos, ou seja, um token expirado continua valendo por até 5 minutos. Dá para zerar em `TokenValidationParameters`.
- **O payload não é criptografado, só assinado:** nunca coloque senha ou dado sensível dentro do token.
- **Nunca commite a chave:** a do exemplo é de mentira. Em projeto real, use user-secrets, variável de ambiente ou um cofre.

## 7. No Plantão

Em breve: [github.com/Agonxx/plantao](https://github.com/Agonxx/plantao).
