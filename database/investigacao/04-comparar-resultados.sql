-- Compara RESULTADOS (não desempenho). Cada bloco imprime o que esperar; o que for medido vai para o docs/investigacao-query.md.

SET NOCOUNT ON;

-- Referência independente: soma dos itens por cliente em 2026, sem usar valor_total.
SELECT p.cliente_id, SUM(i.quantidade * i.preco_unitario) AS total
INTO #referencia
FROM pedido p
JOIN item_pedido i ON i.pedido_id = p.id
WHERE p.data_criacao >= '2026-01-01' AND p.data_criacao < '2027-01-01'
GROUP BY p.cliente_id;

-- Corrigida sem o TOP (lista completa), para comparar linha a linha.
SELECT t.cliente_id, t.total
INTO #corrigida
FROM (
    SELECT p.cliente_id, SUM(p.valor_total) AS total
    FROM pedido p
    WHERE p.data_criacao >= '2026-01-01' AND p.data_criacao < '2027-01-01'
    GROUP BY p.cliente_id
) t;

-- Versão original (traduzida), guardada para as comparações abaixo.
SELECT c.nome, SUM(i.quantidade * i.preco_unitario) AS total
INTO #original
FROM cliente c
LEFT JOIN pedido p ON p.cliente_id = c.id
LEFT JOIN item_pedido i ON i.pedido_id = p.id
WHERE YEAR(p.data_criacao) = 2026
GROUP BY c.nome;

PRINT '1) valor_total diverge da soma dos itens? ESPERADO: 0';
SELECT COUNT(*) AS pedidos_divergentes
FROM pedido p
JOIN (SELECT pedido_id, SUM(quantidade * preco_unitario) AS total FROM item_pedido GROUP BY pedido_id) s ON s.pedido_id = p.id
WHERE p.valor_total <> s.total;

PRINT '2) corrigida EXCEPT referencia. ESPERADO: 0 linhas';
SELECT cliente_id, total FROM #corrigida EXCEPT SELECT cliente_id, total FROM #referencia;

PRINT '3) referencia EXCEPT corrigida. ESPERADO: 0 linhas';
SELECT cliente_id, total FROM #referencia EXCEPT SELECT cliente_id, total FROM #corrigida;

PRINT '4) Linhas: clientes com pedido em 2026 (corrigida) x linhas da original (agrupada por NOME)';
SELECT (SELECT COUNT(*) FROM #corrigida) AS clientes_com_pedido_2026,
       (SELECT COUNT(*) FROM #original) AS linhas_original,
       (SELECT COUNT(DISTINCT nome) FROM cliente WHERE id IN (SELECT cliente_id FROM #corrigida)) AS nomes_distintos_entre_eles;

PRINT '5) Total geral: original x corrigida. ESPERADO: iguais (a original so perde a separacao dos homonimos)';
SELECT (SELECT SUM(total) FROM #original) AS soma_original, (SELECT SUM(total) FROM #corrigida) AS soma_corrigida;

PRINT '6) Nomes fundidos na original (homonimos com pedido em 2026)';
SELECT COUNT(*) AS nomes_fundidos
FROM (SELECT c.nome FROM cliente c WHERE c.id IN (SELECT cliente_id FROM #corrigida)
      GROUP BY c.nome HAVING COUNT(*) > 1) x;

PRINT '7) Ordem: 5 primeiras linhas da ORIGINAL (ORDER BY total, ascendente = os MENORES)';
SELECT TOP (5) nome, total FROM #original ORDER BY total;

PRINT '8) Ranking correto (corrigida, TOP 5)';
SELECT TOP (5) c.id, c.nome, t.total FROM #corrigida t JOIN cliente c ON c.id = t.cliente_id ORDER BY t.total DESC, c.id;

PRINT '9) Clientes sem nenhum pedido que a original NAO mostra (WHERE em coluna de pedido anula o LEFT JOIN)';
SELECT COUNT(*) AS clientes_sem_pedido FROM cliente c WHERE NOT EXISTS (SELECT 1 FROM pedido p WHERE p.cliente_id = c.id);

DROP TABLE #referencia, #corrigida, #original;
