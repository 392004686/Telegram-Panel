import paramiko
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
stdin, stdout, stderr = c.exec_command("ps -ef | grep -E 'docker build|deploy-1.01|pnpm|dotnet' | grep -v grep; echo '---'; docker ps --format 'table {{.Names}}\t{{.Status}}'; echo '---'; cd /opt/Telegram-Panel && git log -1 --oneline")
print(stdout.read().decode('utf-8','replace'))
print(stderr.read().decode('utf-8','replace'))
c.close()
