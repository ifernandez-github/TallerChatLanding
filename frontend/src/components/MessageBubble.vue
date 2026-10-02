<script setup lang="ts">
import { computed, ref } from 'vue'
import type { Source } from '../api'
import Icon from './Icon.vue'

const props = defineProps<{ role: 'user' | 'assistant'; text: string; sources?: Source[]; error?: boolean; canEmail?: boolean }>()
const emit = defineEmits<{ email: [] }>()

type Part = { kind: 'cite'; n: number } | { kind: 'text'; text: string }

// Convierte "[1]" del texto en botones que abren la fuente correspondiente (sin v-html).
const parts = computed<Part[]>(() =>
  props.text.split(/(\[\d+\])/g).filter(Boolean).map((p) => {
    const m = /^\[(\d+)\]$/.exec(p)
    return m ? { kind: 'cite', n: Number(m[1]) } : { kind: 'text', text: p }
  })
)

const opened = ref<Set<number>>(new Set())
function show(n: number) {
  opened.value = new Set(opened.value).add(n)
}
function sync(n: number, e: Event) {
  const next = new Set(opened.value)
  if ((e.target as HTMLDetailsElement).open) next.add(n)
  else next.delete(n)
  opened.value = next
}
</script>

<template>
  <article :class="['msg', role, { error }]">
    <div v-if="role === 'assistant'" class="avatar" aria-hidden="true"><Icon name="wrench" :size="18" /></div>
    <div class="body">
      <div class="bubble">
        <p class="text"><template v-for="(p, i) in parts" :key="i"><button v-if="p.kind === 'cite'" type="button" class="cite" :aria-label="`Ver fuente ${p.n}`" @click="show(p.n)">{{ p.n }}</button><template v-else>{{ p.text }}</template></template></p>
      </div>

      <div v-if="canEmail" class="actions">
        <button type="button" class="chip-btn" @click="emit('email')"><Icon name="mail" :size="16" />Enviar por email</button>
      </div>

      <section v-if="sources?.length" class="sources" aria-label="Fuentes consultadas">
        <h2>Fuentes consultadas</h2>
        <details v-for="s in sources" :key="s.chunkId" :open="opened.has(s.ref)" @toggle="sync(s.ref, $event)">
          <summary>
            <span class="ref">{{ s.ref }}</span>
            <span class="meta"><span class="title">{{ s.title }}</span><span class="cat">{{ s.category }} / {{ s.subCategory }}</span></span>
            <Icon name="chevron" :size="16" class="chev" />
          </summary>
          <dl>
            <template v-for="x in s.sections" :key="x.label">
              <dt>{{ x.label }}</dt>
              <dd>{{ x.text }}</dd>
            </template>
          </dl>
          <p v-if="s.tags.length" class="tags"><span v-for="t in s.tags" :key="t">{{ t }}</span></p>
          <p class="id">Referencia: {{ s.chunkId }}</p>
        </details>
      </section>
    </div>
  </article>
</template>
