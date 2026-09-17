import paramiko, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
stdin, stdout, stderr = c.exec_command('pgrep -af deploy-1.02 || echo NODEPLOY; echo ---; tail -18 /root/deploy-1.02.log')
print(stdout.read().decode('utf-8','replace'))
c.close()
