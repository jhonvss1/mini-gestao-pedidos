-- Mede a listagem (GET /api/pedidos) com e sem ix_pedido_cliente_id_data_criacao_id. Só na base de volume.
-- Reproduz as duas consultas de PedidoConsultas.ListarAsync (contagem e página de cabeçalhos), 3 execuções por cenário.
-- ix_pedido_data_criacao_id fica sempre ativo; só o índice por cliente é desabilitado e depois reconstruído.

SET NOCOUNT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

DECLARE @i int, @cli int = 3634, @n int;

PRINT '=== L1. SEM índice por cliente ===';
ALTER INDEX ix_pedido_cliente_id_data_criacao_id ON dbo.pedido DISABLE;

PRINT '--- L1a. filtro por cliente (contagem + página 1)';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT @n = COUNT(*) FROM pedido p WHERE p.cliente_id = @cli;
    SELECT p.id, p.cliente_id, c.nome, p.data_criacao, p.valor_total INTO #t1
    FROM pedido p JOIN cliente c ON c.id = p.cliente_id
    WHERE p.cliente_id = @cli ORDER BY p.data_criacao DESC, p.id DESC OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
    DROP TABLE #t1; SET @i += 1;
END

PRINT '--- L1b. cliente + período 2026-03-01..2026-03-31 (contagem + página 1)';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT @n = COUNT(*) FROM pedido p WHERE p.cliente_id = @cli AND p.data_criacao >= '2026-03-01' AND p.data_criacao < '2026-04-01';
    SELECT p.id, p.cliente_id, c.nome, p.data_criacao, p.valor_total INTO #t2
    FROM pedido p JOIN cliente c ON c.id = p.cliente_id
    WHERE p.cliente_id = @cli AND p.data_criacao >= '2026-03-01' AND p.data_criacao < '2026-04-01'
    ORDER BY p.data_criacao DESC, p.id DESC OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
    DROP TABLE #t2; SET @i += 1;
END

PRINT '--- L1c. sem filtros (contagem + página 1)';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT @n = COUNT(*) FROM pedido p;
    SELECT p.id, p.cliente_id, c.nome, p.data_criacao, p.valor_total INTO #t3
    FROM pedido p JOIN cliente c ON c.id = p.cliente_id
    ORDER BY p.data_criacao DESC, p.id DESC OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
    DROP TABLE #t3; SET @i += 1;
END

PRINT '=== Recriando o índice por cliente ===';
ALTER INDEX ix_pedido_cliente_id_data_criacao_id ON dbo.pedido REBUILD;

PRINT '=== L2. COM índice por cliente ===';
PRINT '--- L2a. filtro por cliente (contagem + página 1)';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT @n = COUNT(*) FROM pedido p WHERE p.cliente_id = @cli;
    SELECT p.id, p.cliente_id, c.nome, p.data_criacao, p.valor_total INTO #t4
    FROM pedido p JOIN cliente c ON c.id = p.cliente_id
    WHERE p.cliente_id = @cli ORDER BY p.data_criacao DESC, p.id DESC OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
    DROP TABLE #t4; SET @i += 1;
END

PRINT '--- L2b. cliente + período 2026-03-01..2026-03-31 (contagem + página 1)';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT @n = COUNT(*) FROM pedido p WHERE p.cliente_id = @cli AND p.data_criacao >= '2026-03-01' AND p.data_criacao < '2026-04-01';
    SELECT p.id, p.cliente_id, c.nome, p.data_criacao, p.valor_total INTO #t5
    FROM pedido p JOIN cliente c ON c.id = p.cliente_id
    WHERE p.cliente_id = @cli AND p.data_criacao >= '2026-03-01' AND p.data_criacao < '2026-04-01'
    ORDER BY p.data_criacao DESC, p.id DESC OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
    DROP TABLE #t5; SET @i += 1;
END

PRINT '--- L2c. sem filtros (contagem + página 1)';
SET @i = 0;
WHILE @i < 3
BEGIN
    SELECT @n = COUNT(*) FROM pedido p;
    SELECT p.id, p.cliente_id, c.nome, p.data_criacao, p.valor_total INTO #t6
    FROM pedido p JOIN cliente c ON c.id = p.cliente_id
    ORDER BY p.data_criacao DESC, p.id DESC OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
    DROP TABLE #t6; SET @i += 1;
END

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
