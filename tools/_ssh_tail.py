import paramiko, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
stdin, stdout, stderr = c.exec_command('ps -ef | grep deploy-1.01 | grep -v grep; echo ---TAIL---; tail -40 /root/deploy-1.01.log')
out = stdout.read().decode('utf-8','replace')
print(out.encode('utf-8','replace').decode('utf-8','replace'))
c.close()
