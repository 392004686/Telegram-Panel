import paramiko
host, user, password = '16.216.65.42', 'root', '1UWvPrUJcUvi'
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(host, username=user, password=password, timeout=30, allow_agent=False, look_for_keys=False)
def run(cmd, timeout=60):
    print('>>>', cmd, flush=True)
    stdin, stdout, stderr = c.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode('utf-8', 'replace')
    err = stderr.read().decode('utf-8', 'replace')
    code = stdout.channel.recv_exit_status()
    print(out + err, end='' if (out+err).endswith('\n') else '\n', flush=True)
    print('exit', code, flush=True)
    return code
run('hostname; date; docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"')
run('cd /opt/Telegram-Panel && git rev-parse --abbrev-ref HEAD && git log -1 --oneline')
c.close()
