-- Cria o banco da aplicação, se ainda não existir.
-- Executar conectado ao banco master. Idempotente.

IF DB_ID(N'gestao_pedidos') IS NULL
BEGIN
    CREATE DATABASE gestao_pedidos;
END
