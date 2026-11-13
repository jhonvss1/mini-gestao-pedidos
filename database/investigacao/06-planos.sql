-- Plano de execução REAL em texto (SET STATISTICS PROFILE): operadores, linhas estimadas x reais, Scan x Seek.
-- Executar conectado a gestao_pedidos_volume, com -W -w 500 para a saída ficar legível. Uma vez por cenário.
-- Para o plano gráfico (.sqlplan), use o SSMS ou o Azure Data Studio com "Include Actual Execution Plan".
-- Coluna a observar em cada linha do plano: StmtText (operador), EstimateRows x Rows (reais), TotalSubtreeCost.

SET NOCOUNT ON;
SET STATISTICS PROFILE ON;

PRINT '=== A. SEM índices: ORIGINAL ===';
ALTER INDEX ix_pedido_data_criacao_id ON dbo.pedido DISABLE;
ALTER INDEX ix_pedido_cliente_id_data_criacao_id ON dbo.pedido DISABLE;
SELECT c.nome, SUM(i.quantidade * i.preco_unitario) AS total INTO #r_a
FROM cliente c LEFT JOIN pedido p ON p.cliente_id = c.id LEFT JOIN item_pedido i ON i.pedido_id = p.id
WHERE YEAR(p.data_criacao) = 2026 GROUP BY c.nome ORDER BY total;
DROP TABLE #r_a;

PRINT '=== B. SEM índices: CORRIGIDA ===';
SELECT TOP (5) c.id, c.nome, t.total INTO #r_b
FROM (SELECT p.cliente_id, SUM(p.valor_total) AS total FROM pedido p
      WHERE p.data_criacao >= '2026-01-01' AND p.data_criacao < '2027-01-01' GROUP BY p.cliente_id) t
JOIN cliente c ON c.id = t.cliente_id ORDER BY t.total DESC, c.id ASC;
DROP TABLE #r_b;

ALTER INDEX ix_pedido_data_criacao_id ON dbo.pedido REBUILD;
ALTER INDEX ix_pedido_cliente_id_data_criacao_id ON dbo.pedido REBUILD;

PRINT '=== C. COM índices: ORIGINAL ===';
SELECT c.nome, SUM(i.quantidade * i.preco_unitario) AS total INTO #r_c
FROM cliente c LEFT JOIN pedido p ON p.cliente_id = c.id LEFT JOIN item_pedido i ON i.pedido_id = p.id
WHERE YEAR(p.data_criacao) = 2026 GROUP BY c.nome ORDER BY total;
DROP TABLE #r_c;

PRINT '=== E. COM índices: CORRIGIDA ===';
SELECT TOP (5) c.id, c.nome, t.total INTO #r_e
FROM (SELECT p.cliente_id, SUM(p.valor_total) AS total FROM pedido p
      WHERE p.data_criacao >= '2026-01-01' AND p.data_criacao < '2027-01-01' GROUP BY p.cliente_id) t
JOIN cliente c ON c.id = t.cliente_id ORDER BY t.total DESC, c.id ASC;
DROP TABLE #r_e;

SET STATISTICS PROFILE OFF;
