-- Login do banco usado pela API em produção, em vez do "sa". Executar no master:
--   sqlcmd -d master -v SENHA_APP="<senha>" -i 03-login-aplicacao.sql
-- Idempotente: cria o login ou, se ele já existir, apenas atualiza a senha (rotação de segredo).
-- A senha deve atender à política de senhas do SQL Server (CHECK_POLICY): mínimo de 8 caracteres, três entre maiúscula,
-- minúscula, dígito e símbolo, e NÃO pode conter o nome do login (app_pedidos). Aspas simples na senha são tratadas.

DECLARE @senha nvarchar(128) = N'$(SENHA_APP)';
DECLARE @literal nvarchar(300) = N'N''' + REPLACE(@senha, N'''', N'''''') + N'''';
DECLARE @comando nvarchar(max);

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'app_pedidos')
    SET @comando = N'CREATE LOGIN app_pedidos WITH PASSWORD = ' + @literal + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
ELSE
    SET @comando = N'ALTER LOGIN app_pedidos WITH PASSWORD = ' + @literal + N';';

EXEC (@comando);
