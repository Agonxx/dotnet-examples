# dotnet-examples

Exemplos .NET bizarramente simples, um tema por pasta, com o código comentado em português.

## Índice

| Exemplo | O que ensina | Post |
|---|---|---|
| [`jwt-auth`](jwt-auth) | Emitir e validar um token JWT, e a diferença entre 401 e 403 | em breve |
| [`health-checks`](health-checks) | Liveness x readiness, relatório em JSON e uma dependência caindo | em breve |

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker: só nos exemplos que disserem. Nenhum dos exemplos acima precisa.

## Como rodar qualquer exemplo

1. Entre na pasta do exemplo: `cd <pasta>`
2. Rode: `dotnet run`
3. Abra `http://localhost:<porta>/swagger` (a porta está no README de cada exemplo), ou use o arquivo `.http` da pasta (Visual Studio, VS Code com a extensão REST Client, ou Rider).

## Convenções

- Código e nomes em inglês; comentários em português.
- Cada pasta é um projeto independente, sem solution e sem dependência entre exemplos.
- Cada exemplo tem um `README.md` no mesmo modelo de 7 seções:
  1. O que é
  2. Quando usar e quando não usar
  3. Como rodar
  4. O que observar
  5. FAQ
  6. Pulos do gato
  7. No Plantão

## Projeto relacionado

O **Plantão** é um helpdesk interno com IA onde esses conceitos aparecem aplicados de verdade: [github.com/Agonxx/plantao](https://github.com/Agonxx/plantao).
