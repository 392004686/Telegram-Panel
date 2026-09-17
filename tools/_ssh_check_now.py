import paramiko, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
cmd = '''echo ===IMAGE===
docker inspect telegram-panel --format "{{.Config.Image}} {{.Created}} {{.State.Status}} {{.State.Health.Status}}"
echo ===HEALTH===
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5000/healthz
echo ===VER===
docker exec telegram-panel sh -c "grep -o 1.0.[0-9] /app/wwwroot/panel-spa/assets/MainLayout*.js | sort | uniq"
echo ===DEPLOYLOG===
tail -40 /root/deploy-1.02.log 2>/dev/null || echo NOLOG
echo ===GIT===
cd /opt/Telegram-Panel && git log -1 --format="%h %ci %s" && git status -sb
echo ===AUTHOR===
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:7000/healthz
'''
stdin, stdout, stderr = c.exec_command(cmd)
print(stdout.read().decode('utf-8','replace'))
err = stderr.read().decode('utf-8','replace')
if err.strip():
    print('STDERR', err)
c.close()
