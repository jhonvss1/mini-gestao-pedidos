#!/usr/bin/env bash
# Troca os containers de produção pela versão publicada no GHCR. Roda NA INSTÂNCIA, chamado pelo workflow de deploy
# por SSH (depois de ele posicionar o repositório no commit certo) ou à mão, para voltar a uma versão anterior:
#
#   scripts/deploy-remoto.sh <tag-da-imagem>        # ex.: scripts/deploy-remoto.sh sha-1a2b3c4
#
# O que faz:
#   1. baixa as imagens da api e do frontend com a tag informada;
#   2. recria só o que mudou (o db continua de pé, com o mesmo volume, e o db-init reaplica os scripts idempotentes);
#   3. espera /api/saude responder Healthy (a API consultou o banco); se não responder, falha e mostra o log da API;
#   4. remove imagens antigas sem uso.
#
# NUNCA usa "down -v": os dados do banco não são tocados.

set -euo pipefail

TAG="${1:?Informe a tag da imagem (ex.: sha-1a2b3c4)}"
[[ "$TAG" =~ ^(latest|sha-[0-9a-f]{7})$ ]] || { echo "Tag inválida: $TAG" >&2; exit 1; }

cd "$(dirname "$0")/.."
[[ -f .env.producao ]] || { echo ".env.producao não encontrado em $(pwd)" >&2; exit 1; }

export IMAGE_TAG="$TAG"
COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.producao)

echo "[$(date -u +%FT%TZ)] Baixando as imagens ($TAG)"
"${COMPOSE[@]}" pull api frontend

echo "Atualizando os serviços"
"${COMPOSE[@]}" up -d --no-build

echo "Aguardando a API ficar saudável"
for tentativa in $(seq 1 36); do
  if "${COMPOSE[@]}" exec -T frontend wget -qO- http://127.0.0.1/api/saude 2>/dev/null | grep -q Healthy; then
    echo "API saudável após $((tentativa * 5)) s. Versão em produção: $TAG"
    docker image prune -f > /dev/null
    exit 0
  fi
  sleep 5
done

echo "A API não ficou saudável em 3 minutos. Últimas linhas do log:" >&2
"${COMPOSE[@]}" logs --tail 40 api >&2
exit 1
