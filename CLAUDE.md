# Mini Gestão de Pedidos

Teste técnico: clientes, produtos e pedidos com criação validada, listagem paginada e ranking de clientes.
Fonte de verdade: [docs/spec.md](docs/spec.md) (o quê; numeração RN-xx e CA-xx) e [docs/plan.md](docs/plan.md) (como; etapas E0 a E22).
Em caso de divergência entre código e spec, a spec manda; mudanças de comportamento começam por ela.

## Arquitetura

Quatro projetos em `src/`, com a direção de dependências garantida por `ProjectReference`:

```
Domain → (nenhuma) ← Application ← Infrastructure
                         ↑               ↑
                         └──── Api ──────┘   (Api referencia Infrastructure só para a DI)
```

- **Domain:** entidades e regras (preço da venda, total, consolidação de itens, limites). Sem HTTP, EF ou SQL.
- **Application:** casos de uso, validação da etapa 1, `Resultado` único com lista de erros, duas portas (`IPedidoRepositorio`, `IPedidoConsultas`).
- **Infrastructure:** EF Core, consultas, transação e débito atômico de estoque no SQL Server.
- **Api:** controllers, contratos HTTP, Problem Details, configuração.

**Proibido** sem necessidade concreta documentada: MediatR, barramento de eventos, repositório genérico, Unit of Work próprio, CQRS formal, interfaces criadas por convenção, AutoMapper, FluentValidation.

## Regras de ouro

- O preço vem do cadastro do produto e é copiado para o item; o cliente da API nunca o informa.
- Estoque nunca fica negativo. O débito é um `UPDATE` condicional (`estoque >= @q`) em ordem crescente de `Id`; 0 linhas afetadas significa que perdeu a disputa.
- Pedido, itens e baixa de estoque na mesma transação.
- Dinheiro sempre `decimal` (`decimal(18,2)` no banco); nunca `float`/`double`.
- `DataCriacao` em UTC, truncada para segundos antes de gravar.
- Validação do `POST /pedidos` em etapas (RN-11): estrutura, cliente, produtos, estoque; para na primeira etapa com falhas.

## Banco

`database/` é a fonte do schema: `00-create-database.sql`, `01-schema.sql`, `02-seed.sql`. Idempotentes, sem `GO`. Sem migrations e sem `EnsureCreated`.

Banco: `gestao_pedidos`. Nomes no banco: **tabelas no singular** (`cliente`, `produto`, `pedido`, `item_pedido`) e **colunas em snake_case** (`cliente_id`, `data_criacao`, `valor_total`, `preco_unitario`); restrições e índices no mesmo padrão (`ck_produto_estoque`, `ix_pedido_data_criacao_id`). C# em PascalCase e JSON em camelCase; a tradução é explícita no mapeamento do EF e o SQL escrito à mão usa os nomes do banco.

## Comandos

```bash
dotnet build Pedidos.slnx
dotnet test Pedidos.slnx
dotnet test Pedidos.slnx --filter "CA=CA-14"      # testes de um critério (trait CA)
powershell -File scripts/verificar-referencias.ps1  # direção das dependências
docker compose up --build                           # a partir do E17
```

## Testes

- Integração **sempre contra SQL Server real** (Testcontainers); nunca in-memory nem SQLite.
- Todo teste de critério de aceite leva `[Trait("CA", "CA-xx")]`.
- Unitários: NSubstitute só nas duas portas de Application; `FakeTimeProvider` para datas.
- Frontend: sem testes automatizados; roteiro manual no README.

## Git

- Commits pequenos e semânticos, em português (skill `commit-semantico`), um por etapa do plano.
- **Sem `Co-Authored-By`** nem outra linha de coautoria nas mensagens.
- Commitar apenas quando o usuário pedir. Nunca reescrever histórico; correções entram como novos commits.

## Segurança

- Não ler `.env`. O `.env.example` não contém segredos.
- Qualquer MCP de banco é somente leitura e só aponta para o banco local de desenvolvimento.

## Definição de pronto de uma etapa

Build verde, testes da etapa passando, critérios de aceite citados (CA-xx) e commit seguindo o plano.
