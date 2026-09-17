import paramiko
script = open('tools/_remote_deploy.sh', encoding='utf-8').read().replace('\r\n','\n')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
c.get_transport().set_keepalive(30)
sftp = c.open_sftp()
with sftp.file('/root/deploy-1.02.sh','w') as f:
    f.write(script)
sftp.chmod('/root/deploy-1.02.sh', 0o755)
sftp.close()
cmd = 'nohup bash /root/deploy-1.02.sh >/root/deploy-1.02.log 2>&1 </dev/null & sleep 2; wc -l /root/deploy-1.02.log; tail -20 /root/deploy-1.02.log'
stdin, stdout, stderr = c.exec_command(cmd)
print(stdout.read().decode('utf-8','replace'))
print(stderr.read().decode('utf-8','replace'))
c.close()
