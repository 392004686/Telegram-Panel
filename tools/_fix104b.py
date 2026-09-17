from pathlib import Path

p = Path('src/TelegramPanel.Web/Api/CustomerManagementApi.cs')
t = p.read_text(encoding='utf-8')
old = 'private static async Task<IResult> GroupsAsync(AppDbContext db) => Results.Ok(await db.CustomerGroups.AsNoTracking().OrderBy(x => x.Name).Select(x => new CustomerGroupDto(x.Id, x.Name, x.Description, x.Assignments.Count)).ToListAsync());'
new = 'private static async Task<IResult> GroupsAsync(AppDbContext db) => Results.Ok(await db.CustomerGroups.AsNoTracking().OrderBy(x => x.Name).Select(x => new CustomerGroupDto(x.Id, x.Name, x.Description, x.Assignments.Count, x.Assignments.Count(a => a.Customer.InteractionStatus != "contacted"))).ToListAsync());'
if old not in t: raise SystemExit('groups query missing')
t = t.replace(old, new, 1)
old = 'public sealed record CustomerGroupDto(int Id, string Name, string? Description, int CustomerCount);'
new = 'public sealed record CustomerGroupDto(int Id, string Name, string? Description, int CustomerCount, int PendingCustomerCount);'
if old not in t: raise SystemExit('dto missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
print('api ok')

p = Path('frontend/src/api/types.ts')
t = p.read_text(encoding='utf-8')
old = 'export interface CustomerGroupOption { id: number; name: string; description?: string | null; customerCount?: number }'
new = 'export interface CustomerGroupOption { id: number; name: string; description?: string | null; customerCount?: number; pendingCustomerCount?: number }'
if old not in t: raise SystemExit('types missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
print('types ok')

p = Path('frontend/src/components/TaskConfigForm.vue')
t = p.read_text(encoding='utf-8')
old = '<el-option v-for="item in customerGroups" :key="item.id" :label="`${item.name}（${item.customerCount ?? 0}）`" :value="item.id" /></el-select><div class="form-hint no-offset">按所选分类中的未沟通客户自动分配；当前分类统计合计 {{ selectedCustomerCount }} 人，重叠客户执行时自动去重。</div>'
new = '<el-option v-for="item in customerGroups" :key="item.id" :label="`${item.name}（未沟通 ${item.pendingCustomerCount ?? 0} / 共 ${item.customerCount ?? 0}）`" :value="item.id" /></el-select><div class="form-hint no-offset">只邀请未沟通客户；已沟通的会跳过，不会重复建群。当前未沟通 {{ selectedPendingCount }} 人，分类合计 {{ selectedCustomerCount }} 人，重叠客户执行时自动去重。</div>'
if old not in t: raise SystemExit('form hint missing')
t = t.replace(old, new, 1)
old = 'const selectedCustomerCount = computed(() => forms.groupEngagement.customerGroupIds.reduce((sum, id) => sum + (customerGroups.value.find(x => x.id === id)?.customerCount ?? 0), 0))'
new = '''const selectedCustomerCount = computed(() => forms.groupEngagement.customerGroupIds.reduce((sum, id) => sum + (customerGroups.value.find(x => x.id === id)?.customerCount ?? 0), 0))
const selectedPendingCount = computed(() => forms.groupEngagement.customerGroupIds.reduce((sum, id) => sum + (customerGroups.value.find(x => x.id === id)?.pendingCustomerCount ?? 0), 0))'''
if old not in t: raise SystemExit('selected count missing')
t = t.replace(old, new, 1)
old = '  return { total: Math.max(1, selectedCustomerCount.value), config: JSON.stringify(config), canSubmit: true, validationError: null }'
new = '  if (selectedPendingCount.value <= 0) throw new Error("所选客户分类没有未沟通客户。已沟通的不会重复建群邀请，请先改回未执行或换分类。")\n  return { total: Math.max(1, selectedPendingCount.value), config: JSON.stringify(config), canSubmit: true, validationError: null }'
if old not in t: raise SystemExit('draft total missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
print('form ok')
