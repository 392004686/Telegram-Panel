set -euo pipefail
cd /opt/Telegram-Panel
echo START $(date -u)
git fetch origin
git checkout codex/multi-user-ui
git pull origin codex/multi-user-ui
git log -1 --oneline
COMMIT_HASH=$(git rev-parse --short HEAD)
echo COMMIT=$COMMIT_HASH
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE=/root/telegram-panel-data-${TIMESTAMP}.tar.gz
tar -czf "$BACKUP_FILE" docker-data/ || true
echo BACKUP=$BACKUP_FILE
IMAGE_TAG=telegram-panel:multi-user-ui-${COMMIT_HASH}
docker build -t "$IMAGE_TAG" -t telegram-panel:latest .
docker compose stop telegram-panel
docker compose rm -f telegram-panel
grep -q '^TP_IMAGE=' .env 2>/dev/null && sed -i 's|^TP_IMAGE=.*|TP_IMAGE=telegram-panel:latest|' .env || echo 'TP_IMAGE=telegram-panel:latest' >> .env
grep -q '^TP_UPDATE_MODE=' .env 2>/dev/null && sed -i 's/^TP_UPDATE_MODE=.*/TP_UPDATE_MODE=image/' .env || echo 'TP_UPDATE_MODE=image' >> .env
docker compose up -d --pull never telegram-panel
sleep 15
docker inspect telegram-panel --format '{{.Config.Image}} {{.State.Status}}'
curl -sf http://127.0.0.1:5000/healthz && echo 5000_OK || echo 5000_FAIL
curl -sf http://127.0.0.1:7000/healthz && echo 7000_OK || echo 7000_FAIL
echo DEPLOY_DONE COMMIT=$COMMIT_HASH IMAGE=$IMAGE_TAG BACKUP=$BACKUP_FILE
echo END $(date -u)
