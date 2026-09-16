#!/bin/bash
# Telegram Panel 部署脚本
# 构建Docker镜像并更新容器

set -e

echo "=========================================="
echo "  Telegram Panel 部署脚本"
echo "=========================================="

# 获取当前分支
BRANCH=$(git branch --show-current)
echo "当前分支: $BRANCH"

# 获取最新的commit hash
COMMIT_HASH=$(git rev-parse --short HEAD)
echo "Commit Hash: $COMMIT_HASH"

# 定义镜像名称和标签
IMAGE_NAME="telegram-panel-custom"
IMAGE_TAG="${BRANCH}-${COMMIT_HASH}"
IMAGE_FULL="${IMAGE_NAME}:${IMAGE_TAG}"
IMAGE_LATEST="${IMAGE_NAME}:latest"

echo ""
echo "=========================================="
echo "  步骤 1: 构建前端"
echo "=========================================="
cd frontend
if [ ! -d "node_modules" ]; then
    echo "安装前端依赖..."
    pnpm install --frozen-lockfile
fi

echo "构建前端..."
pnpm run build

cd ..

echo ""
echo "=========================================="
echo "  步骤 2: 构建 Docker 镜像"
echo "=========================================="
echo "镜像名称: $IMAGE_FULL"

docker build -t "$IMAGE_FULL" -t "$IMAGE_LATEST" .

echo ""
echo "=========================================="
echo "  步骤 3: 停止并删除旧容器"
echo "=========================================="

if docker ps -a | grep -q telegram-panel; then
    echo "停止旧容器..."
    docker stop telegram-panel || true
    echo "删除旧容器..."
    docker rm telegram-panel || true
else
    echo "没有发现旧容器"
fi

echo ""
echo "=========================================="
echo "  步骤 4: 启动新容器"
echo "=========================================="

# 更新docker-compose.yml中的镜像
export TP_IMAGE="$IMAGE_LATEST"

echo "使用镜像: $TP_IMAGE"
docker-compose up -d

echo ""
echo "=========================================="
echo "  步骤 5: 检查容器状态"
echo "=========================================="

sleep 5
docker ps | grep telegram-panel

echo ""
echo "=========================================="
echo "  步骤 6: 查看日志"
echo "=========================================="

docker logs --tail 50 telegram-panel

echo ""
echo "=========================================="
echo "  部署完成！"
echo "=========================================="
echo "访问地址: http://localhost:5000"
echo "镜像标签: $IMAGE_TAG"
echo ""
echo "查看实时日志: docker logs -f telegram-panel"
echo "查看容器状态: docker-compose ps"
echo "停止容器: docker-compose down"
echo "=========================================="
