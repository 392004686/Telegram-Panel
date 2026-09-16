# 服务器部署指南 - 更新5000端口自定义版

## 服务器信息
- 服务器: 16.216.65.42
- SSH用户: root
- SSH密码: 1UWvPrUJcUvi
- 自定义版端口: 5000 (需要更新)
- 原作者版端口: 7000 (不要动)

## 当前状态
- 当前5000镜像: telegram-panel:multi-user-ui-2263b05
- 新代码commit: 40e6836
- 目标镜像: telegram-panel:multi-user-ui-40e6836

## 部署步骤

### 1. 连接到服务器
```bash
ssh root@16.216.65.42
# 密码: 1UWvPrUJcUvi
```

### 2. 进入项目目录并更新代码
```bash
cd /opt/Telegram-Panel

# 显示当前状态
echo "当前分支和commit:"
git branch --show-current
git log -1 --oneline

# 拉取最新代码
git fetch origin
git checkout codex/multi-user-ui
git pull origin codex/multi-user-ui

# 确认更新到最新
echo "更新后的commit:"
git log -1 --oneline
# 应该显示: 40e6836
```

### 3. 备份数据
```bash
# 创建带时间戳的备份
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="/root/telegram-panel-data-${TIMESTAMP}.tar.gz"

echo "创建备份: $BACKUP_FILE"
tar -czf "$BACKUP_FILE" docker-data/

# 验证备份
ls -lh "$BACKUP_FILE"
sha256sum "$BACKUP_FILE"
```

### 4. 构建新Docker镜像
```bash
# 获取commit hash
COMMIT_HASH=$(git rev-parse --short HEAD)
echo "Commit Hash: $COMMIT_HASH"

# 构建镜像（同时打两个tag）
IMAGE_TAG="telegram-panel:multi-user-ui-${COMMIT_HASH}"
docker build -t "$IMAGE_TAG" -t "telegram-panel:latest" .

# 验证镜像构建成功
docker images | grep telegram-panel | head -5
```

### 5. 停止并更新5000端口容器
```bash
# 停止5000端口的容器（注意：只停止telegram-panel服务）
docker compose stop telegram-panel

# 删除旧容器
docker compose rm -f telegram-panel

# 确认只有telegram-panel停止，其他容器不受影响
docker ps -a
```

### 6. 更新配置并启动新容器
```bash
# 更新.env文件使用最新镜像
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

# 查看.env配置
echo "当前.env配置:"
cat .env

# 启动5000端口容器
docker compose up -d telegram-panel

# 等待容器启动
echo "等待容器启动..."
sleep 10
```

### 7. 验证5000端口部署
```bash
# 检查容器状态
echo "容器状态:"
docker compose ps telegram-panel

# 查看使用的镜像
echo "使用的镜像:"
docker inspect telegram-panel --format '{{.Config.Image}}'

# 健康检查
echo "健康检查:"
for i in {1..5}; do
    if curl -sf http://127.0.0.1:5000/healthz > /dev/null; then
        echo "✓ 5000端口健康检查通过"
        break
    else
        echo "等待服务启动... ($i/5)"
        sleep 3
    fi
done

# 详细检查
curl -i http://127.0.0.1:5000/healthz

# 查看容器日志（检查是否有错误）
echo "容器日志（最后50行）:"
docker logs --tail 50 telegram-panel
```

### 8. 验证7000端口原作者版未受影响
```bash
# 检查7000端口容器
echo "检查7000端口原作者版:"
docker ps | grep 7000

# 如果7000端口有容器，检查健康状态
curl -sf http://127.0.0.1:7000/healthz && echo "✓ 7000端口正常" || echo "✗ 7000端口异常"
```

### 9. 测试新功能
```bash
# 打开浏览器访问
# http://16.216.65.42:5000

# 测试以下功能：
# 1. 客户列表 - 批量操作栏是否正常
# 2. 客户列表 - 活跃状态和最后在线列是否显示
# 3. 客户查询 - 实时查询提示是否正确
# 4. 客户查询 - 历史查询状态筛选是否可用
# 5. 任务中心 - 批量建群任务是否置顶
```

## 回滚步骤（如果出现问题）

### 快速回滚
```bash
cd /opt/Telegram-Panel

# 停止新容器
docker compose stop telegram-panel
docker compose rm -f telegram-panel

# 切换回旧镜像
sed -i 's|^TP_IMAGE=.*|TP_IMAGE=telegram-panel:multi-user-ui-2263b05|' .env

# 启动旧容器
docker compose up -d telegram-panel

# 验证
curl http://127.0.0.1:5000/healthz
```

### 恢复数据（如果需要）
```bash
# 找到最近的备份
ls -lht /root/telegram-panel-data-*.tar.gz | head -3

# 停止容器
docker compose stop telegram-panel

# 恢复数据
cd /opt/Telegram-Panel
rm -rf docker-data
tar -xzf /root/telegram-panel-data-YYYYMMDD-HHMMSS.tar.gz

# 启动容器
docker compose up -d telegram-panel
```

## 验证清单

- [ ] 代码更新到commit 40e6836
- [ ] 数据备份已创建并验证
- [ ] 新镜像构建成功
- [ ] 5000端口容器使用新镜像启动
- [ ] 5000端口健康检查通过
- [ ] 5000端口日志无错误
- [ ] 7000端口原作者版未受影响
- [ ] 客户列表批量操作栏正常
- [ ] 活跃状态和最后在线列显示正常
- [ ] 客户查询功能正常
- [ ] 历史查询筛选功能可用

## 关键注意事项

1. **只更新5000端口**: docker-compose.yml中只操作telegram-panel服务
2. **不要碰7000端口**: 7000端口是原作者版对照容器，完全不动
3. **先备份再更新**: 每次更新前必须备份docker-data目录
4. **验证镜像标签**: 确认使用的是telegram-panel:latest，对应commit 40e6836
5. **检查日志**: 启动后检查日志，确认没有错误

## 常用命令

```bash
# 查看所有容器
docker ps -a

# 查看5000端口容器日志
docker logs -f telegram-panel

# 查看5000端口容器状态
docker compose ps telegram-panel

# 重启5000端口容器
docker compose restart telegram-panel

# 查看镜像列表
docker images | grep telegram-panel

# 进入5000端口容器
docker exec -it telegram-panel sh
```

## 完成后确认

部署完成后，在下面记录：
- 部署时间: _______________
- 使用镜像: telegram-panel:multi-user-ui-40e6836
- 备份文件: /root/telegram-panel-data-YYYYMMDD-HHMMSS.tar.gz
- 5000端口状态: _______________
- 7000端口状态: _______________
- 功能测试结果: _______________
