import paramiko, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('16.216.65.42', username='root', password='1UWvPrUJcUvi', timeout=30, allow_agent=False, look_for_keys=False)
cmd = r'''
echo ===TASK32===
docker exec telegram-panel python3 - <<'PY' 2>/dev/null || true
print('no-python')
PY
echo ===DB===
docker exec telegram-panel sh -c 'ls -l /data/*.db /app/*.db /data/docker-data/*.db 2>/dev/null; ls -l /data 2>/dev/null | head'
echo ===LOGS===
docker logs telegram-panel --since 20m 2>&1 | grep -E '32|CustomerGroup|建群|TaskId' | tail -80
'''
stdin, stdout, stderr = c.exec_command(cmd)
print(stdout.read().decode('utf-8','replace'))
print(stderr.read().decode('utf-8','replace'))
c.close()
