import sys, time, paramiko
host, user, password = '16.216.65.42', 'root', '1UWvPrUJcUvi'
script = r'''set -euo pipefail
cd /opt/Telegram-Panel
echo '==== 1 git ===='
git fetch origin
git checkout codex/multi-user-ui
git pull origin codex/multi-user-ui
git log -1 --oneline
COMMIT_HASH=$(git rev-parse --short HEAD)
echo COMMIT=$COMMIT_HASH
echo '==== 2 backup ===='
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE=/root/telegram-panel-data-${TIMESTAMP}.tar.gz
tar -czf "$BACKUP_FILE" docker-data/
ls -lh "$BACKUP_FILE"
echo '==== 3 build ===='
IMAGE_TAG=telegram-panel:multi-user-ui-${COMMIT_HASH}
docker build -t "$IMAGE_TAG" -t telegram-panel:latest .
echo '==== 4 recreate 5000 only ===='
docker compose stop telegram-panel
docker compose rm -f telegram-panel
if grep -q '^TP_IMAGE=' .env 2>/dev/null; then sed -i 's|^TP_IMAGE=.*|TP_IMAGE=telegram-panel:latest|' .env; else echo 'TP_IMAGE=telegram-panel:latest' >> .env; fi
if grep -q '^TP_UPDATE_MODE=' .env 2>/dev/null; then sed -i 's/^TP_UPDATE_MODE=.*/TP_UPDATE_MODE=image/' .env; else echo 'TP_UPDATE_MODE=image' >> .env; fi
docker compose up -d telegram-panel
sleep 12
echo '==== 5 verify ===='
docker compose ps telegram-panel || true
docker inspect telegram-panel --format '{{.Config.Image}} {{.State.Status}}'
curl -sf http://127.0.0.1:5000/healthz && echo ' 5000 OK' || echo '5000 FAIL'
curl -sf http://127.0.0.1:7000/healthz && echo ' 7000 OK' || echo '7000 FAIL'
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
docker logs --tail 40 telegram-panel
echo DEPLOY_DONE COMMIT=$COMMIT_HASH BACKUP=$BACKUP_FILE IMAGE=$IMAGE_TAG
'''
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(host, username=user, password=password, timeout=30, allow_agent=False, look_for_keys=False, banner_timeout=30)
t = c.get_transport()
t.set_keepalive(30)
sftp = c.open_sftp()
with sftp.file('/root/deploy-1.01.sh', 'w') as f:
    f.write(script.replace('\r\n','\n'))
sftp.chmod('/root/deploy-1.01.sh', 0o755)
sftp.close()
print('uploaded /root/deploy-1.01.sh', flush=True)
chan = t.open_session()
chan.get_pty(width=160, height=40)
chan.settimeout(0.0)
chan.exec_command('bash /root/deploy-1.01.sh')
buf = ''
start = time.time()
while True:
    if chan.recv_ready():
        chunk = chan.recv(8192).decode('utf-8', 'replace')
        sys.stdout.write(chunk)
        sys.stdout.flush()
        buf += chunk
    if chan.recv_stderr_ready():
        chunk = chan.recv_stderr(8192).decode('utf-8', 'replace')
        sys.stdout.write(chunk)
        sys.stdout.flush()
        buf += chunk
    if chan.exit_status_ready() and not chan.recv_ready() and not chan.recv_stderr_ready():
        break
    if time.time() - start > 3600:
        print('\nTIMEOUT 3600s', flush=True)
        break
    time.sleep(0.2)
code = chan.recv_exit_status()
print('\nREMOTE_EXIT', code, flush=True)
c.close()
sys.exit(0 if code == 0 else 1)
