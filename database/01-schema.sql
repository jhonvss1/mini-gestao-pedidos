-- Schema do banco gestao_pedidos (spec, seção 3.2; plano, seções 2.3 e 2.7).
-- Executar conectado ao banco gestao_pedidos. Idempotente e sem GO: roda via sqlcmd e via ADO.NET.
-- Convenção: tabelas no singular, colunas, restrições e índices em snake_case.

IF OBJECT_ID(N'dbo.cliente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.cliente (
        id   int           IDENTITY(1, 1) NOT NULL,
        nome nvarchar(200) NOT NULL,
        CONSTRAINT pk_cliente PRIMARY KEY (id)
    );
END

IF OBJECT_ID(N'dbo.produto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.produto (
        id      int            IDENTITY(1, 1) NOT NULL,
        nome    nvarchar(200)  NOT NULL,
        preco   decimal(18, 2) NOT NULL,
        estoque int            NOT NULL,
        CONSTRAINT pk_produto PRIMARY KEY (id),
        CONSTRAINT ck_produto_preco   CHECK (preco > 0),
        CONSTRAINT ck_produto_estoque CHECK (estoque >= 0)
    );
END

IF OBJECT_ID(N'dbo.pedido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pedido (
        id           int            IDENTITY(1, 1) NOT NULL,
        cliente_id   int            NOT NULL,
        data_criacao datetime2(0)   NOT NULL,
        valor_total  decimal(18, 2) NOT NULL,
        CONSTRAINT pk_pedido PRIMARY KEY (id),
        CONSTRAINT fk_pedido_cliente FOREIGN KEY (cliente_id) REFERENCES dbo.cliente (id)
    );
END

IF OBJECT_ID(N'dbo.item_pedido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.item_pedido (
        id             int            IDENTITY(1, 1) NOT NULL,
        pedido_id      int            NOT NULL,
        produto_id     int            NOT NULL,
        quantidade     int            NOT NULL,
        preco_unitario decimal(18, 2) NOT NULL,
        CONSTRAINT pk_item_pedido PRIMARY KEY (id),
        CONSTRAINT fk_item_pedido_pedido  FOREIGN KEY (pedido_id)  REFERENCES dbo.pedido (id),
        CONSTRAINT fk_item_pedido_produto FOREIGN KEY (produto_id) REFERENCES dbo.produto (id),
        CONSTRAINT ck_item_pedido_quantidade     CHECK (quantidade > 0),
        CONSTRAINT ck_item_pedido_preco_unitario CHECK (preco_unitario > 0),
        CONSTRAINT uq_item_pedido_pedido_id_produto_id UNIQUE (pedido_id, produto_id)
    );
END

-- Listagem sem filtro de cliente (faixa de data + ordem estável) e ranking por período.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_pedido_data_criacao_id' AND object_id = OBJECT_ID(N'dbo.pedido'))
BEGIN
    CREATE INDEX ix_pedido_data_criacao_id
        ON dbo.pedido (data_criacao DESC, id DESC)
        INCLUDE (cliente_id, valor_total);
END

-- Listagem filtrada por cliente, com ou sem período; também apoia a FK cliente_id.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_pedido_cliente_id_data_criacao_id' AND object_id = OBJECT_ID(N'dbo.pedido'))
BEGIN
    CREATE INDEX ix_pedido_cliente_id_data_criacao_id
        ON dbo.pedido (cliente_id, data_criacao DESC, id DESC)
        INCLUDE (valor_total);
END
