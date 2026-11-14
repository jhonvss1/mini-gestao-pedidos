---
name: criar-endpoint
description: Cria um novo endpoint da API seguindo o padrão deste projeto (spec primeiro, Application, Infrastructure, Api, testes com trait CA e documentação). Use quando o usuário pedir um endpoint novo, uma consulta nova ou uma nova operação sobre pedidos, clientes ou produtos.
---

# Criar um novo endpoint

Siga a ordem abaixo. Cada passo termina com o build verde. Leia antes `CLAUDE.md` (arquitetura, pastas, regras de ouro) e use endpoints existentes como modelo: `ListarCatalogo` e `GET /api/clientes` para uma leitura simples, `ListarPedidos` para leitura com filtros e paginação, `CriarPedido` para um comando com validação em etapas.

## 0. Entenda o pedido
- É leitura (consulta) ou escrita (comando)? Muda dados? Precisa de transação?
- Quais erros pode ter? Escolha o status: `400` malformado, `422` semanticamente inválido, `404` inexistente, `409` conflito de estoque ou concorrência.
- Se faltar informação que muda o comportamento, pergunte antes de implementar.

## 1. Spec primeiro (docs/spec.md)
A spec manda: mudança de comportamento começa por ela.
1. Descreva o endpoint na seção 4 (rota **relativa a `/api`**, parâmetros, corpo, respostas e códigos).
2. Registre as regras novas como `RN-xx` e os critérios de aceite como `CA-xx` (próximo número livre, em sequência).
3. Se houver decisão de design, acrescente uma linha `D-xx` na tabela de decisões.

## 2. Application (`src/Pedidos.Application`)
- **Entrada:** `Comandos/` (escrita) ou `Consultas/` (leitura), como `record`.
- **Saída de leitura:** `Modelos/`, como `record`.
- **Caso de uso:** classe concreta em `CasosDeUso/`, com construtor primário, devolvendo `Resultado<T>` (`Comum/`). Não crie interface para o caso de uso.
- **Validação:** em `Validacao/`, agregando todos os erros da etapa. Mensagens em português, centralizadas em `Comum/Mensagens.cs`; códigos em `Comum/CodigosErro.cs`.
- **Acesso a dados:** acrescente um método na porta existente (`Interfaces/IPedidoConsultas` para leitura, `IPedidoRepositorio` para escrita). Não crie interface nova nem repositório genérico.

## 3. Infrastructure (`src/Pedidos.Infrastructure`)
- Implemente o método da porta em `Persistencia/Consultas/` ou `Persistencia/Repositorios/`.
- Leitura: `AsNoTracking`, projeção para o read model, ordenação estável e determinística.
- Escrita: mesma transação do pedido; débito de estoque só por `UPDATE` condicional em ordem crescente de id (veja `PedidoRepositorio`).
- Dinheiro sempre `decimal`; datas em UTC. Se criar tabela ou coluna, altere `database/01-schema.sql` (idempotente, sem `GO`) e a `IEntityTypeConfiguration` em `Persistencia/Configuracoes/`.

## 4. Api (`src/Pedidos.Api`)
- Controller fino em `Controllers/` (`[ApiController]`, rota sob `api/...`) que só traduz: chama o caso de uso e converte o `Resultado` com `RespostasDeErro.DeResultado`.
- Contratos em `Contratos/Requisicoes/` e `Contratos/Respostas/` (um tipo por arquivo). Campos de requisição anuláveis: ausente é 422, tipo errado é 400.
- Registre o caso de uso em `Program.cs` (`AddScoped`). Registros de infraestrutura ficam em `Infrastructure/DependencyInjection.cs`.

## 5. Testes
- **Unitários** (`tests/Pedidos.UnitTests`): regras e orquestração. NSubstitute só nas duas portas; `FakeTimeProvider` para datas.
- **Integração** (`tests/Pedidos.IntegrationTests/Api`): SQL Server real via Testcontainers, usando `BancoDeTeste` e `FabricaApi`. Cubra o caminho feliz e cada erro do passo 1.
- Todo teste de critério leva `[Trait("CA", "CA-xx")]`. Nomes em PascalCase, objetivos, sem underscore.
- Rode: `dotnet test Pedidos.slnx --filter "CA=CA-xx"` e depois a suíte inteira. Se a API local estiver rodando (ela trava as DLLs), use `--artifacts-path .artifacts`.

## 6. Frontend e documentação
- Se o endpoint alimenta uma tela: serviço em `frontend/src/app/core/api`, modelos em `core/modelos`, tela em `features/`. O frontend não tem testes automatizados: acrescente o passo ao roteiro manual do README.
- README: tabela de endpoints e, se for o caso, o roteiro manual.

## 7. Finalização
- `dotnet build Pedidos.slnx` e `powershell -File scripts/verificar-referencias.ps1` verdes.
- Commits pequenos e semânticos, em português, **sem `Co-Authored-By`**: um para a spec, um por camada, um para os testes.
- Relate ao usuário o que foi decidido por conta própria.
