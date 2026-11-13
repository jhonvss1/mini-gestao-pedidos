-- Investigação da query (Parte 4 do teste): cria uma base SEPARADA, só para medir desempenho com volume.
-- Executar conectado ao master. Nunca roda no Compose padrão nem nos testes. Idempotente e sem GO.
-- Depois, no banco criado aqui, rodar database/01-schema.sql (mesmo schema do projeto) e 01-gerar-volume.sql.

IF DB_ID(N'gestao_pedidos_volume') IS NULL
BEGIN
    CREATE DATABASE gestao_pedidos_volume;
END
