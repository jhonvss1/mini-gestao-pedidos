# Investigação da query do ranking (Parte 4 do enunciado)

**Status: análise por leitura e medições concluídas (ranking e listagem)** (2026-11-14, uma execução em SQL Server 2022 CU28 em container, 4 vCPU). As seções 1 a 3 vêm da leitura do SQL; a seção 5 só traz números copiados da saída bruta guardada em `database/investigacao/resultados/`. Limites da medição estão na seção 5.1.

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

### Hipóteses e veredito

| # | Hipótese | Veredito | Evidência |
|---|---|---|---|
| H1 | `YEAR()` impede o `Seek` e causa varredura de `pedido` | **Confirmada** | Planos A e C: `Clustered Index Scan` e `Index Scan` sobre `pedido`, sem `Seek` (`resultado-06.txt`). A varredura lê o índice estreito (1895 leituras) em vez do clustered (2084), ganho pequeno |
| H2 | Trocar `YEAR()` pela faixa, sozinho, reduz leituras lógicas | **Parcial** | C x D: `pedido` cai de 1895 para 906 leituras (-52%), mas o total quase não muda (8389 para 7400, -12%) porque `item_pedido` segue em 6418. CPU 348 para 283 ms; tempo total não melhorou (122 para 138 ms, dentro do ruído de 3 execuções) |
| H3 | Agregar antes de juntar elimina as leituras de `item_pedido` | **Confirmada** | D x E: `item_pedido` de 6418 para 0 leituras; CPU 283 para 32 ms; total 7400 para 920 leituras |
| H4 | Os índices de apoio reduzem as leituras da corrigida | **Confirmada** | B x E: `pedido` de 1986 para 906 leituras (-54%), CPU 54 para 32 ms, `Index Seek` em `ix_pedido_data_criacao_id`. `ix_pedido_cliente_id_data_criacao_id` **não** é usado pelo ranking; o benefício dele está na listagem (H7) |
| H7 | `ix_pedido_cliente_id_data_criacao_id` reduz as leituras de `GET /api/pedidos` filtrado por cliente | **Confirmada** | `resultado-07.txt`, cliente 3634: contagem de 1806 para 3 leituras (CPU ~22 para ~0 ms); página de 317 para 3; cliente + período de 80 para 3 em cada consulta. Sem filtro de cliente não há diferença |
| H5 | A original devolve linhas a menos e mostra os menores primeiro | **Confirmada** | Script 04: 4500 clientes com pedido em 2026, 4000 linhas na original (500 nomes fundidos); 500 clientes sem pedido não aparecem; as 5 primeiras linhas são as de menor total (2439,80 a 2516,40) |
| H6 | `valor_total` é igual à soma dos itens em todos os pedidos | **Confirmada** | Script 04: 0 pedidos divergentes; `EXCEPT` nos dois sentidos devolve 0 linhas; soma geral 431.910.813,80 nas duas queries |

Saídas brutas: [`resultado-04.txt`](../database/investigacao/resultados/resultado-04.txt) (resultados), [`resultado-05.txt`](../database/investigacao/resultados/resultado-05.txt) (leituras e tempo) e [`resultado-06.txt`](../database/investigacao/resultados/resultado-06.txt) (planos).

### Medições (mediana de 3 execuções, cache aquecido, 500 mil pedidos e 1,5 milhão de itens)

| Cenário | Índices de apoio | Query | Leituras lógicas `pedido` / `item_pedido` | CPU | Tempo total | Operador em `pedido` |
|---|---|---|---|---|---|---|
| A | não | original | 2084 / 6418 | 352 ms | 117 ms | Clustered Index Scan |
| B | não | corrigida | 1986 / 0 | 54 ms | 57 ms | Clustered Index Scan (filtro de datas) |
| C | sim | original | 1895 / 6418 | 348 ms | 122 ms | Index Scan em `ix_pedido_cliente_id_data_criacao_id` (sem Seek) |
| D | sim | original com faixa de datas | 906 / 6418 | 283 ms | 138 ms | não capturado (o script 06 não gera o plano do cenário D) |
| E | sim | corrigida | 906 / 0 | 32 ms | 32 ms | Index Seek em `ix_pedido_data_criacao_id` |

Original sem índices (A) contra corrigida com índices (E): de 8578 para 920 leituras lógicas no total (-89%), CPU de 352 para 32 ms e tempo de 117 para 32 ms.

### 5.1 Limites da medição

- Uma única máquina (container, 4 vCPU), cache aquecido, três execuções por cenário: o tempo oscila alguns ms (no cenário D o tempo total ficou acima do C só por ruído); as **leituras lógicas** são a métrica confiável.
- Dados sintéticos e determinísticos: 5000 clientes (4500 compram), 200 produtos, pedidos entre 2025-01-01 e 2026-12-31 (249.997 em 2026). A proporção entre os cenários depende dessa distribuição.
- As consultas originais rodam em plano paralelo (4 threads); o tempo da corrigida é de um plano serial.
- Não houve execução a frio (`DROP CLEANBUFFERS`).
- A medição da listagem (H7) usa um único cliente (3634) e uma única página (a primeira); páginas profundas e o custo extra de manter o índice em cada `INSERT` de pedido **não foram medidos**.

### Defeito encontrado no gerador de volume

Na primeira execução, o gerador distribuía a data com `CHECKSUM(n)`, que para inteiros sequenciais concentra os valores no começo do intervalo: os 500 mil pedidos caíram entre 2025-01-01 e 2025-01-06 e **nenhum pedido era de 2026**, o que tornaria qualquer medição sem sentido (a query filtra 2026). Foi trocado por um hash multiplicativo determinístico em `01-gerar-volume.sql`, e a base foi regenerada antes de todas as medições acima.

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

Atenção ao copiar a pasta: se `/tmp/database` já existir no container, `docker compose cp database db:/tmp/database` cria `/tmp/database/database` e os scripts antigos continuam sendo executados. Em uma nova rodada, copie para um caminho novo.

Cuidado: os scripts 05 e 06 desabilitam e reconstroem os dois índices de apoio de `pedido` **só na base de volume**. Se interromper no meio, rode `ALTER INDEX ... REBUILD` neles.

## 7. Como ler os resultados

- **Leituras lógicas** (`logical reads` em `SET STATISTICS IO`): é a métrica mais estável; tempo varia com cache e máquina.
- **Scan x Seek** no plano: `Clustered Index Scan` ou `Index Scan` sobre `pedido` na original é a evidência para H1.
- **Linhas estimadas x reais**: grande diferença indica estatística ruim ou predicado que o otimizador não consegue estimar (o caso típico de `YEAR()`).
- Uma execução a frio (`DBCC DROPCLEANBUFFERS`) é opcional e só vale no banco local de desenvolvimento.

## 8. Decisão sobre os índices

Os índices de `pedido` nasceram como hipóteses de projeto (plano, seção 2.7). Com as evidências:

- **`ix_pedido_data_criacao_id`: mantido.** O cenário E é melhor que o B (906 contra 1986 leituras em `pedido`; CPU 32 contra 54 ms) e o plano usa `Index Seek`.
- **`ix_pedido_cliente_id_data_criacao_id`: mantido.** O ranking não o usa, mas a listagem filtrada por cliente depende dele (H7: de 1806 para 3 leituras na contagem). O custo dele em cada `INSERT` não foi medido.
- qualquer mudança entra em `database/01-schema.sql` em commit à parte, citando esta investigação.
