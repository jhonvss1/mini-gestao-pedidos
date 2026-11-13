-- Usuário da API no banco e o MÍNIMO de permissões que ela usa. Executar conectado ao banco gestao_pedidos,
-- depois de 01-schema.sql e 03-login-aplicacao.sql. Idempotente (GRANT repetido não falha).
--
-- O que a API faz e, portanto, o que ela recebe:
--   * lê clientes e produtos (listagens, catálogo, snapshot do estoque);
--   * baixa o estoque com UPDATE condicional: só a coluna estoque de produto;
--   * grava pedido e itens (INSERT; o OUTPUT do identity exige SELECT);
--   * nunca altera preço nem nome, nunca apaga nada e nunca muda o schema.

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'app_pedidos')
    CREATE USER app_pedidos FOR LOGIN app_pedidos;

IF DATABASE_PRINCIPAL_ID(N'papel_aplicacao') IS NULL
    CREATE ROLE papel_aplicacao;

IF IS_ROLEMEMBER(N'papel_aplicacao', N'app_pedidos') <> 1
    ALTER ROLE papel_aplicacao ADD MEMBER app_pedidos;

GRANT SELECT ON dbo.cliente TO papel_aplicacao;
GRANT SELECT ON dbo.produto TO papel_aplicacao;
GRANT UPDATE (estoque) ON dbo.produto TO papel_aplicacao;
GRANT SELECT, INSERT ON dbo.pedido TO papel_aplicacao;
GRANT SELECT, INSERT ON dbo.item_pedido TO papel_aplicacao;
