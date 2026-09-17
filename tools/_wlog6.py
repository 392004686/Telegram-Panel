from pathlib import Path
p=Path('src/TelegramPanel.Data/AppDbContext.cs')
t=p.read_text(encoding='utf-8')
old='            entity.HasIndex(e => new { e.ExecutionKind, e.Status, e.NextEligibleAtUtc });\n        });'
new=old+'\n\n        modelBuilder.Entity<BatchTaskLog>(entity =>\n        {\n            entity.HasKey(e => e.Id);\n            entity.Property(e => e.Level).IsRequired().HasMaxLength(20);\n            entity.Property(e => e.Message).IsRequired().HasMaxLength(4000);\n            entity.HasIndex(e => new { e.BatchTaskId, e.CreatedAt });\n        });'
if old not in t: raise SystemExit('cfg missing')
p.write_text(t.replace(old,new,1), encoding='utf-8')
print('ctx cfg ok')
