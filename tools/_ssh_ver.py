import paramiko, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
cmd = 'cd /opt/Telegram-Panel && git log -1 --oneline && docker inspect telegram-panel --format "{{.Config.Image}} {{.Image}}" && docker exec telegram-panel sh -c "grep -R -l 1.01 /app/wwwroot/panel-spa 2>/dev/null | head -5"'
stdin, stdout, stderr = c.exec_command(cmd, timeout=30)
print(stdout.read().decode('utf-8','replace'))
print(stderr.read().decode('utf-8','replace'))
c.close()
