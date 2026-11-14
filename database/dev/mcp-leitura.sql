-- Login SOMENTE LEITURA usado pelo servidor MCP (Data API builder) para o Claude consultar o banco LOCAL de
-- desenvolvimento. Só desenvolvimento: não faz parte do Compose nem do ambiente publicado.
-- Executar no master (o script entra no banco gestao_pedidos sozinho):
--   sqlcmd -C -S localhost,1433 -U sa -P "<senha do sa>" -d master -v SENHA_MCP="<senha>" -i database/dev/mcp-leitura.sql
-- Idempotente: cria o login ou, se existir, apenas atualiza a senha. Sem GO.
-- A senha deve atender à política do SQL Server (8+ caracteres, três entre maiúscula, minúscula, dígito e símbolo)
-- e NÃO pode conter o nome do login (mcp_reader).
--
-- Permissão: apenas SELECT nas quatro tabelas. Sem INSERT, UPDATE, DELETE, EXECUTE nem DDL. É uma das três camadas de
-- defesa (as outras: permissão "read" por entidade e ferramentas de escrita desligadas no dab-config.json).

DECLARE @senha nvarchar(128) = N'$(SENHA_MCP)';
DECLARE @literal nvarchar(300) = N'N''' + REPLACE(@senha, N'''', N'''''') + N'''';
DECLARE @comando nvarchar(max);

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'mcp_reader')
    SET @comando = N'CREATE LOGIN mcp_reader WITH PASSWORD = ' + @literal + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
ELSE
    SET @comando = N'ALTER LOGIN mcp_reader WITH PASSWORD = ' + @literal + N';';

EXEC (@comando);

EXEC gestao_pedidos.sys.sp_executesql N'
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N''mcp_reader'')
    CREATE USER mcp_reader FOR LOGIN mcp_reader;

GRANT SELECT ON dbo.cliente TO mcp_reader;
GRANT SELECT ON dbo.produto TO mcp_reader;
GRANT SELECT ON dbo.pedido TO mcp_reader;
GRANT SELECT ON dbo.item_pedido TO mcp_reader;';
