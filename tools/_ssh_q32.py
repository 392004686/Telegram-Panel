import paramiko, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
sql = r'''
.headers on
.mode line
SELECT Id, Name, Status, Total, Completed, Failed, CreatedAt, StartedAt, CompletedAt, substr(Config,1,800) AS Config FROM BatchTasks WHERE Id=32;
SELECT Id, Level, Message, CreatedAt FROM BatchTaskLogs WHERE BatchTaskId=32;
SELECT g.Id, g.Name, COUNT(a.CustomerId) AS members, SUM(CASE WHEN c.InteractionStatus='contacted' THEN 1 ELSE 0 END) AS contacted, SUM(CASE WHEN c.InteractionStatus!='contacted' OR c.InteractionStatus IS NULL THEN 1 ELSE 0 END) AS pending
FROM CustomerGroups g
LEFT JOIN CustomerGroupAssignments a ON a.CustomerGroupId=g.Id
LEFT JOIN Customers c ON c.Id=a.CustomerId
WHERE g.Id=2 GROUP BY g.Id, g.Name;
SELECT c.Id, c.Phone, c.Username, c.DisplayName, c.InteractionStatus FROM Customers c JOIN CustomerGroupAssignments a ON a.CustomerId=c.Id WHERE a.CustomerGroupId=2;
'''
sftp = c.open_sftp()
with sftp.file('/tmp/q32.sql','w') as f: f.write(sql)
sftp.close()
cmd = "docker exec telegram-panel sh -c 'command -v sqlite3; ls /usr/bin/sqlite* 2>/dev/null; which sqlite3'"
stdin, stdout, stderr = c.exec_command(cmd)
print('BIN', stdout.read().decode('utf-8','replace'), stderr.read().decode('utf-8','replace'))
cmd = 'apt-get install -y sqlite3 >/dev/null 2>&1; sqlite3 /opt/Telegram-Panel/docker-data/telegram-panel.db < /tmp/q32.sql'
stdin, stdout, stderr = c.exec_command(cmd)
print(stdout.read().decode('utf-8','replace'))
print(stderr.read().decode('utf-8','replace'))
c.close()
