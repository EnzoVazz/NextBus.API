# NextBus

API do aviso de chegada do ônibus, entrega de Advanced Business Development with .NET da Global Solution 2026/2. O piloto é São Paulo. O aplicativo do grupo continua apontando para a API Java. Esta API repete o mesmo contrato.

## Integrantes

* **Enzo Vaz** — RM: 561702 — 2TDSPF
* **Lucas Ryuji Fukuda** — RM: 562152 — 2TDSPF
* **Pietro Donela Salomão** — RM: 561722 — 2TDSPF

## O que a API faz

A pessoa consulta linhas, paradas e a posição dos ônibus. Quem repete o mesmo embarque cadastra uma rotina. Na janela do horário, `GET /avisos` devolve quantos minutos faltam para o ônibus chegar no ponto.

A frase do aviso é padrão. Ela usa letreiro, ponto, minutos, idade do GPS e, se houver, o comunicado vigente carregado no projeto. Sem comunicado e sem veículo, o texto não inventa acidente, desvio nem lotação. A geração por IA fica na disciplina de Disruptive Architectures.

## Tecnologias

* **API:** ASP.NET Core Minimal API (`net10.0`), Swagger/OpenAPI, JWT e rate limit nativo
* **ORM:** Entity Framework Core com Oracle 19c (`UseOracleSQLCompatibility(DatabaseVersion19)`)
* **Trânsito:** API Olho Vivo, da SPTrans
* **Testes:** xUnit (`NextBus.Domain.Tests` e `NextBus.API.Tests`)

O erro de validação, conflito, acesso negado e limite usa o mesmo JSON, com `titulo`, `status` e `erros`. O health não devolve esse formato: ele devolve `status` e `resultados`.

## Estrutura

* **`NextBus.Domain`:** entidades, exceções e a regra do aviso (janela, escolha do ônibus, minutos e frase).
* **`NextBus.Application`:** DTOs, portas e casos de uso. A API não expõe as entidades.
* **`NextBus.Infrastructure`:** EF Core, Oracle, hash de senha e cliente da Olho Vivo.
* **`NextBus.Api`:** Minimal API, JWT, Swagger, health e o tratamento de erro.
* **`NextBus.Domain.Tests`:** a regra do aviso, sem banco e sem HTTP.
* **`NextBus.API.Tests`:** os endpoints, contra o Oracle da FIAP.

## Como executar

1. Abra a pasta `NextBus` no Rider e confirme a solução `NextBus.slnx`.
2. A senha do Oracle fica em `src/NextBus.Api/appsettings.Local.json`, fora do git. O arquivo só precisa da chave `Oracle:Password`. O host, a porta, o SID e o usuário já estão no `appsettings.json`.
3. O token da Olho Vivo fica na variável de ambiente `OLHOVIVO_TOKEN`, também fora do código.
4. Rode o perfil `http` do projeto `NextBus.Api`.

Na linha de comando, na pasta do projeto:

```powershell
dotnet test NextBus.slnx
dotnet run --project src/NextBus.Api --launch-profile http
```

| Endereço | Uso |
| --- | --- |
| http://localhost:5218/swagger | Swagger |
| http://localhost:5218/health | Banco e autenticação na Olho Vivo |

No Swagger, o `POST /auth/login` devolve o campo `token`. O botão **Authorize** recebe só esse valor, sem a palavra Bearer. As rotas protegidas passam a enviar o cabeçalho.

`GET /health` não aparece na lista do Swagger. Com a Olho Vivo autenticada, os dois itens ficam `Healthy`. Se `POST /Login/Autenticar` responder `false`, o item `olhovivo` fica degradado e a API continua no ar.

## Usuários

O cadastro público cria apenas cidadão. O gestor entra pelo usuário do script.

| E-mail | Senha | Papel |
| --- | --- | --- |
| marina.costa@email.com | Marina#2026 | CIDADAO |
| ricardo.nunes@email.com | Ricardo#2026 | GESTOR |

A senha não está na tabela `USUARIO`. A API grava o hash na tabela `SENHA`.

## Banco

A API usa o Oracle da FIAP (`oracle.fiap.com.br`, porta `1521`, SID `ORCL`) e as tabelas do script do grupo: `USUARIO`, `ROTINA` e `AVISO`. Os ids são numéricos. Os dias ficam como `1` (segunda) a `7` (domingo). `ativa` fica `S` ou `N`.

`SENHA` e `IDEMPOTENCIA` são criadas pela API no mesmo schema. Elas não fazem parte do script do grupo. `IDEMPOTENCIA` guarda a chave `Idempotency-Key` do `POST /rotinas`.

## Olho Vivo

O cliente autentica com o token, guarda o cookie e renova a sessão a cada 20 minutos. Linha, posição e previsão ficam em cache por 15 segundos.

Com a autenticação em `true`, essas três consultas usam a SPTrans. Se o login responder `false` ou a chamada falhar, a API usa o recorte local e o health marca `olhovivo` como degradado.

O recorte local não é o GTFS completo. Ele cobre a linha 7021-10, a parada Roberto Selmi Dei, o Jardim Maracá para a viagem direta e a linha 8000-10 com GPS de cinco minutos para o painel do gestor. `GET /paradas/proximas` e `POST /viagens/planejar` usam esse recorte para o mapa. O planejador devolve até três opções diretas, sem baldeação. Um endereço fora da lista volta erro de validação.

## Aviso

Vinte minutos antes do horário, a rotina entra na janela. Entre os ônibus que ainda vão chegar, fica o de previsão mais perto do horário. O acompanhamento gruda no prefixo daquele dia. Os minutos são a previsão menos o relógio da feed. A primeira frase sai com no máximo 15 minutos e a mesma notificação atualiza ao cruzar 10, 5 e 2. Em zero, ou se o veículo sai da previsão, o aviso encerra. Sem veículo na janela, sai uma vez a frase sem número.

## Contrato

| Rota | Acesso |
| --- | --- |
| `POST /auth/registrar`, `POST /auth/login` | Público. O cadastro cria só `CIDADAO`. |
| `GET /auth/eu` | Sessão. |
| `GET /linhas`, `GET /paradas/proximas`, `GET /linhas/{codigo}/posicoes`, `GET /previsao` | Sessão. Limite de 60 consultas por minuto. Acima disso, HTTP 429. |
| `POST /viagens/planejar`, CRUD de `/rotinas`, `GET /avisos` | Cidadão, só nas rotinas dele. |
| `POST /rotinas` | Exige o cabeçalho `Idempotency-Key`. A mesma chave e o mesmo corpo devolvem a rotina já criada. Outro corpo com a mesma chave responde 409. |
| `GET /operacao/linhas-sem-gps` | Gestor. Linhas com GPS acima de três minutos. |
| `GET /health` | Público. Banco e autenticação na Olho Vivo. |

O JWT leva o papel `CIDADAO` ou `GESTOR`.

## Testes

`dotnet test NextBus.slnx` cobre a janela da rotina, a escolha do ônibus das 7h18 para a rotina das 7h15, a notificação única atualizada em 15, 10, 5 e 2 minutos, o atraso sem trocar de veículo, a frase sem previsão, o comunicado vigente e os endpoints de acesso, rotina, aviso, planejamento, painel e limite.

Os testes de API usam o mesmo Oracle. O login entra com a Marina e o Ricardo. O teste do aviso cria uma rotina daqui a 15 minutos no ponto Roberto Selmi Dei, linha 7021-10, confere o veículo 74558 com 12 minutos e apaga o que criou. As rotinas do script permanecem.
