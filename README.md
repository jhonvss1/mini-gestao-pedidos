# Mini Gestão de Pedidos

Cadastro de clientes e produtos com **criação de pedidos validada** (cliente, produtos e estoque), **listagem paginada com filtros** e **ranking dos cinco clientes** com maior valor no período. Teste técnico: backend .NET, banco SQL Server, frontend Angular, tudo executável com `docker compose up`.

- O **quê** (regras RN-xx, critérios de aceite CA-xx, decisões D-xx): [docs/spec.md](docs/spec.md)
- O **como** (arquitetura, concorrência, etapas E0 a E22): [docs/plan.md](docs/plan.md)

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10, C#, ASP.NET Core (controllers), Entity Framework Core 10 |
| Banco | SQL Server 2022 (imagem `2022-CU28-ubuntu-22.04`), schema e carga por scripts SQL |
| Frontend | Angular 22 (standalone, signals, sem zone.js), TypeScript |
| Testes | xUnit, NSubstitute, Testcontainers (SQL Server real), `WebApplicationFactory` |
| Execução | Docker Compose: banco, API e frontend (nginx) |

## Pré-requisitos

- **Docker Desktop** em execução (Compose e testes de integração).
- **.NET SDK 10** (para rodar a API e os testes fora do Docker).
- **Node 24.15 ou superior** (só para desenvolver o frontend fora do Docker).

## Como executar

### Tudo pelo Docker Compose

```bash
docker compose up --build
```

| Serviço | Endereço |
|---|---|
| Frontend | http://localhost:4200 |
| API (rotas sob `/api`) | http://localhost:5080/api/pedidos |
| SQL Server | `localhost,1433` (usuário `sa`) |

A senha do `sa` tem um **valor padrão de desenvolvimento** em [docker-compose.yml](docker-compose.yml) e em [.env.example](.env.example); copie o exemplo para `.env` só se quiser mudar senha ou portas. O serviço `db-init` cria o banco `gestao_pedidos`, o schema e a carga inicial; ele é idempotente e roda a cada `up`.

Para parar: `docker compose down`. Para zerar o banco também: `docker compose down -v`.

### Desenvolvimento local (API e frontend fora do Docker)

Banco no Docker, aplicação no seu ambiente:

```bash
docker compose up -d --wait db
```

```bash
docker compose run --rm db-init
```

```bash
dotnet run --project src/Pedidos.Api
```

A API sobe em http://localhost:5080 (perfil do `launchSettings.json`, ambiente `Development`, conexão local em `appsettings.Development.json`). Em outro terminal:

```bash
npm install --prefix frontend
```

```bash
npm start --prefix frontend
```

O frontend sobe em http://localhost:4200 e o `proxy.conf.json` leva `/api` para a API em `localhost:5080`.

## Endpoints

Todos sob o prefixo `/api` (decisão D-20). Erros seguem o formato Problem Details com o campo `codigo`.

| Método e rota | Descrição |
|---|---|
| `POST /api/pedidos` | Cria o pedido. Preço vem do cadastro; estoque é baixado na mesma transação |
| `GET /api/pedidos` | Lista com filtros `clienteId`, `dataInicio`, `dataFim` (`yyyy-MM-dd`, inclusivos, UTC) e `pagina`, `tamanhoPagina` (padrão 20, máximo 100) |
| `GET /api/relatorios/top-clientes` | Até 5 clientes por valor total no período (`dataInicio` e `dataFim` obrigatórios) |
| `GET /api/clientes`, `GET /api/produtos` | Leituras auxiliares para os seletores do frontend |

Criar um pedido:

```bash
curl -i -X POST http://localhost:5080/api/pedidos -H "Content-Type: application/json" -d '{"clienteId":1,"itens":[{"produtoId":1,"quantidade":2}]}'
```

Ranking de outubro da carga inicial (esperado: clientes 1, 3, 5, 2 e 4):

```bash
curl "http://localhost:5080/api/relatorios/top-clientes?dataInicio=2026-10-01&dataFim=2026-10-31"
```

| Status | Quando | `codigo` |
|---|---|---|
| 201 | Pedido criado | n/a |
| 400 | Corpo ausente ou malformado, tipo errado, parâmetro de consulta inválido | `REQUISICAO_INVALIDA` |
| 422 | Campo ausente ou inválido (itens vazios, quantidade ≤ 0, limites numéricos) | `VALIDACAO` |
| 404 | Cliente ou produto inexistente | `CLIENTE_NAO_ENCONTRADO`, `PRODUTO_NAO_ENCONTRADO` |
| 409 | Estoque insuficiente (inclui perder a disputa concorrente) | `ESTOQUE_INSUFICIENTE` |
| 500 | Erro inesperado, sem detalhes internos | n/a |

A validação do `POST` é **em etapas** (estrutura, cliente, produtos, estoque) e para na primeira etapa com falhas, reportando todos os erros dela (RN-11).

## Testes

Os testes de integração sobem um SQL Server real com Testcontainers, então o **Docker precisa estar rodando**.

```bash
dotnet test Pedidos.slnx
```

Cada teste de critério de aceite leva `[Trait("CA", "CA-xx")]`, o que permite rodar um critério isolado:

```bash
dotnet test Pedidos.slnx --filter "CA=CA-14"
```

| O que | Como |
|---|---|
| Direção das dependências entre projetos | `powershell -File scripts/verificar-referencias.ps1` |
| Execução completa pelo Compose (CA-33) e idempotência do `db-init` (CA-34) | `powershell -File scripts/verificar-compose.ps1` (**apaga o volume do banco**) |
| Frontend (CA-38 a CA-40) | Roteiro manual abaixo (a spec não pede testes automatizados do frontend) |

### Roteiro manual do frontend

Com o ambiente no ar, em http://localhost:4200:

1. **Listagem (CA-38):** os pedidos aparecem do mais recente para o mais antigo, com o total no rodapé. Filtre por cliente, por data inicial, por data final e por combinações; o total muda a cada filtro. Os dois "João Souza" aparecem como "João Souza (#3)" e "João Souza (#5)". Com data inicial depois da final, o erro aparece e nenhuma requisição sai. Mude "Por página" e navegue entre as páginas.
2. **Criação com erros (CA-39):** em "Novo pedido", o botão fica desabilitado sem cliente, sem produto ou com quantidade menor que 1. Escolha o produto "Webcam HD" (sem estoque) e envie: aparece o aviso de estoque com solicitado e disponível, e **o formulário continua preenchido**.
3. **Criação com sucesso (CA-40):** com filtros ativos na listagem, crie um pedido válido. A listagem volta com filtros limpos, na página 1, com o pedido novo no topo e o aviso de sucesso. Reabra o formulário: o estoque do produto está menor.

## Arquitetura

Quatro projetos, com a direção das dependências garantida pelas referências de projeto:

```
Domain → (nenhuma) ← Application ← Infrastructure
                         ↑               ↑
                         └──── Api ──────┘   (Api referencia Infrastructure só para a injeção de dependência)
```

| Projeto | Responsabilidade | Pastas |
|---|---|---|
| `Pedidos.Domain` | Entidades e regras (preço da venda, total, consolidação de itens, limites) | `Entidades/`, `ObjetosDeValor/`, `Resultados/`, `Constantes/` |
| `Pedidos.Application` | Casos de uso, validação da etapa 1, `Resultado` único, duas portas | `CasosDeUso/`, `Interfaces/`, `Comandos/`, `Consultas/`, `Modelos/`, `Validacao/`, `Comum/` |
| `Pedidos.Infrastructure` | EF Core, consultas, transação e débito atômico de estoque | `Persistencia/{Contexto,Configuracoes,Repositorios,Consultas}/` |
| `Pedidos.Api` | Controllers, contratos HTTP, Problem Details, configuração | `Controllers/`, `Contratos/{Requisicoes,Respostas}/`, `Erros/` |

Fora de `src/`: `database/` (scripts SQL do schema e da carga inicial), `frontend/` (Angular), `tests/` (espelham as pastas do código), `scripts/` e `docs/`.

Não há MediatR, barramento de eventos, repositório genérico, Unit of Work próprio, CQRS formal nem AutoMapper: só duas interfaces em `Application/Interfaces/` (`IPedidoRepositorio` e `IPedidoConsultas`), que existem para a Application não conhecer o EF Core e para testar a orquestração sem banco.

## Decisões e trade-offs

| Tema | Decisão | Por quê |
|---|---|---|
| **Estoque sob concorrência** | `UPDATE produto SET estoque = estoque - @q WHERE id = @id AND estoque >= @q`, em ordem crescente de `id`; zero linhas afetadas significa que perdeu a disputa | Atômico, sem coluna `rowversion` e sem retry de conflito. A ordem crescente de débito evita deadlock; um teste com o retry desligado prova isso. O `CHECK (estoque >= 0)` é a última barreira |
| **Quem decide a regra** | O domínio decide sobre um snapshot dos produtos (preço, total, estoque, mensagens) e devolve as baixas a aplicar; a Infrastructure só executa a transação e o `UPDATE` condicional | A regra fica no Domain e o repositório não tem lógica de negócio. A comparação de estoque aparece também no SQL, por escolha: no domínio é a decisão, no SQL é a garantia atômica |
| **Transação** | Uma porta, `CriarAsync(..., decidir)`, que recebe a decisão de domínio como função | Evita expor Unit of Work e mantém a decisão pura e reexecutável (a decisão pode ser repetida em disputa ou deadlock) |
| **Perda de disputa** | Desfaz todos os débitos do pedido, relê o estoque e decide de novo, no máximo 3 tentativas | A mensagem do 409 traz o estoque real de todos os produtos afetados. Contenção nunca vira 5xx |
| **Preço** | Vem do cadastro e é **copiado** para o item; o `precoUnitario` do corpo é ignorado | Preço histórico do pedido imutável (RN-04) |
| **Dinheiro** | `decimal` em C#, `decimal(18,2)` no banco e números JSON sem ponto flutuante; no frontend, a estimativa soma centavos inteiros | Evita erros como `0.30000000000000004` (RN-23) |
| **Datas** | `DataCriacao` em UTC, truncada para segundos (`datetime2(0)`); filtros por dia são inclusivos em `[início, fim + 1 dia)` | Sem isso, 23:59:59,6 viraria o dia seguinte |
| **Códigos HTTP** | 400 malformado, 422 semântico, 404 inexistente, 409 estoque | Cada código tem um significado único; o `codigo` do Problem Details evita depender de texto |
| **Paginação** | Ordem fixa `DataCriacao desc, Id desc`; a listagem carrega cabeçalhos e depois itens em duas consultas | Paginar nunca repete nem omite registros, mesmo com datas iguais |
| **Ranking** | Agrupa por `cliente_id` (nunca por nome), desempata por id crescente | Homônimos aparecem como entradas distintas e o resultado é determinístico |
| **Erros no código** | `Resultado` único com lista de erros, traduzido na Api para o Problem Details da spec | Acumula os erros de uma etapa sem biblioteca e mantém a Api sem regra de negócio |
| **Schema** | Scripts em `database/` são a fonte; sem migrations e sem `EnsureCreated`; teste de deriva compara o modelo do EF com o banco | O schema real é o que os scripts criam |
| **Testes** | Integração sempre contra SQL Server real (Testcontainers), um banco por classe | In-memory e SQLite escondem diferenças de concorrência, constraints e tipos |
| **Frontend** | Erros por tela (`ErrosService` sem `providedIn: 'root'`), formulário nunca limpo em caso de erro | Nenhum erro antigo vaza entre rotas e o usuário não perde o preenchimento |

### Correspondência `DataPedido` e `DataCriacao`

O enunciado do teste usa `DataPedido`. O modelo adota `DataCriacao` (coluna `data_criacao`), decisão D-17 da spec. As duas são o mesmo dado: a data em que o pedido foi criado.

## Recursos de C# e Angular usados

Os recursos abaixo não mudam o que o código faz; só reduzem texto ou protegem contra erro. Valem como referência de estudo.

| Recurso | Chegou em | Onde aparece |
|---|---|---|
| `record` | C# 9 / .NET 5 | Comandos, modelos de leitura, erros e resultados (dado imutável, comparado por valor) |
| `namespace X;` (em arquivo) | C# 10 | Todos os arquivos |
| Construtor primário (`class X(IRepo repo)`) | C# 12 / .NET 8 | Casos de uso, controllers, repositório |
| Coleções `[a, b]` e `[.. outra]` | C# 12 | Retornos e testes |
| `ThrowIfNegativeOrZero` e afins | .NET 8 | Guardas das entidades |
| `TimeProvider` | .NET 8 | Relógio do `CriarPedido`; `FakeTimeProvider` nos testes |
| Texto bruto `"""` | C# 11 | SQL dentro dos testes |
| `switch` e `is not null` com padrões | C# 7 a 9 | Tradução dos resultados e validações |
| Nullable reference types | C# 8 | Projeto inteiro |

| Recurso do Angular | Onde aparece |
|---|---|
| Componentes standalone e `input()` / `output()` | Todas as telas e componentes |
| Signals (`signal`, `computed`, `toSignal`) | Estado das telas, sem zone.js |
| Controle de fluxo `@if`, `@for`, `@let` | Templates |
| `switchMap` | Cancela a requisição anterior da listagem quando os filtros mudam |

## Limitações e risco residual

- **Carga muito acima da dos testes** pode estourar o timeout de comando enquanto uma requisição espera lock no `UPDATE` do estoque. A contenção nunca devolve 5xx nos testes, mas o limite de tempo é o padrão do EF Core.
- **Atomicidade restrita à requisição (D-16):** uma resposta 5xx ou uma queda de conexão não garante que nada foi gravado (a conexão pode cair depois do commit). Valores `identity` podem ter lacunas após rollback.
- **Preço alterado durante a criação** de um pedido fica fora do escopo: nenhum endpoint altera preços.
- **Healthcheck da API** só confirma que a porta aceita conexões (a imagem do ASP.NET não traz `curl`); a ordem de dependência do Compose (`db-init` concluído) cobre o banco.
- **Credencial de desenvolvimento** (senha do `sa`) está versionada de propósito, para o `docker compose up` funcionar sem passos extras. Serve só para ambiente local.
- **Sem autenticação**, edição ou cancelamento de pedidos (fora do escopo, spec 1.4).
- **Índices de `pedido`** (`ix_pedido_data_criacao_id` e `ix_pedido_cliente_id_data_criacao_id`) são hipóteses de projeto justificadas pelas consultas, **ainda sem medição** com volume.
- **Investigação da query do enunciado** (etapa E21 do plano) está pendente: o texto da query não foi fornecido, então não há análise nem `docs/investigacao-query.md`. O plano descreve o método.
