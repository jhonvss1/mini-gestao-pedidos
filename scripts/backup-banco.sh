#!/usr/bin/env bash
# Backup do banco gestao_pedidos em produção (rodar no servidor, na raiz do projeto ou de qualquer pasta).
#
#   scripts/backup-banco.sh
#
# O que faz:
#   1. BACKUP DATABASE dentro do container, com CHECKSUM (a edição Express não comprime backups);
#   2. RESTORE VERIFYONLY para conferir que o arquivo é legível;
#   3. copia para o servidor, comprime com gzip e remove a cópia de dentro do container;
#   4. se S3_DESTINO estiver definido, envia para o S3 (cópia fora do servidor);
#   5. apaga backups locais com mais de RETENCAO_DIAS dias.
#
# Variáveis opcionais: DIR_BACKUP (padrão /var/backups/gestao-pedidos), RETENCAO_DIAS (14),
# S3_DESTINO (ex.: s3://meu-bucket/gestao-pedidos; usa o perfil IAM da instância).
#
# Agendamento sugerido (crontab -e): 0 3 * * * /caminho/do/projeto/scripts/backup-banco.sh >> /var/log/backup-gestao-pedidos.log 2>&1

set -euo pipefail

DIR_BACKUP="${DIR_BACKUP:-/var/backups/gestao-pedidos}"
RETENCAO_DIAS="${RETENCAO_DIAS:-14}"
S3_DESTINO="${S3_DESTINO:-}"
DIR_NO_CONTAINER="/var/opt/mssql/backup"

cd "$(dirname "$0")/.."
COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.producao)

[[ "$RETENCAO_DIAS" =~ ^[0-9]+$ ]] || { echo "RETENCAO_DIAS deve ser um número inteiro." >&2; exit 1; }
[[ -n "$DIR_BACKUP" && "$DIR_BACKUP" != "/" ]] || { echo "DIR_BACKUP inválido." >&2; exit 1; }

SENHA="$("${COMPOSE[@]}" exec -T db printenv MSSQL_SA_PASSWORD | tr -d '\r\n')"
sql() { "${COMPOSE[@]}" exec -T db /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$SENHA" -h -1 -W; }

carimbo="$(date -u +%Y%m%dT%H%M%SZ)"
arquivo="gestao_pedidos_${carimbo}.bak"

mkdir -p "$DIR_BACKUP"
"${COMPOSE[@]}" exec -T db mkdir -p "$DIR_NO_CONTAINER"

echo "[$(date -u +%FT%TZ)] Gerando backup $arquivo"
printf '%s\nGO\n' "BACKUP DATABASE gestao_pedidos TO DISK = N'${DIR_NO_CONTAINER}/${arquivo}' WITH CHECKSUM, INIT, NAME = N'gestao_pedidos ${carimbo}';" | sql

echo "Verificando o arquivo"
printf '%s\nGO\n' "RESTORE VERIFYONLY FROM DISK = N'${DIR_NO_CONTAINER}/${arquivo}' WITH CHECKSUM;" | sql

"${COMPOSE[@]}" cp "db:${DIR_NO_CONTAINER}/${arquivo}" "${DIR_BACKUP}/${arquivo}"
"${COMPOSE[@]}" exec -T db rm -f "${DIR_NO_CONTAINER}/${arquivo}"
gzip -f "${DIR_BACKUP}/${arquivo}"
echo "Backup salvo em ${DIR_BACKUP}/${arquivo}.gz"

if [[ -n "$S3_DESTINO" ]]; then
  aws s3 cp "${DIR_BACKUP}/${arquivo}.gz" "${S3_DESTINO%/}/${arquivo}.gz"
  echo "Cópia enviada para ${S3_DESTINO%/}/${arquivo}.gz"
fi

# Retenção local: só arquivos de backup deste projeto dentro de DIR_BACKUP.
find "$DIR_BACKUP" -maxdepth 1 -type f -name 'gestao_pedidos_*.bak.gz' -mtime "+${RETENCAO_DIAS}" -print -delete
echo "[$(date -u +%FT%TZ)] Backup concluído"
