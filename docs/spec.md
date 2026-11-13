# Especificação — Mini Gestão de Pedidos

> Esta especificação registra as restrições técnicas já escolhidas e os comportamentos esperados.
> Arquitetura, padrões de implementação e mecanismos (inclusive de concorrência) são definidos em `docs/plan.md`.
> Referências **[D-n]** apontam para a seção "Decisões tomadas" ao final.

## 1. Objetivo e limites do escopo

### 1.1 Objetivo
Aplicação de mini gestão de pedidos que permite registrar pedidos de clientes (validando cliente e estoque), listá-los com filtros e paginação, e obter o ranking dos cinco clientes com maior valor total de pedidos em um período.

### 1.2 Restrições técnicas já definidas
| Item | Escolha |
|---|---|
| Backend | .NET 10, C#, Entity Framework Core |
| Banco | SQL Server |
| Frontend | Angular com TypeScript |
| Execução | Docker Compose (banco, API e frontend) |
| Testes | xUnit, NSubstitute e testes de integração contra SQL Server real |
| Banco (scripts) | Scripts SQL de criação do schema e de carga inicial |

### 1.3 Dentro do escopo
- Entidades Clientes, Produtos, Pedidos, ItensPedido.
- `POST /pedidos`, `GET /pedidos`, `GET /relatorios/top-clientes`.
- Endpoints auxiliares de leitura `GET /clientes` e `GET /produtos` (somente para alimentar o frontend) **[D-5]**.
- Frontend com listagem de pedidos, filtros e criação de pedido.
- Scripts SQL de criação e carga inicial.
- Testes unitários e de integração.

### 1.4 Fora do escopo
Autenticação/autorização; CRUD de clientes e produtos; edição, cancelamento ou exclusão de pedidos; consulta de pedido por id; pagamento, frete, descontos, impostos; multi-moeda; tela do relatório top-clientes **[D-14]**; internacionalização.

---

## 2. Regras de negócio e validações

### 2.1 Criação de pedido
- **RN-01** O pedido pertence a exatamente um cliente existente.
- **RN-02** O pedido contém **pelo menos um item**; lista vazia ou ausente é inválida.
- **RN-03** Cada item referencia um produto existente e tem `quantidade` **inteira e maior que zero**. Zero, negativos, decimais e ausência são inválidos.
- **RN-04** O preço unitário **nunca vem do cliente da API**: é lido do cadastro do produto no momento da criação e **copiado para o item** (`PrecoUnitario`). Alterações posteriores no preço do produto não afetam pedidos existentes. Campo de preço enviado no corpo é ignorado.
- **RN-05** `Subtotal = Quantidade × PrecoUnitario`; `ValorTotal` do pedido = soma dos subtotais. Valores calculados pelo servidor.
- **RN-06** A quantidade pedida de cada produto não pode exceder o estoque disponível.
- **RN-07** **Atomicidade:** criação do pedido, dos itens e baixa de estoque ocorrem na mesma transação. Em caso de falha antes do commit, nenhuma alteração de negócio dessa requisição é persistida: pedido, itens e baixa de estoque são revertidos. Isso não impede que outras requisições concorrentes alterem o banco, nem que valores `identity` apresentem lacunas após rollback.
- **RN-08** **Concorrência:** sob requisições simultâneas, o estoque de um produto **nunca fica negativo** e a soma das quantidades vendidas em pedidos confirmados **nunca excede** o estoque inicial. Requisições que perdem a disputa falham com `409` e sem efeitos colaterais. Contenção **não pode** gerar resposta `5xx` (ex.: erro de deadlock exposto ao cliente). O mecanismo é definido em `docs/plan.md`.
- **RN-09** **Produto repetido no mesmo pedido [D-1]:** itens com o mesmo `produtoId` são **consolidados em um único item**, somando as quantidades. A validação de estoque (RN-06) usa a **soma**. O pedido persistido tem no máximo um item por produto.
- **RN-10** Data do pedido (`DataCriacao`) definida pelo servidor, em UTC **[D-11]**.
- **RN-11** **Validação em etapas [D-15].** As validações ocorrem na ordem abaixo. A requisição é interrompida na **primeira etapa com falhas**, e todos os erros identificados **nessa etapa** são reportados juntos na mesma resposta; etapas seguintes não são avaliadas.
  1. **Estrutura e campos:** corpo, tipos, `clienteId`, `itens` não vazio, `produtoId` e `quantidade` válidos, quantidade consolidada dentro do limite (RN-24) → `400` (malformada) ou `422` (semântica). Se o corpo for malformado (`400`), a requisição é interrompida sem avaliar os demais campos.
  2. **Existência do cliente** → `404` `CLIENTE_NAO_ENCONTRADO`.
  3. **Existência dos produtos** → `404` `PRODUTO_NAO_ENCONTRADO`, listando **todos** os `produtoId` inexistentes. Concluída esta etapa (preços conhecidos), verificam-se os limites monetários de subtotais e total (RN-24) → `422` `VALIDACAO`.
  4. **Disponibilidade de estoque** → `409` `ESTOQUE_INSUFICIENTE`, listando **todos** os produtos afetados, com quantidade solicitada (consolidada) e disponível.

  Cada erro deve ser descritivo: identifica o campo ou item afetado (ex.: `itens[1].quantidade`, `produtoId` 7) e a causa. A ordem torna o comportamento previsível: por exemplo, cliente inexistente com produto sem estoque resulta em `404` de cliente, e produto inexistente com outro sem estoque resulta em `404` de produto.

### 2.2 Listagem de pedidos
- **RN-12** Filtros opcionais e combináveis (AND): `clienteId`, `dataInicio`, `dataFim`.
- **RN-13** Datas no formato `yyyy-MM-dd` (sem horário). Intervalo **inclusivo nas duas pontas**, em UTC **[D-3]**: `dataInicio` equivale a `DataCriacao >= dataInicio 00:00:00Z`; `dataFim` equivale a `DataCriacao < (dataFim + 1 dia) 00:00:00Z`. Pode-se informar só uma das pontas.
- **RN-14** `dataInicio > dataFim` → `400`. Formato inválido de data → `400`.
- **RN-15** `clienteId` inexistente não é erro: retorna lista vazia com `200` **[D-8]**.
- **RN-16** **Ordenação fixa e estável:** `DataCriacao` decrescente, desempate por `Id` decrescente. Mesma ordenação para qualquer página, de modo que paginar nunca repete nem omite registros (na ausência de novas inserções).
- **RN-17** Paginação: `pagina` (base 1, padrão 1), `tamanhoPagina` (padrão 20, máximo 100) **[D-9]**. Valores fora dos limites → `400`. Página além do total → `200` com lista vazia. `totalItens` reflete o total após filtros.

### 2.3 Relatório top-clientes
- **RN-18** Retorna até **5** clientes ordenados por soma de `ValorTotal` de seus pedidos com `DataCriacao` no período (mesma regra inclusiva de RN-13), decrescente.
- **RN-19** **Agrupamento por `ClienteId`**, nunca por nome. Dois clientes com o mesmo nome aparecem como entradas distintas, cada uma com seu `clienteId` e seu total.
- **RN-20** Desempate de totais iguais: `ClienteId` crescente (resultado determinístico).
- **RN-21** Clientes sem pedidos no período não aparecem; se houver menos de 5 clientes com pedidos, retorna-se menos de 5 (inclusive lista vazia).
- **RN-22** `dataInicio` e `dataFim` **obrigatórios** **[D-4]**; mesmas validações de RN-13/RN-14.

### 2.4 Precisão e limites numéricos
- **RN-23** Valores monetários usam tipo decimal exato: `decimal` em C#, `decimal(18,2)` no SQL Server, nunca `float`/`double`. Preços têm 2 casas decimais e quantidades são inteiras, logo subtotais e totais são exatos, sem arredondamento. Na API, valores são números JSON com até 2 casas (ex.: `89.90`). No frontend, a formatação monetária não pode passar por aritmética de ponto flutuante que altere o valor exibido.
- **RN-24** **Limites numéricos:** quantidades consolidadas (RN-09) devem caber no tipo definido para quantidade e estoque (`int`). Subtotais e o total do pedido devem caber na precisão monetária definida (`decimal(18,2)`). Valores fora desses limites produzem erro de validação (`422` `VALIDACAO`), sem persistência. O plano define como implementar essa proteção.

---

## 3. Modelo de dados

### 3.1 Relacionamentos
```
Clientes 1 ──< Pedidos 1 ──< ItensPedido >── 1 Produtos
```

### 3.2 Tabelas

**Clientes**
| Coluna | Tipo | Regras |
|---|---|---|
| Id | int identity | PK **[D-6]** |
| Nome | nvarchar(200) | NOT NULL. **Não único** (homônimos permitidos) **[D-13]** |

**Produtos**
| Coluna | Tipo | Regras |
|---|---|---|
| Id | int identity | PK |
| Nome | nvarchar(200) | NOT NULL |
| Preco | decimal(18,2) | NOT NULL, CHECK `> 0` |
| Estoque | int | NOT NULL, CHECK `>= 0` |

**Pedidos**
| Coluna | Tipo | Regras |
|---|---|---|
| Id | int identity | PK |
| ClienteId | int | NOT NULL, FK → Clientes(Id) |
| DataCriacao | datetime2(0) | NOT NULL, UTC, definida pelo servidor |
| ValorTotal | decimal(18,2) | NOT NULL, = soma dos subtotais **[D-7]** |

**ItensPedido**
| Coluna | Tipo | Regras |
|---|---|---|
| Id | int identity | PK |
| PedidoId | int | NOT NULL, FK → Pedidos(Id) |
| ProdutoId | int | NOT NULL, FK → Produtos(Id) |
| Quantidade | int | NOT NULL, CHECK `> 0` |
| PrecoUnitario | decimal(18,2) | NOT NULL, CHECK `> 0`, cópia do preço no momento da compra |

Restrições: UNIQUE (`PedidoId`, `ProdutoId`) (reflete RN-09). Subtotal não é armazenado (derivado). FKs restritivas, sem exclusão em cascata de Produtos/Clientes com pedidos.

Índices exigidos pelos filtros/ordenação (definição exata no plano): `Pedidos(ClienteId, DataCriacao)`, `Pedidos(DataCriacao, Id)` e `ItensPedido(PedidoId)`.

Nomenclatura: o enunciado do teste usa `DataPedido`; esta especificação adota `DataCriacao` **[D-17]**. O README deve registrar essa correspondência ao apresentar a query corrigida.

### 3.3 Scripts SQL
- Em `db/` (ou caminho definido no plano): `01-schema.sql` (tabelas, FKs, CHECKs, índices) e `02-seed.sql` (carga inicial).
- Os scripts são a fonte do schema; o mapeamento EF Core deve ser compatível com eles. Reexecução não deve duplicar dados nem falhar (idempotentes).
- **Carga inicial mínima verificável:**
  - ≥ 7 clientes, incluindo **dois clientes distintos com o mesmo nome**.
  - ≥ 6 produtos, incluindo um com estoque baixo (ex.: 5) e um com estoque `0`.
  - Pedidos históricos suficientes para que ≥ 6 clientes tenham pedidos em um mesmo período (para que o corte em 5 seja observável), em pelo menos dois períodos distintos, e ao menos um empate de total entre clientes.
  - Pedidos semeados consistentes com RN-05 (itens, preços copiados e `ValorTotal` coerentes).

---

## 4. Contratos dos endpoints

Convenções: JSON, `camelCase`, datas/horas em ISO 8601 UTC, valores monetários como número. Erros no formato **Problem Details (RFC 7807)** com `errors` (mapa campo → mensagens, quando aplicável) e `codigo` estável **[D-2]**.

### 4.1 `POST /pedidos`

**Requisição**
```json
{
  "clienteId": 3,
  "itens": [
    { "produtoId": 7, "quantidade": 2 },
    { "produtoId": 9, "quantidade": 1 }
  ]
}
```

**201 Created** (produto 7 a 89.90 e produto 9 a 79.90)
```json
{
  "id": 101,
  "clienteId": 3,
  "dataCriacao": "2026-11-12T14:32:10Z",
  "valorTotal": 259.70,
  "itens": [
    { "produtoId": 7, "quantidade": 2, "precoUnitario": 89.90, "subtotal": 179.80 },
    { "produtoId": 9, "quantidade": 1, "precoUnitario": 79.90, "subtotal": 79.90 }
  ]
}
```
Se o corpo trouxer duas linhas do produto 7 (quantidades 2 e 3), a resposta contém **um** item do produto 7 com `quantidade: 5`.

**Códigos**
| HTTP | Quando | `codigo` |
|---|---|---|
| 201 | Pedido criado, estoque baixado | — |
| 400 | Requisição malformada: corpo ausente, JSON inválido ou tipos errados (ex.: `quantidade` `1.5` ou `"abc"`, `clienteId` texto) | `REQUISICAO_INVALIDA` |
| 422 | Requisição bem formada mas semanticamente inválida: `clienteId`/`produtoId` ausentes ou ≤ 0; `itens` vazio ou ausente; `quantidade` ausente ou ≤ 0 | `VALIDACAO` |
| 404 | Cliente inexistente | `CLIENTE_NAO_ENCONTRADO` |
| 404 | Produto inexistente | `PRODUTO_NAO_ENCONTRADO` |
| 409 | Estoque insuficiente (inclui perda de disputa concorrente) | `ESTOQUE_INSUFICIENTE` |
| 500 | Erro inesperado (sem vazar detalhes internos) | — |

Respostas `400`, `404`, `409` e `422` indicam rejeição sem persistência dessa requisição (RN-07). Uma resposta `5xx` ou uma falha de comunicação não garante ausência de gravação (ex.: a conexão pode cair depois do commit); o cliente deve conferir o estado antes de reenviar.

Em todos os casos de erro, `errors`/`produtos` seguem a etapa de validação que falhou (RN-11).

**422**
```json
{
  "type": "https://httpstatuses.com/422",
  "title": "Entidade não processável",
  "status": 422,
  "codigo": "VALIDACAO",
  "errors": {
    "itens[0].quantidade": ["A quantidade deve ser um inteiro maior que zero."]
  }
}
```

**404** (produtos inexistentes)
```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Produto não encontrado",
  "status": 404,
  "codigo": "PRODUTO_NAO_ENCONTRADO",
  "detail": "Um ou mais produtos informados não existem.",
  "produtos": [
    { "produtoId": 99, "mensagem": "Produto 99 não encontrado (itens[1])." }
  ]
}
```

**409**
```json
{
  "type": "https://httpstatuses.com/409",
  "title": "Estoque insuficiente",
  "status": 409,
  "codigo": "ESTOQUE_INSUFICIENTE",
  "detail": "Um ou mais produtos não possuem estoque suficiente.",
  "produtos": [
    { "produtoId": 7, "solicitado": 5, "disponivel": 3 }
  ]
}
```

Não há header `Location` na resposta, pois `GET /pedidos/{id}` está fora do escopo **[D-12]**.

### 4.2 `GET /pedidos`

**Query:** `clienteId` (int, opc.), `dataInicio` (yyyy-MM-dd, opc.), `dataFim` (yyyy-MM-dd, opc.), `pagina` (int ≥ 1, padrão 1), `tamanhoPagina` (1–100, padrão 20).

Exemplo: `GET /pedidos?clienteId=3&dataInicio=2026-11-01&dataFim=2026-11-12&pagina=1&tamanhoPagina=20`

**200 OK**
```json
{
  "registros": [
    {
      "id": 101,
      "clienteId": 3,
      "clienteNome": "Maria Silva",
      "dataCriacao": "2026-11-12T14:32:10Z",
      "valorTotal": 259.70,
      "itens": [
        { "produtoId": 7, "produtoNome": "Teclado", "quantidade": 2, "precoUnitario": 89.90, "subtotal": 179.80 },
        { "produtoId": 9, "produtoNome": "Mouse", "quantidade": 1, "precoUnitario": 79.90, "subtotal": 79.90 }
      ]
    }
  ],
  "pagina": 1,
  "tamanhoPagina": 20,
  "totalItens": 57,
  "totalPaginas": 3
}
```

| HTTP | Quando |
|---|---|
| 200 | Sucesso (inclusive lista vazia, `clienteId` inexistente, página além do fim) |
| 400 | Data em formato inválido; `dataInicio > dataFim`; `pagina < 1`; `tamanhoPagina` fora de 1–100; `clienteId` não numérico |

### 4.3 `GET /relatorios/top-clientes`

**Query (obrigatórios):** `dataInicio`, `dataFim` (yyyy-MM-dd, inclusivos).

Exemplo: `GET /relatorios/top-clientes?dataInicio=2026-10-01&dataFim=2026-10-31`

**200 OK** (dois clientes chamados "João Souza", com ids distintos)
```json
[
  { "clienteId": 3, "nome": "Maria Silva", "valorTotal": 1520.40 },
  { "clienteId": 5, "nome": "João Souza",  "valorTotal": 1310.00 },
  { "clienteId": 8, "nome": "João Souza",  "valorTotal": 990.50 },
  { "clienteId": 2, "nome": "Ana Lima",    "valorTotal": 450.00 },
  { "clienteId": 4, "nome": "Pedro Alves", "valorTotal": 450.00 }
]
```
Empate entre os clientes 2 e 4: desempate por `clienteId` crescente.

| HTTP | Quando |
|---|---|
| 200 | Sucesso (array com 0 a 5 elementos) |
| 400 | Parâmetro ausente, data inválida ou `dataInicio > dataFim` |

### 4.4 Endpoints auxiliares de leitura **[D-5]**
Somente leitura, sem filtros nem paginação, para os seletores do frontend:
- `GET /clientes` → `200 [{ "id": 3, "nome": "Maria Silva" }]`, ordenado por `nome`, `id`.
- `GET /produtos` → `200 [{ "id": 7, "nome": "Teclado", "preco": 89.90, "estoque": 12 }]`, ordenado por `nome`, `id`.

---

## 5. Frontend (comportamento esperado)
- **Listagem:** tabela de pedidos (id, cliente, data, valor total, itens), filtros por cliente, data inicial e data final, paginação com total, ordenação conforme RN-16, estados de carregando/vazio/erro.
- **Criação:** formulário com seleção de cliente e lista dinâmica de itens (produto + quantidade, adicionar/remover linha); impede envio com zero itens ou quantidade ≤ 0 (validação de conveniência — a API continua sendo a autoridade); exibe o total estimado a partir do preço do cadastro, informando que o valor final é o do servidor; mostra mensagens de erro da API (400/404/422/409, inclusive produtos sem estoque); após sucesso, **limpa os filtros, volta à primeira página e atualiza a listagem**, de modo que o novo pedido fique visível **[D-18]**.
- **Filtro de datas:** valida `dataInicio ≤ dataFim` antes de consultar.

---

## 6. Critérios de aceite verificáveis

Notação: **Dado / Quando / Então**. Os critérios de API e banco são cobertos por teste de integração contra SQL Server real, exceto CA-33 (ver CA-42). "Nada alterado" significa nenhuma alteração de negócio dessa requisição (pedido, itens, estoque), desconsiderando lacunas de `identity` e efeitos de outras requisições (RN-07).

### Criação de pedido
| ID | Critério |
|---|---|
| CA-01 | Dado cliente e produto válidos com estoque suficiente, quando `POST /pedidos` com quantidade 2, então `201`, `valorTotal = 2 × preço` e `Estoque` do produto reduzido em 2. |
| CA-02 | Quando `itens` é `[]` ou ausente, então `422` `VALIDACAO` e nenhum pedido/estoque alterado. |
| CA-03 | Quando `quantidade` é 0, −1 ou ausente, então `422` `VALIDACAO`; quando é `1.5` ou `"abc"` (tipo errado), então `400` `REQUISICAO_INVALIDA`; em ambos nada é alterado. |
| CA-03b | Quando o corpo é ausente ou JSON inválido, então `400` e nada alterado. |
| CA-04 | Quando `clienteId` não existe, então `404` `CLIENTE_NAO_ENCONTRADO` e nada alterado. |
| CA-05 | Quando algum `produtoId` não existe, então `404` `PRODUTO_NAO_ENCONTRADO` e nada alterado (inclusive os demais itens válidos). |
| CA-05b | Dois `produtoId` inexistentes no mesmo pedido → um único `404` listando **ambos**, com mensagens que identificam cada item. |
| CA-05c | **Ordem das etapas (RN-11):** cliente inexistente e produto sem estoque no mesmo pedido → `404` de cliente; produto inexistente e outro produto sem estoque → `404` de produto (não `409`); `quantidade` 0 em um item e cliente inexistente → `422` (etapa 1 prevalece). Em todos, nada alterado. |
| CA-05d | Vários erros na mesma etapa são agrupados: itens 0 e 2 com `quantidade` ≤ 0 → um `422` com `errors` para `itens[0].quantidade` e `itens[2].quantidade`. |
| CA-06 | Quando quantidade > estoque, então `409` `ESTOQUE_INSUFICIENTE`, com `solicitado`/`disponivel` de **todos** os produtos afetados, e nada alterado. |
| CA-07 | Quando quantidade == estoque, então `201` e estoque final `0`. |
| CA-08 | Dado produto com estoque 0, quando pedido com qualquer quantidade, então `409`. |
| CA-09 | Pedido com 2 itens onde o segundo não tem estoque → `409` e o estoque do **primeiro** produto permanece intacto (atomicidade). |
| CA-10 | Dado preço 89.90, quando o pedido é criado e depois o preço do produto é alterado diretamente no banco para 99.90, então o `PrecoUnitario` do item e o `ValorTotal` do pedido permanecem os originais. |
| CA-11 | Quando o corpo envia `precoUnitario` diferente do cadastro, então o valor enviado é ignorado e vale o do cadastro. |
| CA-12 | Produto repetido. Dado um pedido com **duas linhas do mesmo produto 7**: `{produtoId:7, quantidade:2}` e `{produtoId:7, quantidade:3}` (total pedido = 5). **(a)** Se o produto 7 tem estoque 4: `409`, pois 2+3 = 5 > 4, e nada é alterado (sem a soma, cada linha isolada — 2 ≤ 4 e 3 ≤ 4 — passaria e venderíamos mais do que existe). **(b)** Se tem estoque 5: `201`, o pedido tem **um único item** com `quantidade: 5` e o estoque final é 0. |
| CA-13 | Falha injetada após a baixa de estoque e antes do commit (ex.: erro forçado na gravação) → estoque e tabelas inalterados (teste de integração com falha provocada). |
| CA-14 | **Concorrência:** produto com estoque 10 e 20 requisições simultâneas de quantidade 1 → exatamente 10 `201`, 10 `409`, estoque final 0, soma das quantidades em itens = 10, nenhum `5xx`. |
| CA-15 | **Concorrência:** estoque 10 e duas requisições simultâneas de quantidade 6 → exatamente uma `201` e uma `409`; estoque final 4. |
| CA-16 | **Concorrência multi-produto:** pedidos simultâneos com os mesmos produtos em ordens diferentes (A,B e B,A) → nenhum `5xx`; estoque nunca negativo; soma vendida ≤ estoque inicial. |
| CA-17 | Preço `0.10`, quantidade 3 → `valorTotal` exatamente `0.30` (sem `0.30000000000000004`), verificado na API e no banco. |
| CA-17b | **Limite de quantidade:** duas linhas do mesmo produto cuja soma excede o valor máximo de `int` → `422` `VALIDACAO`, sem estouro nem persistência. |
| CA-17c | **Limite monetário:** quantidade × preço (subtotal) ou soma dos subtotais excedendo a precisão `decimal(18,2)` → `422` `VALIDACAO`, sem persistência. |

### Listagem
| ID | Critério |
|---|---|
| CA-18 | Sem filtros, retorna página 1 com até 20 registros, `totalItens` correto e ordem `DataCriacao desc, Id desc`. |
| CA-19 | `clienteId=X` retorna apenas pedidos do cliente X; `clienteId` inexistente → `200` com `registros: []`, `totalItens: 0`. |
| CA-20 | Pedido às `2026-11-12T23:59:59Z` aparece com `dataFim=2026-11-12`; pedido às `2026-11-13T00:00:00Z` não aparece. Pedido às `2026-11-01T00:00:00Z` aparece com `dataInicio=2026-11-01`. |
| CA-21 | Apenas `dataInicio` ou apenas `dataFim` funcionam como intervalos abertos. |
| CA-22 | `dataInicio > dataFim`, data `2026-13-40`, `pagina=0`, `tamanhoPagina=0` ou `101` → `400`. |
| CA-23 | Com vários pedidos de mesma `DataCriacao`: percorrer todas as páginas (tamanho 3) e concatenar == listagem completa em página única, sem duplicados nem omissões, com ordem idêntica entre execuções repetidas. |
| CA-24 | Página além da última → `200`, `registros: []`, `totalItens` e `totalPaginas` preservados. |
| CA-25 | Filtros combinados (`clienteId` + período) aplicam AND e `totalItens` reflete o resultado filtrado. |

### Top-clientes
| ID | Critério |
|---|---|
| CA-26 | Com 6+ clientes com pedidos no período, retorna exatamente 5, em ordem decrescente de total. |
| CA-27 | Dois clientes com o mesmo nome e ids distintos aparecem como **duas entradas**, com totais **não somados**. |
| CA-28 | Totais iguais → ordem por `clienteId` crescente, resultado idêntico em execuções repetidas. |
| CA-29 | Pedidos fora do período (inclusive 1 segundo após `dataFim` 23:59:59) não entram na soma. |
| CA-30 | Menos de 5 clientes com pedidos → retorna só os existentes; nenhum pedido → `[]` com `200`. |
| CA-31 | Parâmetro ausente, data inválida ou `dataInicio > dataFim` → `400`. |
| CA-32 | Totais exatos em decimal (soma conferida contra cálculo independente nos dados semeados). |

### Banco e execução
| ID | Critério |
|---|---|
| CA-33 | `docker compose up` em ambiente limpo sobe banco, API e frontend; schema e carga inicial aplicados sem passos manuais. Validado separadamente dos testes automatizados, por procedimento reproduzível registrado no README. |
| CA-34 | Reexecutar `01-schema.sql` e `02-seed.sql` não falha nem duplica dados. |
| CA-35 | Carga inicial atende os mínimos da seção 3.3 (conferível por consultas SQL). |
| CA-36 | Tentativas diretas de gravar `Estoque < 0`, `Quantidade ≤ 0`, `Preco ≤ 0` ou item duplicado no mesmo pedido são rejeitadas pelo banco. |
| CA-37 | Colunas monetárias são `decimal(18,2)` (verificável via metadados do schema). |

### Frontend
| ID | Critério |
|---|---|
| CA-38 | Listagem exibe pedidos, aplica cada filtro e navega entre páginas refletindo o total. |
| CA-39 | Não é possível enviar pedido sem itens ou com quantidade ≤ 0; erros 404/409/422 da API aparecem de forma legível, sem perder o preenchimento do formulário. |
| CA-40 | Após criar um pedido com filtros ativos (ex.: outro cliente ou período), os filtros são limpos, a listagem volta à página 1 e o pedido novo aparece no topo; o estoque atualizado é refletido ao reabrir o formulário. |

### Testes
| ID | Critério |
|---|---|
| CA-41 | Regras de negócio puras (cálculo de totais, consolidação de itens, validações) têm testes unitários xUnit, com NSubstitute para dependências. |
| CA-42 | Regras e endpoints (CA-01 a CA-32) e verificações de banco (CA-34 a CA-37) são automatizados em testes de integração contra SQL Server real (não in-memory/SQLite). A inicialização completa via Docker Compose (CA-33) é validada separadamente, conforme o README. |

---

## 7. Decisões tomadas

| # | Decisão | Resolução | Justificativa |
|---|---|---|---|
| D-1 | Produto repetido no pedido | Consolidar (somar quantidades) | Não é inválido em si; mantém UNIQUE (PedidoId, ProdutoId) e valida estoque pela soma. |
| D-2 | Códigos HTTP e erro | `400` malformada, `422` semanticamente inválida, `404` cliente/produto inexistente, `409` estoque; Problem Details + `codigo` | Cada código tem significado único; `codigo` evita depender de texto. |
| D-3 | Semântica do período | Datas sem horário, inclusivas, UTC | Evita que `dataFim` exclua o próprio dia. |
| D-4 | Período no relatório | Obrigatório | "No período" implica período definido. |
| D-5 | `GET /clientes` e `GET /produtos` | Incluídos, somente leitura | Necessários ao frontend para seletores. |
| D-6 | Tipo de id | `int identity` | Suficiente ao escopo. |
| D-7 | `ValorTotal` em Pedidos | Persistido | Fixa o total da venda; simplifica listagem e ranking. |
| D-8 | `clienteId` inexistente no filtro | `200` com lista vazia | É filtro, não recurso. |
| D-9 | Paginação | Padrão 20, máximo 100, página 1-based, além do fim → `200` vazio | Convencional; teto protege a API. |
| D-10 | Campos do ranking | `clienteId`, `nome`, `valorTotal` | Mínimo que distingue homônimos. |
| D-11 | Fuso horário | UTC | Evita ambiguidade. |
| D-12 | `GET /pedidos/{id}` / `Location` | Não incluir | Fora dos requisitos. |
| D-13 | Atributos de Cliente | Apenas `Id` e `Nome` | Não inventar requisito. |
| D-14 | Tela do top-clientes | Fora do escopo | Frontend pedido cita só listagem, filtros e criação. |
| D-15 | Validação do `POST /pedidos` | Em etapas, interrompendo na primeira com falhas e agrupando os erros dela | Mensagens mais descritivas e comportamento previsível. |
| D-16 | Garantias de atomicidade | Restritas à própria requisição antes do commit; `5xx`/falha de comunicação sem garantia | Outras requisições concorrentes alteram o banco, `identity` pode ter lacunas e a conexão pode cair após o commit. |
| D-17 | `DataPedido` vs `DataCriacao` | Mantido `DataCriacao`, com correspondência registrada no README | Escolha do modelo; evita ambiguidade para quem compara com o enunciado. |
| D-18 | Criação com filtros ativos | Limpar filtros, voltar à página 1 e atualizar | Garante que o pedido criado fique visível. |
