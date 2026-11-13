#!/usr/bin/env bash
# Restaura um backup gerado por scripts/backup-banco.sh.
#
#   scripts/restaurar-banco.sh <arquivo.bak.gz> [banco_destino]
#
# O destino PADRÃO é "gestao_pedidos_verificacao": um banco separado, que serve para TESTAR o backup sem tocar
# nos dados reais (consulte-o e depois apague com DROP DATABASE). Para restaurar de verdade sobre o banco de
# produção, informe gestao_pedidos como destino; o script pede confirmação, para a API e o frontend durante a
# restauração e os religa em seguida. Isso SUBSTITUI todos os dados atuais.

set -euo pipefail

ARQUIVO="${1:?Informe o arquivo de backup (.bak.gz)}"
DESTINO="${2:-gestao_pedidos_verificacao}"
DIR_NO_CONTAINER="/var/opt/mssql/backup"

[[ -f "$ARQUIVO" ]] || { echo "Arquivo não encontrado: $ARQUIVO" >&2; exit 1; }
[[ "$DESTINO" =~ ^[A-Za-z0-9_]+$ ]] || { echo "Nome de banco inválido: $DESTINO" >&2; exit 1; }

cd "$(dirname "$0")/.."
COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.producao)

SENHA="$("${COMPOSE[@]}" exec -T db printenv MSSQL_SA_PASSWORD | tr -d '\r\n')"
sql() { "${COMPOSE[@]}" exec -T db /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$SENHA" -h -1 -W; }

SOBRE_PRODUCAO=false
if [[ "$DESTINO" == "gestao_pedidos" ]]; then
  SOBRE_PRODUCAO=true
  echo "ATENÇÃO: isto vai SUBSTITUIR todos os dados do banco de produção pelos do backup."
  read -r -p "Digite RESTAURAR para continuar: " confirmacao
  [[ "$confirmacao" == "RESTAURAR" ]] || { echo "Cancelado."; exit 1; }
  "${COMPOSE[@]}" stop api frontend
fi

nome="$(basename "$ARQUIVO" .gz)"
"${COMPOSE[@]}" exec -T db mkdir -p "$DIR_NO_CONTAINER"
gunzip -c "$ARQUIVO" | "${COMPOSE[@]}" exec -T db sh -c "cat > ${DIR_NO_CONTAINER}/${nome}"

echo "Restaurando para o banco ${DESTINO}"
printf '%s\nGO\n' "RESTORE DATABASE [${DESTINO}] FROM DISK = N'${DIR_NO_CONTAINER}/${nome}' WITH REPLACE, CHECKSUM,
  MOVE N'gestao_pedidos' TO N'/var/opt/mssql/data/${DESTINO}.mdf',
  MOVE N'gestao_pedidos_log' TO N'/var/opt/mssql/data/${DESTINO}_log.ldf';" | sql

"${COMPOSE[@]}" exec -T db rm -f "${DIR_NO_CONTAINER}/${nome}"

if [[ "$SOBRE_PRODUCAO" == "true" ]]; then
  # O login e o usuário da API podem ter ficado órfãos: o db-init recria o vínculo (idempotente).
  "${COMPOSE[@]}" run --rm db-init
  "${COMPOSE[@]}" up -d api frontend
fi

echo "Restauração concluída em ${DESTINO}."
echo "Conferir: contagem de pedidos no banco restaurado:"
printf '%s\nGO\n' "SET NOCOUNT ON; SELECT COUNT(*) AS pedidos FROM [${DESTINO}].dbo.pedido;" | sql
