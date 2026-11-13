-- Mede leituras lógicas e tempo (SET STATISTICS IO/TIME) em cinco cenários, cada um rodado 3 vezes (cache aquecido).
-- Executar conectado a gestao_pedidos_volume. Guarde a saída inteira em database/investigacao/resultados/.
-- Os resultados das consultas vão para tabela temporária (SELECT INTO): só interessam as estatísticas.
-- ALTER INDEX ... DISABLE/REBUILD só mexe nos dois índices de apoio de pedido; as chaves primárias continuam.
-- Atenção: o REBUILD final recria os índices; não deixe a base sem eles se for usá-la depois.

SET NOCOUNT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

DECLARE @i int;

PRINT '=== A. SEM índices de apoio: ORIGINAL ===';
ALTER INDEX ix_pedido_data_criacao_id ON dbo.pedido DISABLE;
ALTER INDEX ix_pedido_cliente_id_data_criacao_id ON dbo.pedido DISABLE;
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT c.nome, SUM(i.quantidade * i.preco_unitario) AS total INTO #r_a
    FROM cliente c LEFT JOIN pedido p ON p.cliente_id = c.id LEFT JOIN item_pedido i ON i.pedido_id = p.id
    WHERE YEAR(p.data_criacao) = 2026 GROUP BY c.nome ORDER BY total;
    DROP TABLE #r_a; SET @i += 1;
END

PRINT '=== B. SEM índices de apoio: CORRIGIDA ===';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT TOP (5) c.id, c.nome, t.total INTO #r_b
    FROM (SELECT p.cliente_id, SUM(p.valor_total) AS total FROM pedido p
          WHERE p.data_criacao >= '2026-01-01' AND p.data_criacao < '2027-01-01' GROUP BY p.cliente_id) t
    JOIN cliente c ON c.id = t.cliente_id ORDER BY t.total DESC, c.id ASC;
    DROP TABLE #r_b; SET @i += 1;
END

PRINT '=== Recriando os índices de apoio ===';
ALTER INDEX ix_pedido_data_criacao_id ON dbo.pedido REBUILD;
ALTER INDEX ix_pedido_cliente_id_data_criacao_id ON dbo.pedido REBUILD;

PRINT '=== C. COM índices: ORIGINAL ===';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT c.nome, SUM(i.quantidade * i.preco_unitario) AS total INTO #r_c
    FROM cliente c LEFT JOIN pedido p ON p.cliente_id = c.id LEFT JOIN item_pedido i ON i.pedido_id = p.id
    WHERE YEAR(p.data_criacao) = 2026 GROUP BY c.nome ORDER BY total;
    DROP TABLE #r_c; SET @i += 1;
END

PRINT '=== D. COM índices: ORIGINAL só com a faixa de datas no lugar de YEAR() (isola o efeito do predicado) ===';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT c.nome, SUM(i.quantidade * i.preco_unitario) AS total INTO #r_d
    FROM cliente c LEFT JOIN pedido p ON p.cliente_id = c.id LEFT JOIN item_pedido i ON i.pedido_id = p.id
    WHERE p.data_criacao >= '2026-01-01' AND p.data_criacao < '2027-01-01' GROUP BY c.nome ORDER BY total;
    DROP TABLE #r_d; SET @i += 1;
END

PRINT '=== E. COM índices: CORRIGIDA ===';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT TOP (5) c.id, c.nome, t.total INTO #r_e
    FROM (SELECT p.cliente_id, SUM(p.valor_total) AS total FROM pedido p
          WHERE p.data_criacao >= '2026-01-01' AND p.data_criacao < '2027-01-01' GROUP BY p.cliente_id) t
    JOIN cliente c ON c.id = t.cliente_id ORDER BY t.total DESC, c.id ASC;
    DROP TABLE #r_e; SET @i += 1;
END

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
