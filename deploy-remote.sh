#!/bin/bash
# 远程服务器部署脚本
# 服务器: 16.216.65.42

set -e

SERVER="16.216.65.42"
USER="root"
PASSWORD="1UWvPrUJcUvi"
REMOTE_DIR="/opt/Telegram-Panel"
BRANCH="codex/multi-user-ui"

echo "=========================================="
echo "  Telegram Panel 远程部署脚本"
echo "=========================================="
echo "服务器: $SERVER"
echo "分支: $BRANCH"
echo ""

# 获取当前commit
COMMIT_HASH=$(git rev-parse --short HEAD)
IMAGE_TAG="telegram-panel:multi-user-ui-${COMMIT_HASH}"

echo "当前 Commit: $COMMIT_HASH"
echo "镜像标签: $IMAGE_TAG"
echo ""

# 使用sshpass进行SSH连接并执行命令
# 如果没有sshpass，需要手动输入密码

echo "=========================================="
echo "  步骤 1: 连接到服务器并更新代码"
echo "=========================================="

ssh root@${SERVER} << 'ENDSSH'
set -e

cd /opt/Telegram-Panel

echo "当前目录: $(pwd)"
echo "当前分支: $(git branch --show-current)"

# 拉取最新代码
echo "拉取最新代码..."
git fetch origin
git checkout codex/multi-user-ui
git pull origin codex/multi-user-ui

echo "代码更新完成"
echo "最新 commit: $(git log -1 --oneline)"

ENDSSH

echo ""
echo "=========================================="
echo "  步骤 2: 备份数据"
echo "=========================================="

ssh root@${SERVER} << 'ENDSSH'
set -e

cd /opt/Telegram-Panel

# 创建备份
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="/root/telegram-panel-data-${TIMESTAMP}.tar.gz"

echo "创建备份: $BACKUP_FILE"
tar -czf "$BACKUP_FILE" docker-data/

# 验证备份
if [ -f "$BACKUP_FILE" ]; then
    SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
    echo "备份成功: $BACKUP_FILE (大小: $SIZE)"
    sha256sum "$BACKUP_FILE"
else
    echo "错误: 备份文件未创建"
    exit 1
fi

ENDSSH

echo ""
echo "=========================================="
echo "  步骤 3: 构建Docker镜像"
echo "=========================================="

ssh root@${SERVER} << 'ENDSSH'
set -e

cd /opt/Telegram-Panel

COMMIT_HASH=$(git rev-parse --short HEAD)
IMAGE_TAG="telegram-panel:multi-user-ui-${COMMIT_HASH}"

echo "构建镜像: $IMAGE_TAG"

# 构建镜像
docker build -t "$IMAGE_TAG" -t "telegram-panel:latest" .

echo "镜像构建完成"
docker images | grep telegram-panel | head -5

ENDSSH

echo ""
echo "=========================================="
echo "  步骤 4: 停止旧容器"
echo "=========================================="

ssh root@${SERVER} << 'ENDSSH'
set -e

cd /opt/Telegram-Panel

echo "停止容器..."
docker compose stop telegram-panel || true

echo "删除旧容器..."
docker compose rm -f telegram-panel || true

ENDSSH

echo ""
echo "=========================================="
echo "  步骤 5: 启动新容器"
echo "=========================================="

ssh root@${SERVER} << 'ENDSSH'
set -e

cd /opt/Telegram-Panel

# 更新.env文件使用本地镜像
if [ -f .env ]; then
    sed -i 's|^TP_IMAGE=.*|TP_IMAGE=telegram-panel:latest|' .env
else
    echo "TP_IMAGE=telegram-panel:latest" > .env
fi

# 确保使用image模式
if grep -q "^TP_UPDATE_MODE=" .env; then
    sed -i 's/^TP_UPDATE_MODE=.*/TP_UPDATE_MODE=image/' .env
else
    echo "TP_UPDATE_MODE=image" >> .env
fi

echo "启动容器..."
docker compose up -d telegram-panel

echo "等待容器启动..."
sleep 10

ENDSSH

echo ""
echo "=========================================="
echo "  步骤 6: 验证部署"
echo "=========================================="

ssh root@${SERVER} << 'ENDSSH'
set -e

cd /opt/Telegram-Panel

echo "容器状态:"
docker compose ps telegram-panel

echo ""
echo "使用的镜像:"
docker inspect telegram-panel --format '{{.Config.Image}}'

echo ""
echo "健康检查:"
for i in {1..5}; do
    if curl -sf http://127.0.0.1:5000/healthz > /dev/null; then
        echo "✓ 健康检查通过"
        break
    else
        echo "等待服务启动... ($i/5)"
        sleep 3
    fi
done

curl -i http://127.0.0.1:5000/healthz 2>&1 | head -10

echo ""
echo "容器日志 (最后50行):"
docker logs --tail 50 telegram-panel 2>&1 | tail -50

ENDSSH

echo ""
echo "=========================================="
echo "  步骤 7: 验证7000端口作者版"
echo "=========================================="

ssh root@${SERVER} << 'ENDSSH'
set -e

echo "检查7000端口作者版:"
docker ps | grep telegram-panel || echo "未发现7000端口容器"

ENDSSH

echo ""
echo "=========================================="
echo "  部署完成！"
echo "=========================================="
echo "服务器: http://$SERVER:5000"
echo "面板用户: tgpanel"
echo "面板密码: tgpanel123"
echo ""
echo "查看日志: ssh root@$SERVER 'docker logs -f telegram-panel'"
echo "=========================================="
