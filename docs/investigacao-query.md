# Investigação da query do ranking (Parte 4 do enunciado)

**Status: análise por leitura concluída; medições pendentes.** As seções 1 a 3 vêm da leitura do SQL. A seção 5 (evidências) só recebe números copiados da saída bruta guardada em `database/investigacao/resultados/`. Nada ali é conclusão até estar preenchido com a medição anexada.

## 1. A query do enunciado e o que ela pretende

O enunciado pede o ranking dos clientes que mais compraram em 2026 e informa que a query está lenta e com resultado incorreto:

```sql
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total
```

Tradução para o schema do projeto (`database/investigacao/02-query-original.sql`): `Clientes` → `cliente`, `Pedidos` → `pedido`, `ItensPedido` → `item_pedido`, `DataPedido` → `data_criacao` (D-17).

## 2. Defeitos de resultado (por leitura)

| # | Defeito | Efeito |
|---|---|---|
| 1 | `WHERE YEAR(p.DataPedido) = 2026` em coluna de `Pedidos` com `LEFT JOIN` | O `WHERE` descarta as linhas com `NULL` de `p`, então o `LEFT JOIN` vira `INNER JOIN` na prática. Clientes sem pedido nunca aparecem, ao contrário do que o `LEFT JOIN` sugere |
| 2 | `GROUP BY c.Nome` | Homônimos viram uma linha só, com os totais somados (viola a RN-19) |
| 3 | `ORDER BY Total` sem `DESC` | Ordem ascendente: devolve os que menos compraram primeiro |
| 4 | Sem desempate | Empate de total sai em ordem instável entre execuções |
| 5 | Sem limite | Devolve todos os clientes; a spec pede os 5 maiores (RN-18) |
| 6 | `SUM` depois de `LEFT JOIN` | Se o `LEFT JOIN` valesse, quem não tem item ficaria com `Total = NULL`, que ordena antes dos outros |

## 3. Defeitos de desempenho (por leitura)

- **`YEAR(coluna)`**: função aplicada à coluna filtrada deixa o predicado *não sargable*; o banco não consegue usar `Seek` num índice por data e tende a varrer todos os pedidos. A forma correta é a faixa `>= '2026-01-01' AND < '2027-01-01'`.
- **Agrega depois de juntar**: o `GROUP BY` roda sobre pedidos já multiplicados pelos itens. Agregar antes, só em `pedido`, trabalha com uma linha por pedido, e a junção com `cliente` acontece sobre o resultado agregado.
- **Sem índice de apoio** para filtro por data e agrupamento por cliente. Os índices do projeto (`ix_pedido_data_criacao_id` e `ix_pedido_cliente_id_data_criacao_id`, ambos com `INCLUDE`) foram pensados para isso.

## 4. Query corrigida

`database/investigacao/03-query-corrigida.sql`, equivalente ao endpoint `GET /api/relatorios/top-clientes`:

```sql
SELECT TOP (5) c.id, c.nome, t.total
FROM (
    SELECT p.cliente_id, SUM(p.valor_total) AS total
    FROM pedido p
    WHERE p.data_criacao >= '2026-01-01' AND p.data_criacao < '2027-01-01'
    GROUP BY p.cliente_id
) t
JOIN cliente c ON c.id = t.cliente_id
ORDER BY t.total DESC, c.id ASC;
```

Usa `valor_total` (soma dos itens, RN-05) em vez de juntar `item_pedido`. A equivalência é provada no script 04, contra uma soma independente dos itens.

## 5. Hipóteses e evidências

### Hipóteses (a confirmar ou refutar pelas medições)

| # | Hipótese | Como testar |
|---|---|---|
| H1 | `YEAR()` impede o `Seek` e causa varredura de `pedido` | Plano do cenário C (Scan) e leituras do D (só a faixa de datas) |
| H2 | Trocar `YEAR()` pela faixa, sozinho, reduz leituras lógicas | Comparar C x D |
| H3 | Agregar antes de juntar reduz leituras de `item_pedido` (a corrigida nem lê a tabela de itens) | Comparar D x E |
| H4 | Os índices de apoio reduzem as leituras da corrigida | Comparar B x E |
| H5 | A original devolve linhas a menos que `clientes com pedido em 2026` (homônimos fundidos) e mostra os menores primeiro | Script 04, itens 4, 6 e 7 |
| H6 | `valor_total` é igual à soma dos itens em todos os pedidos | Script 04, itens 1 a 3 |

### Evidências (a preencher com a saída anexada)

| # | Resultado medido | Arquivo com a saída bruta |
|---|---|---|
| H1 | _pendente_ | `resultados/` |
| H2 | _pendente_ | `resultados/` |
| H3 | _pendente_ | `resultados/` |
| H4 | _pendente_ | `resultados/` |
| H5 | _pendente_ | `resultados/` |
| H6 | _pendente_ | `resultados/` |

Tabela de cenários (preencher a partir de `05-medir.sql`, mediana de 3 execuções com cache aquecido):

| Cenário | Índices de apoio | Query | Leituras lógicas (`pedido` / `item_pedido`) | Tempo de CPU | Tempo total | Operador em `pedido` |
|---|---|---|---|---|---|---|
| A | não | original | _pendente_ | _pendente_ | _pendente_ | _pendente_ |
| B | não | corrigida | _pendente_ | _pendente_ | _pendente_ | _pendente_ |
| C | sim | original | _pendente_ | _pendente_ | _pendente_ | _pendente_ |
| D | sim | original com faixa de datas | _pendente_ | _pendente_ | _pendente_ | _pendente_ |
| E | sim | corrigida | _pendente_ | _pendente_ | _pendente_ | _pendente_ |

## 6. Roteiro de execução (ambiente de desenvolvimento)

Pré-requisito: `docker compose up -d db` já rodando, só em desenvolvimento (nunca no ambiente de produção). A base de volume é separada (`gestao_pedidos_volume`) e não interfere em `gestao_pedidos`.

```bash
# 1) copia a pasta database para dentro do container do banco
docker compose cp database db:/tmp/database
```

```bash
# 2) cria a base de volume
docker compose exec db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -b -I -f 65001 -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d master -i /tmp/database/investigacao/00-criar-base-volume.sql'
```

```bash
# 3) cria o schema do projeto nela
docker compose exec db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -b -I -f 65001 -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d gestao_pedidos_volume -i /tmp/database/01-schema.sql'
```

```bash
# 4) gera o volume (alguns minutos); a saída final mostra as contagens
docker compose exec db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -b -I -f 65001 -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d gestao_pedidos_volume -i /tmp/database/investigacao/01-gerar-volume.sql'
```

```bash
# 5) compara resultados (script 04) e guarda a saída
docker compose exec db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -b -I -f 65001 -W -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d gestao_pedidos_volume -i /tmp/database/investigacao/04-comparar-resultados.sql -o /tmp/resultado-04.txt'
```

```bash
# 6) mede leituras e tempo (script 05)
docker compose exec db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -b -I -f 65001 -W -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d gestao_pedidos_volume -i /tmp/database/investigacao/05-medir.sql -o /tmp/resultado-05.txt'
```

```bash
# 7) planos reais em texto (script 06)
docker compose exec db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -b -I -f 65001 -W -w 500 -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d gestao_pedidos_volume -i /tmp/database/investigacao/06-planos.sql -o /tmp/resultado-06.txt'
```

```bash
# 8) traz as saídas brutas para o repositório
docker compose cp db:/tmp/resultado-04.txt database/investigacao/resultados/resultado-04.txt
docker compose cp db:/tmp/resultado-05.txt database/investigacao/resultados/resultado-05.txt
docker compose cp db:/tmp/resultado-06.txt database/investigacao/resultados/resultado-06.txt
```

Para o plano gráfico, abra o SSMS ou o Azure Data Studio na base `gestao_pedidos_volume` e rode as queries com "Include Actual Execution Plan".

Cuidado: os scripts 05 e 06 desabilitam e reconstroem os dois índices de apoio de `pedido` **só na base de volume**. Se interromper no meio, rode `ALTER INDEX ... REBUILD` neles.

## 7. Como ler os resultados

- **Leituras lógicas** (`logical reads` em `SET STATISTICS IO`): é a métrica mais estável; tempo varia com cache e máquina.
- **Scan x Seek** no plano: `Clustered Index Scan` ou `Index Scan` sobre `pedido` na original é a evidência para H1.
- **Linhas estimadas x reais**: grande diferença indica estatística ruim ou predicado que o otimizador não consegue estimar (o caso típico de `YEAR()`).
- Uma execução a frio (`DBCC DROPCLEANBUFFERS`) é opcional e só vale no banco local de desenvolvimento.

## 8. Decisão sobre os índices

Os índices de `pedido` são hipóteses de projeto (plano, seção 2.7). Depois de preenchidas as evidências:

- se o cenário E não for melhor que B, o índice não se paga e deve ser reavaliado;
- se algum índice não for usado por nenhuma consulta do sistema, removê-lo reduz o custo de cada `INSERT` de pedido;
- qualquer mudança entra em `database/01-schema.sql` em commit à parte, citando esta investigação.
