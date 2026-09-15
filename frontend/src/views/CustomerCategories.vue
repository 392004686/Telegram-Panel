<template>
  <div class="category-page">
    <el-card shadow="never">
      <template #header><div class="header"><div><b>客户分类管理</b><div class="muted">维护分类名称和说明；删除分类后关联客户自动变为未分类。</div></div><el-button @click="load">刷新</el-button></div></template>
      <el-form label-position="top" class="create-form">
        <el-form-item label="分类名称"><el-input v-model="createForm.name" placeholder="例如：潜在客户" /></el-form-item>
        <el-form-item label="描述"><el-input v-model="createForm.description" placeholder="分类用途说明" /></el-form-item>
        <el-form-item><el-button type="primary" :loading="creating" :disabled="!createForm.name.trim()" @click="createGroup">添加分类</el-button></el-form-item>
      </el-form>
      <el-table v-loading="loading" :data="groups" stripe>
        <el-table-column prop="name" label="分类名称" min-width="180" /><el-table-column prop="description" label="描述" min-width="260"><template #default="{ row }">{{ row.description || '-' }}</template></el-table-column><el-table-column prop="customerCount" label="客户数量" width="110" />
        <el-table-column label="操作" width="140" fixed="right"><template #default="{ row }"><el-button link type="primary" @click="openEdit(row)">编辑</el-button><el-button link type="danger" @click="removeGroup(row)">删除</el-button></template></el-table-column>
      </el-table>
    </el-card>
    <el-dialog v-model="edit.visible" title="编辑客户分类" width="460px"><el-form label-position="top"><el-form-item label="分类名称"><el-input v-model="edit.name" /></el-form-item><el-form-item label="描述"><el-input v-model="edit.description" type="textarea" :rows="3" /></el-form-item></el-form><template #footer><el-button @click="edit.visible=false">取消</el-button><el-button type="primary" :loading="edit.saving" @click="saveEdit">保存</el-button></template></el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { panelApi } from '@/api/panel'
import type { CustomerGroupOption } from '@/api/types'
const groups = ref<CustomerGroupOption[]>([])
const loading = ref(false), creating = ref(false)
const createForm = reactive({ name: '', description: '' })
const edit = reactive({ visible: false, saving: false, id: 0, name: '', description: '' })
async function load() { loading.value = true; try { groups.value = await panelApi.customerGroups() } finally { loading.value = false } }
async function createGroup() { creating.value = true; try { await panelApi.createCustomerGroup(createForm); Object.assign(createForm, { name: '', description: '' }); ElMessage.success('分类已添加'); await load() } finally { creating.value = false } }
function openEdit(row: CustomerGroupOption) { Object.assign(edit, { visible: true, id: row.id, name: row.name, description: row.description || '' }) }
async function saveEdit() { edit.saving = true; try { await panelApi.updateCustomerGroup(edit.id, { name: edit.name, description: edit.description }); edit.visible = false; ElMessage.success('分类已更新'); await load() } finally { edit.saving = false } }
async function removeGroup(row: CustomerGroupOption) { await ElMessageBox.confirm(`删除分类“${row.name}”？`, '确认删除', { type: 'warning' }); await panelApi.deleteCustomerGroup(row.id); ElMessage.success('分类已删除'); await load() }
onMounted(load)
</script>
<style scoped>.header{display:flex;justify-content:space-between;align-items:center}.muted{color:var(--tp-muted);margin-top:6px}.create-form{display:grid;grid-template-columns:220px minmax(260px,1fr) auto;gap:12px;align-items:end;margin-bottom:18px}.create-form :deep(.el-form-item){margin-bottom:0}@media(max-width:760px){.create-form{grid-template-columns:1fr}}</style>
