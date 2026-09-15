<template>
  <div class="page-shell">
    <div class="page-header"><div><div class="breadcrumb">客户管理</div><h1>客户管理</h1><p>沉淀手机号和 @账号，按导入批次、客户分组和查询状态筛选。</p></div><el-button type="primary" @click="importDialog.visible = true">批量导入</el-button></div>
    <el-card shadow="never" class="filter-card">
      <el-row :gutter="12">
        <el-col :xs="24" :sm="8"><el-input v-model="filters.search" clearable placeholder="手机号、@用户名或昵称" @keyup.enter="load" /></el-col>
        <el-col :xs="12" :sm="5"><el-select v-model="filters.status" clearable placeholder="查询状态" class="full"><el-option label="待查询" value="pending"/><el-option label="已找到" value="found"/><el-option label="未确认" value="not_found"/><el-option label="查询异常" value="error"/></el-select></el-col>
        <el-col :xs="12" :sm="5"><el-select v-model="filters.groupId" clearable placeholder="客户分组" class="full"><el-option v-for="g in groups" :key="g.id" :label="g.name" :value="g.id"/></el-select></el-col>
        <el-col :xs="24" :sm="6"><el-button @click="load">查询</el-button><el-button @click="reset">重置</el-button></el-col>
      </el-row>
    </el-card>
    <el-card shadow="never">
      <el-table v-loading="loading" :data="rows" stripe>
        <el-table-column prop="id" label="ID" width="72"/><el-table-column prop="phone" label="手机号" min-width="145"><template #default="{row}">{{ row.phone || '-' }}</template></el-table-column>
        <el-table-column label="用户名" min-width="140"><template #default="{row}">{{ row.username ? `@${row.username}` : '-' }}</template></el-table-column>
        <el-table-column prop="displayName" label="昵称" min-width="130"/><el-table-column prop="telegramUserId" label="Telegram ID" min-width="130"/>
        <el-table-column label="查询状态" width="110"><template #default="{row}"><el-tag :type="row.lookupStatus === 'found' ? 'success' : row.lookupStatus === 'pending' ? 'info' : 'warning'">{{ statusLabel(row.lookupStatus) }}</el-tag></template></el-table-column>
        <el-table-column label="分组" min-width="150"><template #default="{row}"><el-tag v-for="g in row.groups" :key="g.id" class="mr-1" effect="plain">{{ g.name }}</el-tag><span v-if="!row.groups.length">-</span></template></el-table-column>
        <el-table-column label="导入时间" min-width="165"><template #default="{row}">{{ formatTime(row.createdAt) }}</template></el-table-column>
        <el-table-column label="操作" width="90" fixed="right"><template #default="{row}"><el-button link type="danger" @click="remove(row)">删除</el-button></template></el-table-column>
      </el-table>
      <div class="pager"><span>共 {{ total }} 条</span><el-pagination v-model:current-page="filters.page" v-model:page-size="filters.pageSize" layout="sizes, prev, pager, next" :total="total" :page-sizes="[20,50,100]" @change="load"/></div>
    </el-card>

    <el-dialog v-model="importDialog.visible" title="批量导入客户" width="min(680px, calc(100vw - 24px))">
      <el-alert title="每行一个国际格式手机号或 @用户名；重复客户不会重复建档，但会记录到本次批次并加入所选分组。" type="info" :closable="false" class="mb-3"/>
      <el-form label-position="top">
        <el-form-item label="批次名称"><el-input v-model="importDialog.batchName" placeholder="例如：9月美国客户第一批（留空自动生成）"/></el-form-item>
        <el-form-item label="客户分组"><el-select v-model="importDialog.groupId" clearable class="full" placeholder="可选"><el-option v-for="g in groups" :key="g.id" :label="g.name" :value="g.id"/></el-select></el-form-item>
        <el-form-item label="或新建分组"><el-input v-model="importDialog.newGroupName" placeholder="填写后优先新建/复用同名分组"/></el-form-item>
        <el-form-item label="手机号 / @用户名"><el-input v-model="importDialog.values" type="textarea" :rows="12" placeholder="+12125550123&#10;@username"/></el-form-item>
      </el-form>
      <template #footer><el-button @click="importDialog.visible=false">关闭</el-button><el-button type="primary" :loading="importDialog.saving" @click="submitImport">导入</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'; import { ElMessage, ElMessageBox } from 'element-plus'; import { panelApi } from '@/api/panel'; import type { CustomerGroupOption, CustomerItem } from '@/api/types'; import { formatTime } from '@/utils/format'
const loading=ref(false), rows=ref<CustomerItem[]>([]), total=ref(0), groups=ref<CustomerGroupOption[]>([])
const filters=reactive({page:1,pageSize:20,search:'',status:'',groupId:undefined as number|undefined})
const importDialog=reactive({visible:false,saving:false,batchName:'',groupId:undefined as number|undefined,newGroupName:'',values:''})
const statusLabel=(v:string)=>({pending:'待查询',found:'已找到',not_found:'未确认',error:'查询异常'}[v]||v)
async function load(){loading.value=true;try{const r=await panelApi.customers({...filters});rows.value=r.items;total.value=r.total;groups.value=await panelApi.customerGroups()}finally{loading.value=false}}
function reset(){filters.search='';filters.status='';filters.groupId=undefined;filters.page=1;load()}
async function submitImport(){if(!importDialog.values.trim()){ElMessage.warning('请填写手机号或 @用户名');return}importDialog.saving=true;try{const r=await panelApi.importCustomers({values:importDialog.values,batchName:importDialog.batchName||undefined,groupId:importDialog.groupId,newGroupName:importDialog.newGroupName||undefined});ElMessage.success(`导入 ${r.imported}，重复 ${r.duplicates}，无效 ${r.invalid}`);importDialog.visible=false;importDialog.values='';await load()}finally{importDialog.saving=false}}
async function remove(row:CustomerItem){await ElMessageBox.confirm(`删除客户 #${row.id}？`,'确认删除',{type:'warning'});await panelApi.deleteCustomer(row.id);ElMessage.success('已删除');await load()}
onMounted(load)
</script>
<style scoped>.page-header{display:flex;justify-content:space-between;gap:16px;align-items:flex-start;margin-bottom:18px}.page-header h1{margin:4px 0}.page-header p,.breadcrumb{color:var(--tp-muted)}.filter-card{margin-bottom:14px}.full{width:100%}.pager{display:flex;align-items:center;justify-content:flex-end;gap:18px;margin-top:16px}.mr-1{margin-right:4px}.mb-3{margin-bottom:12px}@media(max-width:640px){.page-header{flex-direction:column}.pager{flex-wrap:wrap}}</style>
