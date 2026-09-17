import paramiko, sys, time
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
cmd = 'cd /opt/Telegram-Panel && docker compose up -d --pull never telegram-panel && sleep 12 && docker inspect telegram-panel --format "{{.Config.Image}} {{.State.Status}} {{.State.Health.Status}}" && echo --- && curl -sf -o /dev/null -w "5000 %{http_code}\n" http://127.0.0.1:5000/healthz && curl -sf -o /dev/null -w "7000 %{http_code}\n" http://127.0.0.1:7000/healthz && docker ps --format "{{.Names}} {{.Status}} {{.Ports}}" && echo --- && docker logs --tail 15 telegram-panel'
stdin, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode('utf-8','replace'))
print(stderr.read().decode('utf-8','replace'))
c.close()
