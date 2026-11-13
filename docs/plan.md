# Plano de implementação — Mini Gestão de Pedidos

> Complementa `docs/spec.md`, que define o **quê**. Este documento define o **como**: arquitetura, mecanismos (inclusive transação e concorrência), testes, Docker, configuração do Claude Code e sequência de implementação.
> Referências RN-xx e CA-xx apontam para a spec; PD-n, para a seção 9. Itens marcados como hipótese não foram medidos.

## 0. Problemas e contradições identificados

| # | Ponto | Tensão | Resolução proposta |
|---|---|---|---|
| 0.1 | Application depende só de Domain, mas a transação e o débito de estoque precisam envolver a **decisão de domínio** | Se a transação fica em Infrastructure, a regra de estoque precisa rodar *dentro* dela; se a regra fica em Application, a transação fica fora | Uma porta única definida em Application, cuja implementação (Infrastructure) abre a transação, lê o snapshot dos produtos, **invoca a decisão de domínio recebida como função** e aplica o débito atômico. Ver 2.2 (PD-1) |
| 0.2 | "Domínio protege estoque" × "Infrastructure protege estoque concorrente" | Com `UPDATE ... WHERE estoque >= @q` (decisão do autor), a mesma comparação existe no domínio e no SQL | **Duplicação assumida conscientemente:** o Domain avalia a regra sobre um snapshot (decisão, preço, total e mensagens completas da RN-11) e devolve as **baixas de estoque** a aplicar, sem mutar o saldo em memória; o predicado `estoque >= @q` do UPDATE é a **garantia atômica** sob concorrência, responsabilidade da Infrastructure. É uma comparação, com papéis distintos |
| 0.3 | Domain "independente de EF" × entidades ricas persistidas por EF | Setters privados, coleção encapsulada, construtor sem parâmetros | Mapeamento Fluent em Infrastructure (campos de apoio, construtores privados); **sem modelo de persistência separado**. O domínio mantém apenas construtores privados vazios, sem atributos de EF |
| 0.4 | RN-11 (mensagens agregadas por etapa, com caminhos `itens[i].quantidade`) × invariantes protegidas pelo domínio | A validação detalhada é de entrada; o domínio não conhece caminhos HTTP | `ValidadorCriarPedido` (Application) produz os erros agregados da etapa 1; o domínio mantém **guardas** (exceção em estado inválido, inalcançáveis no fluxo normal). Pequena duplicação de regras simples, coberta por testes nos dois níveis |
| 0.5 | Sem interfaces por convenção × NSubstitute exigido pela spec | NSubstitute precisa de fronteiras substituíveis | Apenas **duas interfaces**, com necessidade concreta (isolar EF das camadas internas e testar a orquestração sem banco) + `TimeProvider` (já existente no .NET). Casos de uso são classes concretas injetadas direto nos controllers |
| 0.6 | Spec exige .NET 10; **SDK 10 não está instalado** | `global.json` não pode fixar SDK 10; SDK 11 compila `net10.0` mas diverge das imagens Docker (`sdk:10.0`) | Pré-requisito do E1: instalar o SDK 10 (recomendado). Plano B: usar SDK 11 com `TargetFramework net10.0` e `rollForward` no `global.json` (PD-7) |
| 0.7 | `[ApiController]` devolve `400` com formato próprio e trata ausente × tipo errado de modo diferente do exigido (RN-11, CA-03) | Formato de erro divergente da spec (`codigo`, Problem Details) | `InvalidModelStateResponseFactory` customizado, DTO com campos anuláveis (`int?`): ausente → `422`, tipo errado → `400 REQUISICAO_INVALIDA` |
| 0.8 | Caminho dos scripts: spec cita `db/` "ou caminho definido no plano"; indicado pelo autor: `database/` | Diferença de nome | Plano usa `database/`; sem mudança de comportamento na spec. Ajuste textual opcional de `db/` para `database/` em commit separado |
| 0.9 | Spec 3.2 lista índice `item_pedido(pedido_id)` | A constraint única `(pedido_id, produto_id)` já oferece esse prefixo de índice | Índice dedicado dispensado (hipótese a confirmar por plano de execução). Ver 2.7 |
| 0.10 | MCP "oficial" para SQL Server | O SQL MCP Server da Microsoft (Data API builder) **não executa SQL arbitrário nem expõe planos/estatísticas** | Serve para inspeção de dados somente leitura; a investigação de desempenho usa `sqlcmd`. Ver 6.4 |
| 0.11 | Query do enunciado indisponível | Não é possível apontar erros reais nem medir | Seção 7 traz **só o método**; análise fica pendente até a query ser fornecida (etapa E21) |
| 0.12 | Modelo de resposta: serviço que acumula erros × contrato HTTP da spec | Um acumulador genérico não deve vazar para Domain nem alterar os corpos de erro já especificados | `Resultado` único em **Application** (tipo + lista de erros), traduzido pela **Api** para o Problem Details da spec 4.1, sem mudar o contrato. Domain mantém resultados tipados próprios. Ver 2.6 (PD-15) |

---

## 1. Arquitetura

### 1.1 Estrutura da solution
```
mini-gestao-pedidos/
├── Pedidos.slnx
├── global.json  Directory.Build.props  Directory.Packages.props   (gestão central de pacotes)
├── .editorconfig  .gitignore  .dockerignore  .env.example  docker-compose.yml
├── CLAUDE.md  README.md
├── .claude/        settings.json, hooks/, skills/        (6)
├── .mcp.json                                              (6.4, só após validação)
├── docs/           spec.md, plan.md, investigacao-query.md (7)
├── database/
│   ├── 00-create-database.sql   cria o banco se não existir (executa no master)
│   ├── 01-schema.sql            tabelas, FKs, CHECKs, índices (idempotente)
│   ├── 02-seed.sql              carga inicial determinística (idempotente)
│   └── investigacao/            gerar-volume.sql, consultas e resultados medidos (7)
├── scripts/        verificar-compose.ps1                  (4.4)
├── src/
│   ├── Pedidos.Domain/
│   ├── Pedidos.Application/
│   ├── Pedidos.Infrastructure/
│   └── Pedidos.Api/
├── tests/
│   ├── Pedidos.UnitTests/          Domain/ e Application/
│   └── Pedidos.IntegrationTests/   Api + Infrastructure + SQL Server real
└── frontend/
```
Os scripts 01/02 não usam `GO` nem instruções que exijam lote próprio, para rodarem tanto no `sqlcmd` (Compose) quanto via ADO.NET (testes).

### 1.2 Dependências (ProjectReference, verificadas em compilação)
```
Domain          → (nenhuma)
Application     → Domain
Infrastructure  → Application (e Domain, transitivamente)   implementa as portas de Application
Api             → Application, Infrastructure                Infrastructure só para a composição de DI
```
A direção é garantida pelas referências de projeto; **sem biblioteca de testes de arquitetura**. Um passo de verificação do E1 confere os `ProjectReference` por script simples.

### 1.3 Responsabilidades

| Projeto | Contém | Não contém |
|---|---|---|
| **Domain** | `Cliente`, `Produto` (`Estoque`, `TemEstoque`), `Pedido` (agregado com `ItemPedido`), `ItemSolicitado` (+ consolidação), `Pedido.Criar(...)` retornando resultado tipado com o `Pedido` e as **baixas de estoque** (produto, quantidade), constantes de limites (RN-24) | HTTP, EF Core, SQL, `TimeProvider` (recebe a data como argumento) |
| **Application** | Casos de uso `CriarPedido`, `ListarPedidos`, `ObterTopClientes`, `ListarCatalogo`; `ValidadorCriarPedido` (etapa 1); validação de parâmetros de consulta; **2 portas**; read models; **`Resultado` único** (tipo + lista de erros) | EF Core, SQL, tipos de HTTP |
| **Infrastructure** | `PedidosDbContext`, mapeamento Fluent, implementação das duas portas, transação, **débito atômico de estoque (UPDATE condicional)**, retry de deadlock, consultas (listagem, ranking, catálogo), ponto de extensão de interceptors | Regras de negócio |
| **Api** | Controllers, contratos de requisição, `ProblemDetails` (mapeamento resultado → HTTP), `IExceptionHandler`, parsing estrito de query string, `Program.cs` e DI | Regras de negócio, acesso a dados |

### 1.4 Abstrações (justificativa concreta)
Somente estas interfaces, ambas em Application:

1. **`IPedidoRepositorio`** — (a) Application não enxerga EF; (b) permite testar a orquestração e a ordem das etapas com NSubstitute, sem banco. Métodos: `ClienteExisteAsync`; `CriarAsync(clienteId, itensConsolidados, decidir)` onde `decidir` é a função de domínio (2.2).
2. **`IPedidoConsultas`** — listagem, ranking e catálogo (clientes/produtos). Retorna **read models**, não agregados. Separada da primeira porque têm consumidores e dublês de teste distintos. **Não é CQRS formal**: mesmo `DbContext`, sem modelos, buses ou handlers separados.

Sem interface para casos de uso nem para o `DbContext`. `TimeProvider` (BCL) substitui qualquer `IRelogio` próprio. Uma só interface com todos os métodos seria alternativa válida (PD-4).

### 1.5 Bibliotecas
| Biblioteca | Uso | Justificativa |
|---|---|---|
| EF Core SQL Server | persistência e consultas | exigido pela spec |
| xUnit, NSubstitute | testes | exigidos pela spec |
| Testcontainers.MsSql | SQL Server real nos testes | sobe a mesma imagem do Compose sem depender de ambiente manual |
| Respawn | limpar dados entre testes | evita recriar o banco a cada teste |
| Microsoft.AspNetCore.Mvc.Testing | `WebApplicationFactory<Program>` | testes HTTP reais em memória |
| Microsoft.Extensions.TimeProvider.Testing | `FakeTimeProvider` | datas controladas (CA-20, CA-29) |

**Não usar:** MediatR, AutoMapper (mapeamento por projeção e métodos estáticos simples), FluentValidation (validação em etapas é pequena e específica), Polly (um laço de retry de poucas linhas), Swagger (não pedido).

---

## 2. Backend e banco

### 2.1 Fluxo de criação (`POST /pedidos`)
```
Controller ── contrato (campos anuláveis; 400 se corpo/tipos inválidos)
   └─> CriarPedido.ExecutarAsync(comando)                               [Application]
        1. ValidadorCriarPedido: nulos, ids ≤ 0, itens vazio,
           quantidade ≤ 0, consolidação (Domain) sem estourar int        → 422 (erros agregados)
        2. repositório.ClienteExisteAsync                                → 404 CLIENTE_NAO_ENCONTRADO
        3. repositório.CriarAsync(clienteId, itensConsolidados,
              decidir: produtos => Pedido.Criar(clienteId, agora, itens, produtos))
              [Infrastructure: transação, snapshot, débito atômico; Domain: decisão]
              3a. todos os produtos existem?                             → 404 PRODUTO_NAO_ENCONTRADO (todos os ids)
              3b. subtotais e total cabem em decimal(18,2)?              → 422 VALIDACAO
              3c. estoque suficiente para todos?                         → 409 ESTOQUE_INSUFICIENTE (todos os afetados)
              3d. Infrastructure aplica as baixas por UPDATE condicional,
                  grava pedido e itens e confirma                         → 201
              3e. algum UPDATE sem linha afetada (perdeu a disputa):
                  rollback, nova leitura e nova decisão do domínio        → 409 ESTOQUE_INSUFICIENTE
```
- Etapas 1–2 ocorrem **antes** de abrir a transação (não seguram locks).
- O domínio decide sobre o **snapshot** dos produtos: verifica existência, limites e estoque, copia o preço, calcula subtotais e total, e devolve o `Pedido` e a lista de **baixas de estoque** a aplicar. Não altera saldos em memória.
- O domínio devolve um tipo fechado (`Criado | ProdutosInexistentes | LimiteExcedido | EstoqueInsuficiente`); Application o traduz, junto com as etapas 1–2, para o **`Resultado` único** (2.6). Sem exceções para fluxo de negócio.
- `agora` vem do `TimeProvider` e é **truncado para segundos** (a coluna é `datetime2(0)`, que arredonda; sem truncar, 23:59:59,6 viraria o dia seguinte e quebraria CA-20/CA-29).

### 2.2 Transação e concorrência

**Fronteira da transação — alternativas (PD-1):**

| Opção | Descrição | Prós | Contras |
|---|---|---|---|
| **T1 (recomendada)** | Uma porta, `CriarAsync(..., decidir)`: Infrastructure abre a transação, bloqueia, chama a função de domínio, persiste ou reverte | Sem Unit of Work exposto; regra fica no Domain, execução sob lock fica na Infrastructure; um único método para mockar | Função como argumento é indireta; dublê de NSubstitute exige `Returns(ci => ...)` |
| T2 | Porta devolve uma "sessão" (`IniciarAsync` → obter produtos bloqueados, adicionar pedido, confirmar) | Fluxo explícito em Application | É, na prática, um Unit of Work próprio — o que a direção de arquitetura pede para evitar |
| T3 | Application usa `PedidosDbContext` diretamente | Menos código, sem portas | Quebra a regra de dependência (Application passaria a conhecer EF/Infrastructure) e impede testar a orquestração sem banco |

**Mecanismo de concorrência — alternativas (PD-2):**

| Opção | Como funciona | Avaliação |
|---|---|---|
| A | Ler cada produto com `WITH (UPDLOCK, ROWLOCK)`, uma consulta por produto em ordem crescente de `Id`; o domínio decide sobre o estado bloqueado | Determinístico e regra 100% no domínio; porém segura locks da leitura ao commit e faz mais idas ao banco |
| B | Otimista com `rowversion` + retry | Exige coluna extra (muda a spec 3.2); sob 20 requisições simultâneas no mesmo produto (CA-14) gera ondas de retries; esgotar tentativas viraria `5xx`, violando RN-08 |
| **C (escolhida pelo autor)** | `UPDATE produto SET estoque = estoque - @q WHERE id = @id AND estoque >= @q`, em ordem crescente de `id`; 0 linhas afetadas = perdeu a disputa | Atômico, sem retry de conflito, sem mudar o schema, locks só durante o débito até o commit; a comparação `estoque >= q` também aparece no SQL (aceito, ver 0.2); mensagens completas da RN-11 exigem nova leitura após a falha |
| D | `SERIALIZABLE` ou `sp_getapplock` | `SERIALIZABLE` aumenta deadlocks por range locks; `sp_getapplock` equivale a A com mais um mecanismo |

**Como C funciona (afirmações a comprovar pelos testes, não pressupostos):**
1. Dentro de uma transação explícita (`READ COMMITTED`, `Database.BeginTransactionAsync`), Infrastructure lê um **snapshot** dos produtos (`AsNoTracking`, sem locks de atualização) e chama `decidir`.
2. Se o domínio falhar (404/422/409), a transação é descartada sem escrever nada.
3. Se o domínio devolver `Criado`, para cada baixa, **em ordem crescente de `Id`**, executa o UPDATE condicional (`ExecuteSqlInterpolatedAsync`, na mesma transação). Predicado e débito são uma única instrução atômica: duas transações nunca debitam o mesmo saldo, e a segunda espera o commit ou rollback da primeira antes de reavaliar `estoque >= @q`.
4. **0 linhas afetadas** = o saldo comprometido já não comporta a baixa. Rollback de tudo (inclusive débitos anteriores do mesmo pedido, CA-09), nova leitura do snapshot e nova decisão do domínio, que agora enxerga o saldo real e devolve `409` com **todos** os produtos afetados e o `disponivel` atual (RN-11). No máximo 3 tentativas por requisição; esgotadas, devolve `409` com o último snapshot, de modo que contenção nunca gera `5xx` (RN-08). Como o saldo só diminui no escopo, a segunda leitura confirma a falta na prática.
5. Todas as baixas aplicadas: insere `pedido` e `item_pedido` (preço do snapshot, `ValorTotal` somado) via EF, `SaveChanges` e commit. Falha em qualquer ponto antes do commit reverte pedido, itens e débitos (RN-07).
6. `EnableRetryOnFailure` **não** é usado (conflita com transações explícitas e esconde falhas).

**Deadlocks e conflitos:**
- Conflito de estoque **não é exceção nem deadlock**: é o resultado de 0 linhas afetadas, tratado no fluxo.
- A ordem crescente de `Id` na aplicação dos UPDATEs impede ciclos de espera entre pedidos com os mesmos produtos em ordens diferentes (CA-16). Não há leitura seguida de escrita sob lock (sem conversão S→X) e o snapshot não segura locks de atualização.
- **Defesa em profundidade:** erro 1205 (vítima de deadlock) repete a transação, no máximo 3 vezes; esgotadas, `500`. Não deve ocorrer; o CA-16 em laço é a evidência. A função de domínio é pura sobre o snapshot recebido, logo reexecutável (PD-3).
- `CHECK (estoque >= 0)` é a **última barreira**: violação (erro 547) indica defeito e vira `500`.
- **Preço:** vem do snapshot lido dentro da mesma transação (RN-04). Alteração de preço *durante* a criação está fora do escopo (nenhum endpoint a faz); fica registrado como limitação conhecida.
- Risco residual: carga muito acima da dos testes pode estourar o timeout de comando enquanto espera lock; registrado no README.

### 2.3 Mapeamento EF Core compatível com `database/`
- **Convenção de nomes (spec D-19):** tabelas no singular e colunas em snake_case (`cliente`, `produto`, `pedido`, `item_pedido`; `cliente_id`, `data_criacao`, `valor_total`, `preco_unitario`...); restrições e índices no mesmo padrão. As propriedades C# seguem em PascalCase.
- A tradução é **explícita** no mapeamento Fluent (`ToTable("item_pedido")`, `HasColumnName("data_criacao")`...), sem pacote de convenção de nomes (PD-16): são 4 tabelas e o teste de deriva de schema cobre qualquer divergência. O SQL escrito à mão (UPDATE condicional, consultas) usa os nomes do banco.
- Tabelas e colunas com os nomes exatos dos scripts. **Sem migrations e sem `EnsureCreated`**: o schema vem só dos scripts.
- `HasPrecision(18,2)` em `Preco`, `PrecoUnitario`, `ValorTotal` (colunas `preco`, `preco_unitario`, `valor_total`); `datetime2(0)` em `DataCriacao` (`data_criacao`) com conversor que marca `DateTimeKind.Utc` na leitura (serialização com `Z`).
- Agregado: `Pedido` → `Itens` por campo de apoio e `IReadOnlyCollection`; referências a cliente e produto **por id** (sem propriedades de navegação entre agregados); consultas usam `join` explícito.
- **Teste de deriva de schema** (integração): compara o modelo do EF (tabelas, colunas, tipos, precisão) com `INFORMATION_SCHEMA` do banco criado pelos scripts. Falha se um dos lados mudar sozinho.

### 2.4 Listagem
- `AsNoTracking`; filtros `ClienteId`, `DataCriacao >= inicio`, `DataCriacao < fimExclusivo`.
- Ordenação **fixa** `DataCriacao DESC, Id DESC` (RN-16); `COUNT` com o mesmo filtro e `OFFSET/FETCH`.
- **Duas etapas explícitas:** página de cabeçalhos (com nome do cliente) e, em seguida, itens com nome do produto por `PedidoId IN (...)`. Mais previsível que uma projeção aninhada única.
- Datas: parsing estrito `yyyy-MM-dd` no controller (`DateOnly.TryParseExact`, cultura invariante), erros agregados em `400`; Application valida semântica (`inicio <= fim`, `pagina >= 1`, `1 <= tamanhoPagina <= 100`) e calcula o intervalo `[inicio 00:00Z, fim+1d 00:00Z)`, protegendo `DateOnly.MaxValue`.
- OFFSET profundo é aceitável no escopo (paginação por número de página é requisito).

### 2.5 Ranking (top-clientes)
Agrega primeiro, une depois: `GROUP BY cliente_id` → `SUM(valor_total)` → `ORDER BY total DESC, cliente_id ASC` → `TOP 5` → junção com `cliente` só para as 5 linhas (RN-18 a RN-21, agrupa por id, nunca por nome).

### 2.6 Resultado único e padronização de erros
**Application** devolve sempre um `Resultado` (ou `Resultado<T>`): `Tipo` (`Sucesso | RequisicaoInvalida | Validacao | NaoEncontrado | Conflito`) e lista de `Erro` (`Codigo`, `Campo?`, `Mensagem`, `Dados?`). É o padrão de notificação (acumula erros por etapa, RN-11) **sem biblioteca**. Ele não vai para o Domain, que mantém resultados tipados próprios; Application faz a tradução.

A **Api** converte `Resultado` em Problem Details **exatamente como a spec 4.1** (`Validacao`→422 com `errors`; `NaoEncontrado`→404 e `Conflito`→409 com `produtos`, montados a partir de `Dados`; `RequisicaoInvalida`→400). O contrato da spec não muda (PD-15). Consultas (`GET`) usam o mesmo `Resultado`.

Todos os erros em Problem Details com `codigo` estável:

| Origem | HTTP / `codigo` |
|---|---|
| Binding/JSON, corpo ausente (`InvalidModelStateResponseFactory`, `IExceptionHandler`) | `400 REQUISICAO_INVALIDA` |
| Query inválida (formato, intervalo, limites) | `400 REQUISICAO_INVALIDA` com `errors` por parâmetro |
| Etapa 1 e limites monetários | `422 VALIDACAO` |
| Cliente / produto inexistente | `404 CLIENTE_NAO_ENCONTRADO` / `PRODUTO_NAO_ENCONTRADO` |
| Estoque | `409 ESTOQUE_INSUFICIENTE` |
| Inesperado, deadlock esgotado, violação de CHECK | `500` genérico, sem detalhes internos, com log |

Mensagens descritivas em português, centralizadas numa classe (identificam campo/item e causa, RN-11).

### 2.7 Índices propostos (em `01-schema.sql`)

| Índice | Colunas | Atende | Observação |
|---|---|---|---|
| `pk_<tabela>` (clustered) | `id` em cada tabela | junções e buscas por id | — |
| `ix_pedido_data_criacao_id` | `(data_criacao DESC, id DESC)` `INCLUDE (cliente_id, valor_total)` | `GET /pedidos` sem cliente (faixa + ordem estável); **ranking** por período (cobre a agregação sem acessar a tabela) | cobre RN-16 e RN-18 |
| `ix_pedido_cliente_id_data_criacao_id` | `(cliente_id, data_criacao DESC, id DESC)` `INCLUDE (valor_total)` | `GET /pedidos?clienteId=` com ou sem período | também serve à FK `cliente_id` |
| `uq_item_pedido_pedido_id_produto_id` | `(pedido_id, produto_id)` | integridade RN-09; busca dos itens por `pedido_id` na página | substitui o `ix_item_pedido_pedido_id` da spec 3.2 |
| FK `item_pedido.produto_id` | sem índice dedicado | — | não há exclusão de produtos; **hipótese**, a confirmar pelo plano |

Estes índices são **hipóteses de projeto**: justificados por raciocínio sobre as consultas, ainda **sem medição**. A confirmação (planos de execução e leituras lógicas, com e sem índice, em base com volume) está na seção 7 e no E21. Custo conhecido: três índices a manter em cada `INSERT` de pedido.

### 2.8 Seed
Datas fixas e ids explícitos (`SET IDENTITY_INSERT`) para ranking reproduzível. Cobre os mínimos da spec 3.3 (≥ 7 clientes com 2 homônimos; ≥ 6 produtos, um com estoque 5 e um com 0; ≥ 6 clientes com pedidos no mesmo período; dois períodos; ao menos um empate de total; totais e preços coerentes com RN-05).

---

## 3. Frontend (Angular)

Versão estável corrente na implementação, fixada no lockfile. Componentes standalone, `HttpClient`, formulários reativos, **sem NgRx** (estado local com signals).

```
frontend/src/app/
├── core/api/        pedidos-api.ts, catalogo-api.ts, api-error.ts (mapeia Problem Details), modelos
├── core/erros/      erros.service.ts (escopado por tela), notificacao.service.ts (falhas genéricas)
├── features/pedidos/
│   ├── lista/       pedidos-lista.component, pedidos-filtros.component
│   └── criar/       pedido-criar.component, linha-item.component
├── shared/          paginacao.component, erros-api.component, formatadores (moeda/data)
└── app.routes.ts    /pedidos, /pedidos/novo
```
- **Listagem:** filtros (cliente por `GET /clientes`, data inicial, data final com `input type=date`, que já entrega `yyyy-MM-dd`); valida `inicio <= fim` antes de consultar; `switchMap` cancela a requisição anterior; paginação com `totalItens`/`totalPaginas`.
- **Estados:** `carregando | ok | vazio | erro`. `erro` mostra mensagem do `ApiError` e botão "tentar novamente"; falha de rede e `5xx` usam mensagem genérica.
- **Criação:** cliente + linhas dinâmicas (produto, quantidade), envio bloqueado sem itens ou com quantidade ≤ 0 e **desabilitado durante o envio** (evita duplicidade); total estimado em **centavos inteiros** (`Math.round(preco * 100) * quantidade`) com aviso de que o valor final é o do servidor; exibição com `Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })`.
- **Erros da API:** o `ApiError` (mapeado do Problem Details) alimenta um **`ErrosService` escopado à tela** (provido no componente; limpo ao reenviar e ao sair; sem estado global que vaze erros antigos entre rotas). O `ErrosApiComponent` lista os erros e `porCampo('itens[0].quantidade')` liga mensagens aos controles. `422` → mensagens por campo; `404` → mensagem sobre o cliente/produto; `409` → lista de produtos com solicitado/disponível; `400`/`5xx` → mensagem geral via `NotificacaoService`. O formulário **não é limpo** em caso de erro (CA-39).
- **Após `201`:** limpa filtros, volta à página 1 e recarrega (D-18, CA-40).
- **Rede:** em desenvolvimento, `proxy.conf.json` leva `/api` à API; no Compose, o nginx serve o build e faz proxy de `/api/` **removendo o prefixo**, preservando os contratos da spec e evitando CORS.

---

## 4. Testes

### 4.1 Camadas
| Camada | Projeto | Escopo | Dublês |
|---|---|---|---|
| Unitários de domínio | `UnitTests/Domain` | `Pedido.Criar`, consolidação, total, limites, `BaixarEstoque` | nenhum |
| Unitários de aplicação | `UnitTests/Application` | ordem e curto-circuito das etapas, agregação de erros, validação de parâmetros, mapeamento de resultados | NSubstitute nas 2 portas; `FakeTimeProvider` |
| Integração | `IntegrationTests` | API + Infrastructure contra **SQL Server real** | nenhum para banco |
| Verificação do Compose | `scripts/verificar-compose.ps1` + README | subida completa | — |
| Frontend | roteiro manual no README | CA-38 a CA-40 | — (a spec não pede testes automatizados) |

### 4.2 SQL Server real nos testes de integração
- **Testcontainers (MsSql)**, mesma imagem e tag do Compose, uma instância por execução (fixture de assembly). Requer Docker em execução na máquina; documentado no README.
- Um banco por classe de teste (`CREATE DATABASE` + `01-schema.sql`; `02-seed.sql` só onde o teste precisa). Classes rodam em paralelo; testes dentro da classe, em sequência.
- Isolamento entre testes da mesma classe: **Respawn**; testes de criação usam clientes e produtos próprios. Testes que dependem de estado global (listagem, relatório) criam seus dados e ficam em classes dedicadas.
- `WebApplicationFactory<Program>` com a string de conexão do container.

### 4.3 Concorrência e rollback
- **Rollback (CA-13):** `SaveChangesInterceptor` de teste, registrado pelo ponto de extensão de interceptors da Infrastructure via `ConfigureTestServices`, lança exceção ao gravar os itens (depois do débito, antes do commit). Verifica estoque, pedidos e itens inalterados.
- **Concorrência (CA-14 a CA-16):** `Task.WhenAll` com barreira de largada sobre chamadas HTTP reais; asserções sobre contagem de `201`/`409`, ausência de `5xx`, estoque final e soma em `item_pedido`. CA-16 usa ordens opostas de produtos e é **repetido em laço** (ex.: 20 rodadas) para expor deadlocks. Um teste adicional desliga o retry para provar que a ordenação sozinha evita deadlock.
- **Perda de disputa com vários produtos:** dois pedidos de 2 produtos em ordens opostas e estoque para apenas um; o perdedor recebe `409` com o produto em falta e o débito do **primeiro** produto é revertido (CA-09 sob concorrência).
- Convenção de rastreio: cada teste recebe `[Trait("CA", "CA-14")]`, base para a skill `verificar-ca` (6.3).

### 4.4 Verificação do Docker Compose (CA-33, separada dos testes)
`scripts/verificar-compose.ps1` executa e o README descreve o mesmo procedimento: `docker compose down -v` → `up -d --build --wait` → conferência de saúde dos serviços → contagens do seed via `sqlcmd` no container → chamadas a `GET /pedidos`, `GET /relatorios/top-clientes` e um `POST /pedidos` → `GET /` do frontend → nova execução de `db-init` para provar idempotência (CA-34).

### 4.5 Relação com os critérios de aceite
| Critérios | Camada principal |
|---|---|
| CA-01 a CA-17c | Integração; regras puras também em unitários de Domain/Application |
| CA-18 a CA-25 | Integração (+ unitários de datas e paginação) |
| CA-26 a CA-32 | Integração |
| CA-34 a CA-37 | Integração (scripts, constraints, `decimal(18,2)`, seed) |
| CA-33 | `verificar-compose.ps1` + README |
| CA-38 a CA-40 | Roteiro manual do frontend |
| CA-41, CA-42 | Estrutura dos projetos de teste |

---

## 5. Docker

| Serviço | Imagem / build | Healthcheck | Depende de |
|---|---|---|---|
| `db` | `mcr.microsoft.com/mssql/server:2022-latest` (tag fixada na implementação) | `sqlcmd -C -Q "SELECT 1"` (intervalo 5 s, `start_period` 20 s, 20 tentativas) | — |
| `db-init` | mesma imagem (contém `sqlcmd`), scripts montados somente leitura | — (serviço de uma execução, `restart: "no"`) | `db` saudável |
| `api` | build multi-stage `sdk:10.0` → `aspnet:10.0`, porta 8080 | verificação TCP na porta 8080 (a imagem não traz `curl`) | `db-init` com `service_completed_successfully` |
| `frontend` | build `node` → `nginx:alpine`, `nginx.conf` com proxy `/api/` | `wget -qO- http://localhost/` | `api` saudável |

- `db-init` executa `00-create-database.sql` (no `master`), depois `01-schema.sql` e `02-seed.sql` no banco `Pedidos`, com `sqlcmd -b` (aborta em erro). Idempotente: pode rodar a cada `up`.
- Garantia de `docker compose up`: senha do SA com **valor padrão de desenvolvimento** (`${MSSQL_SA_PASSWORD:-...}`) para funcionar sem copiar `.env`; `.env.example` documenta a variável; `.env` é ignorado pelo git (PD-8). Portas: frontend `4200→80`, API `5080→8080`, banco `1433→1433` (necessária para `sqlcmd` local e MCP); volume nomeado `mssql-data` (`down -v` zera).
- `--wait` no `up` depende dos healthchecks; por isso todos os serviços de longa duração têm um.
- Nomes de comandos do `sqlcmd` e caminho das ferramentas na imagem serão confirmados no E3 (**a verificar**, não assumidos).

---

## 6. Configuração do Claude Code

### 6.1 `CLAUDE.md` (conteúdo previsto, curto)
1. Visão: sistema, link para `docs/spec.md` e `docs/plan.md`; numeração RN/CA como fonte de verdade.
2. Arquitetura: quatro projetos e direção das dependências; **proibido** MediatR, barramento de eventos, repositório genérico, Unit of Work próprio, CQRS formal, interfaces sem necessidade concreta; só duas portas em Application.
3. Regras de ouro: preço vem do cadastro; estoque nunca negativo; criação em uma transação; débito de estoque por UPDATE condicional (`estoque >= @q`) em ordem crescente de `Id`; dinheiro sempre `decimal`; `DataCriacao` truncada em segundos.
4. Comandos: `dotnet build`, `dotnet test --filter`, `docker compose up --build`, `scripts/verificar-compose.ps1`.
5. Testes: integração **sempre contra SQL Server real** (nunca in-memory/SQLite); todo teste de critério leva `[Trait("CA","CA-xx")]`.
6. Banco: scripts em `database/` são a fonte do schema; sem migrations; idempotentes.
7. Git: commits pequenos e semânticos em português (skill `commit-semantico`), **sem `Co-Authored-By`**, só quando pedido; nunca reescrever histórico.
8. Segurança: nunca ler `.env`; MCP de banco somente leitura e só contra o banco local de desenvolvimento.
9. Definição de pronto de cada etapa: build verde, testes da etapa passando, critérios de aceite citados.

### 6.2 Hook (gatilho e comando)
- **Gatilho:** `PostToolUse`, `matcher: "Edit|Write|MultiEdit"`, apenas quando o arquivo termina em `.cs`.
- **Comando:** `powershell -File .claude/hooks/formatar-cs.ps1` — lê o JSON do hook na entrada padrão, extrai `tool_input.file_path` e, se for `.cs`, executa `dotnet format whitespace --folder --include "<arquivo>" --verbosity quiet`.
- **Por quê:** `whitespace` não exige build nem restore (rápido) e evita commits de ruído de estilo. Falha do hook não bloqueia o fluxo (saída 0).
- Alternativa considerada: `PreToolUse` em `Bash` negando `git commit` com `Co-Authored-By`, que impõe por mecanismo a regra de commits do projeto (PD-12).
- `.claude/settings.json` (versionado) também declara permissões: permitir `dotnet build/test/format`, `docker compose config/ps/logs/up`, `git status/diff/log`; **pedir confirmação** para `docker compose down -v` e `git push`; **negar** leitura de `.env`. O `.claude/settings.local.json` existente (permite `git add/commit`) permanece local.

### 6.3 Skill customizada: `verificar-ca`
- **Local:** `.claude/skills/verificar-ca/SKILL.md`. **Propósito:** ligar spec e testes, dado um ou mais CA (ou "todos").
- **Passos da skill:** (1) ler os CA em `docs/spec.md`; (2) localizar testes com `[Trait("CA","CA-xx")]`; (3) executar `dotnet test --filter "CA=CA-xx"`; (4) emitir tabela CA × teste × resultado, marcando **sem teste** e **manuais** (CA-33, CA-38 a CA-40, com ponteiro ao README); (5) nunca declarar CA verificado sem ter executado o teste nessa rodada.
- **Por que serve ao projeto:** a spec tem ~45 critérios; a matriz de rastreabilidade (4.5) deixa de ser manual.

### 6.4 MCP para SQL Server

**Estado: nada foi instalado, configurado ou conectado.** O que segue vem de **leitura da documentação** (2026-11-12) e será confirmado por execução em etapa própria (E20).

- **Implementação identificada:** **SQL MCP Server**, parte do **Data API builder (DAB)** da Microsoft, ferramenta .NET de código aberto. Documentação: [visão geral](https://learn.microsoft.com/en-us/azure/data-api-builder/mcp/overview), [início rápido](https://learn.microsoft.com/en-us/azure/data-api-builder/mcp/quickstart-visual-studio-code), [ferramentas DML](https://learn.microsoft.com/en-us/azure/data-api-builder/mcp/data-manipulation-language-tools), [transporte stdio](https://learn.microsoft.com/en-us/azure/data-api-builder/mcp/stdio-transport), [autenticação](https://learn.microsoft.com/en-us/azure/data-api-builder/mcp/how-to-configure-authentication).
- **Versão:** disponível a partir do DAB 1.7; a documentação recomenda a 2.0. Status de preview/GA e suporte: **não encontrado na documentação lida**.
- **O que expõe:** apenas as entidades configuradas, por sete ferramentas (`describe_entities`, `create_record`, `read_records`, `update_record`, `delete_record`, `execute_entity`, `aggregate_records`; a última só no 2.0). **Não aceita SQL arbitrário** e a documentação diz que não faz NL2SQL; não há ferramenta para plano de execução nem `SET STATISTICS`. `read_records` trabalha sobre uma tabela ou view por vez, sem joins.
- **Configuração proposta:**
  1. DAB como ferramenta local do repositório (`dotnet new tool-manifest` + `dotnet tool install microsoft.dataapibuilder`, versão fixada).
  2. `dab init --database-type mssql` com `@env('MSSQL_CONNECTION_STRING')`, `dab-config.json` em `tools/sql-mcp/` (versionado, **sem segredo**), host em desenvolvimento, ligado só em `localhost`.
  3. `dab add` para `cliente`, `produto`, `pedido`, `item_pedido` com permissão `anonymous:read` apenas, e descrições de campos (a documentação avisa que sem elas o agente adivinha nomes de colunas).
  4. Desligar em `runtime.mcp.dml-tools`: `create-record`, `update-record`, `delete-record`, `execute-entity` (defesa em profundidade).
  5. Cliente MCP no Claude Code: transporte HTTP (`http://localhost:5000/mcp`, caminho `/mcp`) ou stdio (`dab start --mcp-stdio role:anonymous`), registrado em `.mcp.json` de escopo de projeto. **A sintaxe exata do `claude mcp add` e do `.mcp.json` será conferida na documentação do Claude Code no momento da configuração** (a documentação do DAB mostra exemplos para VS Code e Claude Desktop, não para Claude Code).
- **Credenciais:** login SQL dedicado `mcp_reader`, apenas `db_datareader`, criado por script de desenvolvimento em `database/dev/` e **fora** da carga do Compose padrão; senha só no `.env` (ignorado pelo git). Nunca usar o login `sa`.
- **Permissões (três camadas):** login somente leitura no banco + permissão `read` por entidade + ferramentas de escrita desligadas no DAB. Permissão no Claude Code: aprovar uso das ferramentas de leitura deste servidor; escrita não existe.
- **Uso durante o desenvolvimento:** conferir o seed e o ranking (CA-35, CA-32), inspecionar estoque e itens após os testes de concorrência, e comparar contagens. **Não** serve para a investigação de desempenho da seção 7; para isso usa-se `sqlcmd` via `docker compose exec`.
- **Riscos documentados:** a imagem de contêiner do DAB, executada só com `DAB_CONNSTRING`, expõe todas as tabelas com CRUD anônimo (blog da Microsoft de 2026-11-03); por isso **não usar o padrão da imagem**, só o `dab-config.json` próprio. O MCP herda o provedor `Unauthenticated` no 2.0, aceitável apenas em `localhost`.
- **Alternativas (não recomendadas como padrão):** servidores comunitários (`mssql_mcp_server` em Python, `alyio.mcpmssql` somente leitura por padrão, entre outros) aceitam SQL arbitrário e permitiriam `SET STATISTICS IO`, mas não têm respaldo oficial e exigem auditoria de cadeia de suprimentos antes de uso.

---

## 7. Investigação da query do enunciado

**Pendente: o texto da query não foi fornecido.** Esta seção define o método; a análise e as conclusões ficam em `docs/investigacao-query.md`, criado na etapa E21. Nada abaixo é resultado medido.

### 7.1 Separação obrigatória: hipótese × evidência
`docs/investigacao-query.md` terá duas tabelas: **Hipóteses** (texto, como testar) e **Evidências** (saída bruta em `database/investigacao/resultados/`, números copiados dela). Conclusão só entra em "Evidências" com a medição anexada.

### 7.2 Checklist de hipóteses a verificar quando a query existir (não são achados)
- **Resultado:** junção com `item_pedido` multiplicando linhas antes de `SUM` do valor do pedido; agrupamento por nome (homônimos, RN-19); `BETWEEN` com `datetime` excluindo parte do último dia; `TOP` sem desempate; `DISTINCT` escondendo duplicidade; tratamento de `NULL`; nome `DataPedido` × `DataCriacao`.
- **Desempenho:** função sobre coluna filtrada (ex.: conversão de data) impedindo `Seek`; subconsulta correlacionada; `SELECT *`; ausência de índice de apoio; ordenação sem índice; conversões implícitas.

### 7.3 Dados
Seed pequeno (correção) + `database/investigacao/gerar-volume.sql` (geração em conjunto, determinística, base separada `PedidosVolume`, ordem de grandeza a definir, p. ex. centenas de milhares de pedidos). Nunca incluída nos testes nem no Compose padrão.

### 7.4 Comparar resultados
Executar original e corrigida no seed e no volume; comparar com `EXCEPT` nos dois sentidos, contagem de linhas e `CHECKSUM_AGG`; recalcular o total por uma consulta independente (`SUM(quantidade * preco_unitario)` sobre `item_pedido`) para expor multiplicação de linhas. Toda diferença entre original e corrigida precisa ser explicada linha a linha.

### 7.5 Comparar desempenho
`SET STATISTICS IO, TIME ON` e plano de execução **real** (`SET STATISTICS XML ON` ou plano real no cliente); registrar leituras lógicas, CPU, tempo, operadores (scan × seek, key lookup, spill de sort, linhas estimadas × reais). Cinco execuções com cache aquecido; execução a frio (`CHECKPOINT` + `DBCC DROPCLEANBUFFERS`) **somente** no banco local de desenvolvimento. Comparar antes e depois de cada índice com os mesmos dados; dicas `WITH (INDEX(...))` só para diagnóstico. Ferramenta: `sqlcmd` em `docker compose exec` (o MCP do DAB não fornece estas medições).

### 7.6 Correção e índices
A consulta corrigida usará o modelo do plano (agregar por `cliente_id`, filtro por faixa sobre `data_criacao`, desempate determinístico). Índices candidatos já listados em 2.7; mantidos, alterados ou removidos conforme as **evidências**. O README registra a correspondência `DataPedido` ↔ `DataCriacao`.

---

## 8. Sequência de implementação

Requisitos obrigatórios primeiro (E0–E18); diferenciais depois (E19–E22). Cada etapa é pequena, termina com build verde e testes da etapa passando, e vira **um commit** (ou poucos), em português, semântico, sem `Co-Authored-By`. Os testes entram **no mesmo commit** do código que cobrem; correções aparecem como commits novos, sem reescrever histórico.

| # | Entregável | Validação | Commit sugerido |
|---|---|---|---|
| E0 | `docs/plan.md` aprovado (+ ajuste textual `db/`→`database/` na spec, em commit à parte) | revisão humana registrada | `docs: adiciona plano de implementação` |
| E1 | Solution `.slnx`, 4 projetos + 2 de teste, props, `global.json`, `.gitignore`, `.editorconfig`; **pré-requisito: SDK .NET 10** | `dotnet build` verde; script confere `ProjectReference` (Domain sem referências) | `chore: cria solution em camadas e projetos de teste` |
| E2 | `CLAUDE.md`, `.claude/settings.json`, hook de formatação | editar um `.cs` aciona o hook (verificado) | `docs: adiciona CLAUDE.md e configuração base do Claude Code` |
| E3 | `database/00…02` + Compose parcial (`db`, `db-init`) | `docker compose up db db-init`; contagens via `sqlcmd`; segunda execução sem erro | `feat(banco): adiciona scripts de schema e carga inicial` |
| E4 | Fixture Testcontainers + Respawn + testes de schema/seed | CA-34 a CA-37 | `test(banco): valida schema e carga inicial em SQL Server real` |
| E5 | Domain: `Cliente`, `Produto`, `TemEstoque` | unitários | `feat(domain): modela cliente e produto` |
| E6 | Domain: `ItemSolicitado`, consolidação, `Pedido.Criar`, resultado tipado com baixas de estoque, limites | unitários (CA-12, CA-17, CA-17b/c no nível de domínio) | `feat(domain): cria pedido com consolidação, preço e total` |
| E7 | Infrastructure: `DbContext`, mapeamento, teste de deriva de schema | integração | `feat(infra): mapeia entidades no EF Core compatível com os scripts` |
| E8 | Application: `Resultado` único, `ValidadorCriarPedido`, `CriarPedido`, porta `IPedidoRepositorio` | unitários com NSubstitute (ordem e curto-circuito das etapas, CA-05c/05d) | `feat(app): adiciona caso de uso de criação de pedido` |
| E9 | Infrastructure: `PedidoRepositorio` com transação, snapshot, UPDATE condicional ordenado, reavaliação ao ter 0 linhas afetadas, retry de deadlock | integração do repositório | `feat(infra): persiste pedido com transação e débito atômico de estoque` |
| E10 | Api: `POST /pedidos`, mapeamento `Resultado` → Problem Details, `IExceptionHandler` | CA-01 a CA-13, CA-17 a 17c | `feat(api): expõe POST /pedidos com erros padronizados` |
| E11 | Testes de concorrência e deadlock (e correções, se surgirem) | CA-14 a CA-16 (laço de repetição) | `test: cobre concorrência e deadlock na criação de pedidos` |
| E12 | `GET /pedidos` (Application + Infrastructure + Api) | CA-18 a CA-25 | `feat: adiciona listagem de pedidos com filtros e paginação` |
| E13 | `GET /relatorios/top-clientes` | CA-26 a CA-32 | `feat: adiciona ranking dos cinco maiores clientes` |
| E14 | `GET /clientes`, `GET /produtos` | integração | `feat(api): adiciona consultas de clientes e produtos para o frontend` |
| E15 | Frontend: estrutura, serviços, listagem e filtros | roteiro manual (CA-38) | `feat(frontend): adiciona listagem de pedidos com filtros` |
| E16 | Frontend: criação com tratamento de erros | roteiro manual (CA-39, CA-40) | `feat(frontend): adiciona criação de pedido` |
| E17 | Dockerfiles, nginx, Compose completo, `verificar-compose.ps1` | CA-33 executado e registrado | `chore(docker): completa execução com docker compose up` |
| E18 | `README.md` (execução, testes, Compose, roteiros, risco residual, `DataPedido`↔`DataCriacao`) | seguir o README do zero | `docs: adiciona README` |
| — | **Fim dos obrigatórios** | todos os CA obrigatórios cobertos | — |
| E19 | Skill `verificar-ca` + traits nos testes existentes | rodar a skill e conferir a matriz | `chore: adiciona skill de verificação de critérios de aceite` |
| E20 | MCP SQL (DAB): instalação, `dab-config.json`, login `mcp_reader`, `.mcp.json`; **validar a conexão com evidência** (`describe_entities`, leitura de `pedido`) | saída anexada; escrita comprovadamente indisponível | `chore: adiciona MCP somente leitura para o SQL Server` |
| E21 | Investigação da query (**depende da query do enunciado**): volume, comparação, planos, índices | evidências em `database/investigacao/resultados/` | `docs: registra investigação da query do enunciado` |
| E22 | (Opcional) testes de frontend e revisão final do plano | — | `test(frontend): ...` / `docs: atualiza plano` |

---

## 9. Decisões para revisão

| # | Decisão | Alternativas | Recomendação e justificativa | Risco |
|---|---|---|---|---|
| PD-1 | Fronteira da transação | T1 função de decisão; T2 sessão (UoW); T3 EF em Application | **T1**: preserva a regra de dependência e a regra no Domain sem expor Unit of Work | Indireção; dublê de teste menos óbvio |
| PD-2 | Mecanismo de concorrência | A `UPDLOCK` ordenado; B `rowversion`; C `UPDATE` condicional; D `SERIALIZABLE`/applock | **C (decidido pelo autor)**: atômico, sem retry de conflito, sem mudar o schema, locks curtos | Comparação de estoque também no SQL (duplicação consciente); precisa de nova leitura após 0 linhas afetadas para a mensagem completa |
| PD-3 | Retry de deadlock | sem retry; retry 3× | **3×**, como defesa; a ordem crescente é o mecanismo principal | Mascarar um defeito de ordenação; o teste que desliga o retry evita isso |
| PD-4 | Interfaces de Application | duas portas; uma só | **Duas** (escrita e leitura) por consumidores e dublês distintos | Unificar fica a critério do revisor |
| PD-5 | Contratos de resposta | reusar read models de Application; DTOs próprios na Api | **Reusar** na leitura (formato idêntico à spec) e mapear só a resposta do `POST`; criar DTOs de Api se divergirem | Acoplamento do contrato ao modelo de leitura |
| PD-6 | Onde fica a validação da etapa 1 | Application; Domain | **Application** (mensagens com caminho), guardas no Domain | Duplicação de regras simples |
| PD-7 | SDK .NET | instalar SDK 10; usar SDK 11 com alvo `net10.0` | **SDK 10**, igual às imagens Docker | Instalação é ação do desenvolvedor (altera o sistema) |
| PD-8 | Senha do SA no Compose | padrão de desenvolvimento no `compose`; exigir `.env` | **Padrão de dev + `.env.example`**, para `up` sem passos | Credencial padrão no repositório; só local |
| PD-9 | Healthcheck da API | TCP; endpoint `/health` | **TCP** (sem endpoint novo, sem `curl` na imagem) | Não prova que o banco responde (ordem do Compose cobre) |
| PD-10 | SQL Server nos testes | Testcontainers; `db` do Compose | **Testcontainers**: independente de ambiente manual | Exige Docker rodando |
| PD-11 | MCP de SQL | DAB SQL MCP Server; servidores comunitários | **DAB** (oficial, somente entidades, somente leitura configurável); planos e estatísticas via `sqlcmd` | Preview; sem SQL livre; sintaxe de integração com Claude Code a confirmar |
| PD-12 | Hook | formatação de `.cs`; barrar commit com coautoria | **Formatação**; o outro fica como opção | — |
| PD-13 | Testes de frontend | só roteiro manual; testes de componente | **Roteiro manual** (spec não pede automatizados) | Regressões visuais não detectadas |
| PD-14 | Índices | conjunto da 2.7; adiar até medir | **Criar o conjunto mínimo** e revisar com evidências (E21) | Sem medição, são hipóteses |
| PD-15 | Modelo de resposta | resultado único + contrato da spec; contrato unificado `erros[]` (altera a spec 4.1); tipos de falha distintos | **Resultado único + contrato da spec (decidido pelo autor)**: notificação acumulada em Application, spec intacta; no frontend, serviço de erros por tela | Mapeamento extra na Api; `Dados` do erro exige convenção por código |
| PD-16 | Tradução dos nomes do banco (snake_case, singular) | Mapeamento Fluent explícito; pacote `EFCore.NamingConventions` | **Explícito**: 4 tabelas, sem dependência extra, e o teste de deriva de schema detecta divergência | Mais linhas de configuração; esquecer uma coluna é pego pelo teste |

### Revisão humana

**Status: pendente.** Nenhum ajuste, objeção ou aprovação foi registrado ou presumido.

| Item | Decisão do revisor | Observações |
|---|---|---|
| (a preencher) | | |
