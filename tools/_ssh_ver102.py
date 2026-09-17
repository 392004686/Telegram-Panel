import paramiko, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
cmd = 'cd /opt/Telegram-Panel && git log -1 --oneline && docker inspect telegram-panel --format "{{.Config.Image}} {{.State.Health.Status}}" && docker ps --format "{{.Names}} {{.Status}} {{.Ports}}" && docker exec telegram-panel sh -c "grep -l 1.0.2 /app/wwwroot/panel-spa/assets/MainLayout*.js"'
stdin, stdout, stderr = c.exec_command(cmd)
print(stdout.read().decode('utf-8','replace'))
print(stderr.read().decode('utf-8','replace'))
c.close()
