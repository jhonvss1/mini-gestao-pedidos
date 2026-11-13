-- Carga de DEMONSTRAÇÃO maior, para o ambiente publicado não nascer vazio. Executar conectado ao banco gestao_pedidos.
-- Idempotente: só carrega se a tabela cliente estiver vazia, então nunca sobrescreve dados existentes.
-- Determinística: nenhum valor é aleatório (tudo sai de aritmética sobre o número da linha), então a carga é a mesma
-- a cada execução e pode ser testada. Alternativa ao 02-seed.sql, que é a carga pequena de desenvolvimento e testes.
--
-- Conteúdo: 40 clientes (3 pares de homônimos), 30 produtos (dois sem estoque e vários com estoque baixo) e 320
-- pedidos de 05/01/2026 a 12/11/2026 (cerca de um por dia), com 1 a 4 itens cada.
--   * pedidos mais antigos têm preço de venda menor que o preço atual do produto (o preço do item é preservado);
--   * valor_total é sempre a soma dos itens (RN-05);
--   * os clientes 1 a 8 compram mais, o que deixa o ranking dos cinco maiores variado e com homônimos.

SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.cliente)
BEGIN
    BEGIN TRANSACTION;

    SET IDENTITY_INSERT dbo.cliente ON;
    INSERT INTO dbo.cliente (id, nome) VALUES
        (1, N'Maria Silva'),        (2, N'João Souza'),         (3, N'Ana Lima'),            (4, N'Pedro Alves'),
        (5, N'Carla Mendes'),       (6, N'Rafael Costa'),       (7, N'Beatriz Rocha'),       (8, N'Lucas Ferreira'),
        (9, N'Juliana Martins'),    (10, N'Gustavo Ribeiro'),   (11, N'Fernanda Carvalho'),  (12, N'Rodrigo Gomes'),
        (13, N'Patrícia Barbosa'),  (14, N'Marcelo Araújo'),    (15, N'Camila Dias'),        (16, N'Felipe Nascimento'),
        (17, N'Aline Moreira'),     (18, N'Thiago Cardoso'),    (19, N'Larissa Teixeira'),   (20, N'Bruno Pereira'),
        (21, N'Vanessa Correia'),   (22, N'Diego Monteiro'),    (23, N'Renata Freitas'),     (24, N'André Barros'),
        (25, N'Tatiane Pinto'),     (26, N'Eduardo Machado'),   (27, N'Priscila Azevedo'),   (28, N'Leonardo Castro'),
        (29, N'Mariana Vieira'),    (30, N'Vinícius Lopes'),    (31, N'João Souza'),         (32, N'Maria Silva'),
        (33, N'Ana Paula Santos'),  (34, N'Ricardo Duarte'),    (35, N'Cristiane Nunes'),    (36, N'Henrique Borges'),
        (37, N'Bianca Rezende'),    (38, N'Fábio Siqueira'),    (39, N'Natália Campos'),     (40, N'Ana Lima');
    SET IDENTITY_INSERT dbo.cliente OFF;

    SET IDENTITY_INSERT dbo.produto ON;
    INSERT INTO dbo.produto (id, nome, preco, estoque) VALUES
        (1, N'Teclado mecânico', 89.90, 120),             (2, N'Mouse sem fio', 79.90, 200),
        (3, N'Monitor 24 polegadas', 899.00, 35),         (4, N'Headset com microfone', 149.50, 5),
        (5, N'Webcam HD', 199.90, 0),                     (6, N'Cabo HDMI 2 m', 24.90, 500),
        (7, N'Hub USB', 59.90, 80),                       (8, N'Notebook 15,6 polegadas', 3499.00, 12),
        (9, N'SSD 480 GB', 289.90, 60),                   (10, N'Memória RAM 16 GB', 349.00, 25),
        (11, N'Mousepad grande', 39.90, 150),             (12, N'Cadeira gamer', 1199.00, 8),
        (13, N'Impressora multifuncional', 749.00, 14),   (14, N'Roteador Wi-Fi 6', 399.00, 40),
        (15, N'Switch 8 portas', 129.90, 55),             (16, N'Nobreak 600 VA', 429.00, 18),
        (17, N'Carregador USB-C 65 W', 119.90, 90),       (18, N'Pen drive 64 GB', 44.90, 300),
        (19, N'HD externo 1 TB', 379.00, 3),              (20, N'Caixa de som Bluetooth', 219.90, 45),
        (21, N'Fone de ouvido sem fio', 259.90, 70),      (22, N'Teclado numérico', 49.90, 0),
        (23, N'Suporte para notebook', 89.00, 65),        (24, N'Webcam Full HD', 329.90, 22),
        (25, N'Microfone condensador', 459.00, 9),        (26, N'Estabilizador 1000 VA', 349.00, 16),
        (27, N'Cabo de rede 5 m', 19.90, 400),            (28, N'Adaptador USB-C para HDMI', 79.00, 110),
        (29, N'Tablet 10 polegadas', 1499.00, 11),        (30, N'Smartwatch', 699.00, 6);
    SET IDENTITY_INSERT dbo.produto OFF;

    -- 320 pedidos. A data espalha as compras do início de janeiro a 12/11/2026; o valor_total entra zerado e é
    -- recalculado a partir dos itens logo abaixo.
    SET IDENTITY_INSERT dbo.pedido ON;
    ;WITH numeros AS (
        SELECT TOP (320) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b
    )
    INSERT INTO dbo.pedido (id, cliente_id, data_criacao, valor_total)
    SELECT n,
           CASE WHEN n % 4 = 0 THEN 1 + ((n / 4) % 8) ELSE 1 + ((n * 7 + n / 3) % 40) END,
           DATEADD(SECOND, (n * 7919) % 86400, DATEADD(DAY, (n * 31) % 312, CAST('2026-01-05' AS datetime2(0)))),
           0
    FROM numeros;
    SET IDENTITY_INSERT dbo.pedido OFF;

    -- 1 a 4 itens por pedido, sempre de produtos distintos (o passo de 11 é coprimo com 30).
    INSERT INTO dbo.item_pedido (pedido_id, produto_id, quantidade, preco_unitario)
    SELECT p.id,
           pr.id,
           1 + ((p.id + s.i * 3) % 5),
           CAST(ROUND(pr.preco * (1.0 - (11 - MONTH(p.data_criacao)) * 0.005), 2) AS decimal(18, 2))
    FROM dbo.pedido AS p
    CROSS JOIN (VALUES (0), (1), (2), (3)) AS s(i)
    INNER JOIN dbo.produto AS pr ON pr.id = 1 + ((p.id * 5 + s.i * 11) % 30)
    WHERE s.i < 1 + (p.id % 4);

    UPDATE p
    SET valor_total = t.total
    FROM dbo.pedido AS p
    INNER JOIN (
        SELECT pedido_id, SUM(quantidade * preco_unitario) AS total
        FROM dbo.item_pedido
        GROUP BY pedido_id
    ) AS t ON t.pedido_id = p.id;

    COMMIT TRANSACTION;
END
