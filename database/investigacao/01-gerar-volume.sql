-- Gera um volume determinístico em gestao_pedidos_volume (executar conectado a esse banco, depois do 01-schema.sql).
--   5.000 clientes (1.000 nomes repetidos = homônimos; 500 clientes SEM nenhum pedido)
--     200 produtos
-- 500.000 pedidos entre 2025-01-01 e 2026-12-31 (metade em 2026), com 1 a 5 itens cada (~1,5 milhão de itens)
-- Sem aleatoriedade: CHECKSUM(n) e aritmética modular dão sempre os mesmos dados. Idempotente (não recarrega).

SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM dbo.pedido)
BEGIN
    PRINT 'Volume ja carregado: nada a fazer.';
    RETURN;
END

-- Sequência 1..500000 reutilizada pelas cargas.
CREATE TABLE #n (n int NOT NULL PRIMARY KEY);
INSERT INTO #n (n)
SELECT TOP (500000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL))
FROM sys.all_objects a CROSS JOIN sys.all_objects b;

-- Clientes: os nomes se repetem a cada 4.000 (clientes 4001..5000 são homônimos dos 1..1000).
INSERT INTO dbo.cliente (nome)
SELECT N'Cliente ' + CAST(((n - 1) % 4000) + 1 AS nvarchar(10))
FROM #n WHERE n <= 5000 ORDER BY n;

INSERT INTO dbo.produto (nome, preco, estoque)
SELECT N'Produto ' + CAST(n AS nvarchar(10)),
       CAST(5 + ((n * 37) % 4000) / 10.0 AS decimal(18, 2)),
       1000000
FROM #n WHERE n <= 200 ORDER BY n;

-- Pedidos: só os clientes 1..4500 compram (os 500 últimos ficam sem pedido, para expor o efeito do LEFT JOIN).
SET IDENTITY_INSERT dbo.pedido ON;
INSERT INTO dbo.pedido (id, cliente_id, data_criacao, valor_total)
SELECT n,
       ((n * 7) % 4500) + 1,
       DATEADD(SECOND, CAST((CAST(CHECKSUM(n) AS bigint) & 2147483647) % 63072000 AS int), CAST('2025-01-01' AS datetime2(0))),
       0
FROM #n ORDER BY n;
SET IDENTITY_INSERT dbo.pedido OFF;

-- Itens: 1 a 5 por pedido; produtos distintos dentro do pedido (respeita a unicidade pedido_id + produto_id).
INSERT INTO dbo.item_pedido (pedido_id, produto_id, quantidade, preco_unitario)
SELECT n.n,
       ((n.n * 7 + k.k * 13) % 200) + 1,
       ((n.n + k.k) % 5) + 1,
       pr.preco
FROM #n n
CROSS APPLY (VALUES (1), (2), (3), (4), (5)) AS k (k)
JOIN dbo.produto pr ON pr.id = ((n.n * 7 + k.k * 13) % 200) + 1
WHERE k.k <= 1 + (n.n % 5)
ORDER BY n.n, k.k;

-- valor_total coerente com os itens (RN-05).
UPDATE p
SET valor_total = s.total
FROM dbo.pedido p
JOIN (SELECT pedido_id, SUM(quantidade * preco_unitario) AS total
      FROM dbo.item_pedido GROUP BY pedido_id) s ON s.pedido_id = p.id;

DROP TABLE #n;

UPDATE STATISTICS dbo.cliente;
UPDATE STATISTICS dbo.produto;
UPDATE STATISTICS dbo.pedido;
UPDATE STATISTICS dbo.item_pedido;

SELECT (SELECT COUNT(*) FROM dbo.cliente) AS clientes,
       (SELECT COUNT(*) FROM dbo.produto) AS produtos,
       (SELECT COUNT(*) FROM dbo.pedido) AS pedidos,
       (SELECT COUNT(*) FROM dbo.pedido WHERE data_criacao >= '2026-01-01' AND data_criacao < '2027-01-01') AS pedidos_2026,
       (SELECT COUNT(*) FROM dbo.item_pedido) AS itens;
