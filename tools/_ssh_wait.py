import paramiko, time, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
c.get_transport().set_keepalive(30)
deadline = time.time() + 900
last = ''
while time.time() < deadline:
    stdin, stdout, stderr = c.exec_command('tail -20 /root/deploy-1.01.log; echo ---; pgrep -af deploy-1.01 || true')
    out = stdout.read().decode('utf-8','replace')
    if out != last:
        print('---- poll ----', flush=True)
        print(out, flush=True)
        last = out
    if 'DEPLOY_DONE' in out or 'ERROR: failed to build' in out or 'ELIFECYCLE' in out:
        break
    time.sleep(20)
c.close()
