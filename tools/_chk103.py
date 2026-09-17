from pathlib import Path
t = Path('frontend/src/components/TaskConfigForm.vue').read_text(encoding='utf-8')
i = t.find('extra-parts')
Path('tools/_chk103.out').write_text(t[i+800:i+1800], encoding='utf-8')
