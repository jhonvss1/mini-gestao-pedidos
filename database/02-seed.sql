-- Carga inicial determinística (spec, seção 3.3). Executar conectado ao banco gestao_pedidos.
-- Idempotente: só carrega se a tabela cliente estiver vazia. Datas fixas e ids explícitos.
--
-- Cenários cobertos:
--   * dois clientes homônimos (ids 3 e 5, "João Souza") e um cliente sem pedidos (id 8);
--   * produto com estoque baixo (id 4, estoque 5) e produto sem estoque (id 5, estoque 0);
--   * outubro/2026: 7 clientes com pedidos (corte do top 5 observável), empate de total
--     entre os clientes 2 e 4 e pedidos nas bordas do período (31/10 23:59:59 e 01/11 00:00:00);
--   * setembro/2026: 6 clientes com pedidos e preços de venda diferentes do preço atual do
--     cadastro (o preço do item é preservado).

SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.cliente)
BEGIN
    BEGIN TRANSACTION;

    SET IDENTITY_INSERT dbo.cliente ON;
    INSERT INTO dbo.cliente (id, nome) VALUES
        (1, N'Maria Silva'),
        (2, N'Ana Lima'),
        (3, N'João Souza'),
        (4, N'Pedro Alves'),
        (5, N'João Souza'),
        (6, N'Carla Mendes'),
        (7, N'Rafael Costa'),
        (8, N'Beatriz Rocha');
    SET IDENTITY_INSERT dbo.cliente OFF;

    SET IDENTITY_INSERT dbo.produto ON;
    INSERT INTO dbo.produto (id, nome, preco, estoque) VALUES
        (1, N'Teclado mecânico', 89.90, 120),
        (2, N'Mouse sem fio', 79.90, 200),
        (3, N'Monitor 24 polegadas', 899.00, 35),
        (4, N'Headset com microfone', 149.50, 5),
        (5, N'Webcam HD', 199.90, 0),
        (6, N'Cabo HDMI 2 m', 24.90, 500),
        (7, N'Hub USB', 59.90, 80);
    SET IDENTITY_INSERT dbo.produto OFF;

    SET IDENTITY_INSERT dbo.pedido ON;
    INSERT INTO dbo.pedido (id, cliente_id, data_criacao, valor_total) VALUES
        -- Outubro/2026
        (1,  1, '2026-10-03T10:15:00', 1877.90),
        (2,  3, '2026-10-05T14:30:00',  389.50),
        (3,  5, '2026-10-07T09:00:00',  899.00),
        (4,  2, '2026-10-10T16:45:00',  448.40),
        (5,  4, '2026-10-12T11:20:00',  448.40),
        (6,  6, '2026-10-15T13:10:00',  169.80),
        (7,  7, '2026-10-20T17:00:00',   59.90),
        (8,  3, '2026-10-22T08:30:00',  899.00),
        (9,  1, '2026-10-31T23:59:59',   99.60),
        -- Novembro/2026 (borda do período de outubro)
        (10, 6, '2026-11-01T00:00:00',  159.80),
        -- Setembro/2026
        (11, 2, '2026-09-02T10:00:00',   84.90),
        (12, 5, '2026-09-14T15:00:00',  879.00),
        (13, 7, '2026-09-21T12:00:00',  229.00),
        (14, 8, '2026-09-28T18:30:00',  179.70),
        (15, 4, '2026-09-30T23:59:59',  159.80),
        (16, 1, '2026-09-10T09:30:00',  229.40);
    SET IDENTITY_INSERT dbo.pedido OFF;

    SET IDENTITY_INSERT dbo.item_pedido ON;
    INSERT INTO dbo.item_pedido (id, pedido_id, produto_id, quantidade, preco_unitario) VALUES
        (1,  1,  3, 2, 899.00),
        (2,  1,  2, 1,  79.90),
        (3,  2,  1, 3,  89.90),
        (4,  2,  7, 2,  59.90),
        (5,  3,  3, 1, 899.00),
        (6,  4,  4, 2, 149.50),
        (7,  4,  6, 6,  24.90),
        (8,  5,  4, 2, 149.50),
        (9,  5,  6, 6,  24.90),
        (10, 6,  1, 1,  89.90),
        (11, 6,  2, 1,  79.90),
        (12, 7,  7, 1,  59.90),
        (13, 8,  3, 1, 899.00),
        (14, 9,  6, 4,  24.90),
        (15, 10, 2, 2,  79.90),
        (16, 11, 1, 1,  84.90),
        (17, 12, 3, 1, 879.00),
        (18, 13, 6, 10, 22.90),
        (19, 14, 7, 3,  59.90),
        (20, 15, 2, 2,  79.90),
        (21, 16, 4, 1, 149.50),
        (22, 16, 2, 1,  79.90);
    SET IDENTITY_INSERT dbo.item_pedido OFF;

    COMMIT TRANSACTION;
END
