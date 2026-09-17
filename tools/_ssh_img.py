import paramiko, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
cmds = [
 'docker images --format "{{.Repository}}:{{.Tag}} {{.ID}} {{.CreatedSince}}" | grep telegram-panel | head -20',
 'grep -n "image:\|pull_policy\|TP_IMAGE" /opt/Telegram-Panel/docker-compose.yml /opt/Telegram-Panel/.env | head -40',
]
for cmd in cmds:
    print('>>>', cmd)
    stdin, stdout, stderr = c.exec_command(cmd)
    print(stdout.read().decode('utf-8','replace') + stderr.read().decode('utf-8','replace'))
c.close()
