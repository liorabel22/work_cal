<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
type Section = { enabled: boolean; status: number; updateStatus: boolean; updateTime: boolean; time: string; lowIds: string; midIds: string; highIds: string; all: boolean; typeB: number[] }
type Preview = { summaries: { tableName: string; countsText: string; displayText: string }[]; rows: { tableName: string; action: string; key: string; currentValues: string; proposedValues: string }[] }
const credentials = reactive({ server: '', user: 'sa', password: '' })
const connected = ref(false), databases = ref<string[]>([]), database = ref(''), tables = ref<string[]>([]), options = ref<{lowId: number; idText: string}[]>([])
const loaded = ref(false)
const busy = ref(false), message = ref(''), preview = ref<Preview | null>(null), theme = ref('system')
const names = ['overall', 'gain', 'amplitude', 'zakan'] as const
const defaults = (name: string): Section => ({ enabled: false, status: name === 'overall' ? 2 : 0, updateStatus: true, updateTime: true, time: '', lowIds: '', midIds: '', highIds: '', all: true, typeB: [] })
const sections = reactive(Object.fromEntries(names.map(n => [n, defaults(n)])) as Record<typeof names[number], Section>)
const baselines = reactive<Record<string, string>>({})
const requirements: Record<string, string[]> = { overall: ['overall_status', 'type_a', 'type_b'], gain: ['gain'], amplitude: ['amplitude'], zakan: ['zakan'] }
const missing = (n: string) => requirements[n]!.filter(t => !tables.value.some(x => x.toLowerCase().replace('dbo.', '') === t))
const signature = (s: Section) => JSON.stringify({ ...s, enabled: undefined })
const pending = computed(() => names.filter(n => sections[n].enabled && !missing(n).length && signature(sections[n]) !== baselines[n]))
function clearDatabase() { loaded.value = false; tables.value = []; options.value = []; preview.value = null; for (const n of names) { Object.assign(sections[n], defaults(n)); delete baselines[n] } }
function reset() { connected.value = false; databases.value = []; database.value = ''; credentials.password = ''; clearDatabase(); message.value = '' }
watch(credentials, () => { connected.value = false; databases.value = []; database.value = ''; clearDatabase() })
watch(sections, () => { preview.value = null }, { deep: true })
watch(theme, value => { document.documentElement.dataset.theme = value })
async function request(path: string, body: unknown) {
  const response = await fetch('/api/' + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
  const result = await response.json()
  if (!response.ok || result.isSuccess === false) throw new Error(result.message || 'Request failed')
  return result
}
async function run(action: () => Promise<void>) { busy.value = true; message.value = ''; try { await action() } catch (e) { message.value = e instanceof Error ? e.message : 'Request failed' } finally { busy.value = false } }
function connect() { return run(async () => { const r = await request('connect', credentials); databases.value = r.databases; connected.value = true; message.value = r.message }) }
function selectDatabase() { clearDatabase(); if (!database.value) return; return run(async () => { const r = await request('database', { connection: credentials, database: database.value }); tables.value = r.availableTables; options.value = r.items; loaded.value = true; message.value = r.message || 'Database ready.' }) }
function timestamp(value: string) { if (!value) return null; const time = new Date(value).getTime(); if (!Number.isFinite(time)) throw new Error('Choose a valid date and time.'); return time }
async function changes(action: 'preview' | 'apply') {
  if (action === 'apply' && !window.confirm('Apply the pending changes to ' + database.value + '?')) return
  return run(async () => {
    const body: Record<string, unknown> = { connection: credentials, database: database.value }
    const applied = [...pending.value]
    for (const n of applied) {
      const s = sections[n]
      if (n === 'gain' || n === 'zakan') body[n] = s.status
      else body[n] = { ...s, time: timestamp(s.time) }
    }
    const r = await request(action, body); message.value = r.message
    if (action === 'preview') preview.value = r
    else { for (const n of applied) baselines[n] = signature(sections[n]); preview.value = null }
  })
}
</script>

<template>
  <main>
    <header><div><p class="eyebrow">SQL SERVER WORKSPACE</p><h1>LIOR_CAL<span> / database manager</span></h1></div><label>Appearance<select v-model="theme"><option value="system">System</option><option value="light">Light</option><option value="dark">Dark</option></select></label></header>
    <fieldset :disabled="busy" class="workspace">
      <section class="card"><h2>Connection</h2><p>Connect with SQL Server Authentication. Credentials stay in memory for this session.</p><form @submit.prevent="connect"><div class="fields"><label>Server<input v-model="credentials.server" required placeholder="db.example.com,1433" autocomplete="off"></label><label>Username<input v-model="credentials.user" required autocomplete="off"></label><label>Password<input v-model="credentials.password" type="password" autocomplete="off"></label></div><div class="actions"><button class="primary">{{ busy ? 'Working…' : 'Connect' }}</button><button type="button" @click="reset">Reset</button><small>Encrypted connection · certificate validation enabled</small></div></form></section>
      <section v-if="connected" class="card"><h2>Working database</h2><label>Database<select v-model="database" @change="selectDatabase"><option value="">Select a database</option><option v-for="db in databases" :key="db">{{ db }}</option></select></label></section>
      <div v-if="database && loaded" class="grid">
        <section v-for="n in names" :key="n" class="card"><h2><label class="inline"><input v-model="sections[n].enabled" type="checkbox" :disabled="missing(n).length > 0">{{ n.charAt(0).toUpperCase() + n.slice(1) }} modification</label></h2><p v-if="missing(n).length" class="warning">Missing tables: {{ missing(n).join(', ') }}</p>
          <fieldset :disabled="!sections[n].enabled || missing(n).length > 0">
            <template v-if="n === 'overall'"><div class="fields"><label>Mid IDs<input v-model="sections[n].midIds" placeholder="1, 4, 7-10"></label><label>High IDs<input v-model="sections[n].highIds" placeholder="1-3"></label></div><label class="inline"><input v-model="sections[n].all" type="checkbox">All TB options</label><div class="options"><label v-for="o in options" :key="o.lowId" class="inline"><input v-model="sections[n].typeB" :value="o.lowId" type="checkbox" :disabled="sections[n].all">{{ o.idText }}</label></div></template>
            <label v-if="n === 'amplitude'">Low IDs<input v-model="sections[n].lowIds" placeholder="1, 4, 7-10"></label>
            <label v-if="n === 'overall' || n === 'amplitude'" class="inline"><input v-model="sections[n].updateStatus" type="checkbox">Update status</label>
            <label>Status<select v-model="sections[n].status" :disabled="!sections[n].updateStatus"><template v-if="n === 'overall'"><option :value="0">None</option><option :value="1">In Progress</option><option :value="2">Pass</option><option :value="3">Fail</option></template><template v-else><option :value="0">False</option><option :value="1">True</option></template></select></label>
            <template v-if="n === 'overall' || n === 'amplitude'"><label class="inline"><input v-model="sections[n].updateTime" type="checkbox">Update time</label><label>Time<input v-model="sections[n].time" type="datetime-local" :disabled="!sections[n].updateTime"></label><small>Leave blank to use the current time when Apply runs. Chosen times use your browser’s timezone.</small></template>
          </fieldset>
        </section>
      </div>
      <footer v-if="database"><span>{{ pending.length }} section(s) with pending changes</span><button :disabled="!pending.length" @click="changes('preview')">Preview changes</button><button class="primary" :disabled="!pending.length" @click="changes('apply')">Apply changes</button></footer>
    </fieldset>
    <p v-if="busy" role="status">Working…</p><p v-if="message" role="status" class="message">{{ message }}</p>
    <section v-if="preview" class="card preview"><div class="actions"><h2>Change preview</h2><button @click="preview = null">Close</button></div><p>Read-only preview. Current-time values are recalculated on Apply.</p><p v-for="s in preview.summaries" :key="s.tableName"><strong>{{ s.tableName }}</strong> · {{ s.countsText }} · {{ s.displayText }}</p><div class="scroll"><table><thead><tr><th>Table</th><th>Action</th><th>Key</th><th>Current values</th><th>Proposed values</th></tr></thead><tbody><tr v-for="(r, i) in preview.rows" :key="i"><td>{{ r.tableName }}</td><td>{{ r.action }}</td><td>{{ r.key }}</td><td>{{ r.currentValues }}</td><td>{{ r.proposedValues }}</td></tr></tbody></table></div></section>
  </main>
</template>
