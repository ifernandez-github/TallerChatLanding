<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ask, getFeatures, type ChatMsg, type Turn } from '../api'
import { useChat } from '../composables/useChat'
import EmailPanel from './EmailPanel.vue'
import Icon from './Icon.vue'
import MessageBubble from './MessageBubble.vue'

const { open, openChat, close } = useChat()

const messages = ref<ChatMsg[]>([])
const input = ref('')
const loading = ref(false)
const emailEnabled = ref(false)
const emailFor = ref<number | null>(null)
const showHint = ref(false)
const scroller = ref<HTMLElement | null>(null)
const box = ref<HTMLTextAreaElement | null>(null)
let hintTimer: number | undefined

const suggestions = [
  '¿Cada cuánto hay que cambiar la correa de distribución?',
  '¿Qué pasos hay que seguir para desenergizar un vehículo eléctrico?',
  'El coche vibra al frenar, ¿qué puede ser?'
]

// Respuestas que se pueden enviar por email (con fuentes y sin error).
const emailable = computed(() => messages.value.filter((m) => m.role === 'assistant' && !m.error && m.sources?.length))

function onKey(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); send() }
}
function onEsc(e: KeyboardEvent) {
  if (e.key === 'Escape' && open.value && emailFor.value === null) close()
}

onMounted(async () => {
  window.addEventListener('keydown', onEsc)
  emailEnabled.value = (await getFeatures()).email
  // Aviso discreto una sola vez si el visitante no ha abierto el chat.
  hintTimer = window.setTimeout(() => { if (!open.value) showHint.value = true }, 7000)
})
onBeforeUnmount(() => { window.removeEventListener('keydown', onEsc); window.clearTimeout(hintTimer) })

watch(open, async (v) => {
  if (!v) return
  showHint.value = false
  await nextTick()
  box.value?.focus()
  scrollDown(false)
})

function autosize() {
  const el = box.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = Math.min(el.scrollHeight, 140) + 'px'
}
async function scrollDown(smooth = true) {
  await nextTick()
  scroller.value?.scrollTo({ top: scroller.value.scrollHeight, behavior: smooth ? 'smooth' : 'auto' })
}

async function send(text: string = input.value) {
  const q = text.trim()
  if (!q || loading.value) return
  const history: Turn[] = messages.value.filter((m) => !m.error).map((m) => ({ role: m.role, text: m.text }))
  messages.value.push({ role: 'user', text: q })
  input.value = ''
  loading.value = true
  await nextTick()
  autosize()
  await scrollDown()
  try {
    const r = await ask(q, history)
    messages.value.push({ role: 'assistant', text: r.answer, sources: r.sources, question: q })
  } catch (e) {
    messages.value.push({ role: 'assistant', text: e instanceof Error ? e.message : 'Error inesperado.', error: true })
  } finally {
    loading.value = false
    await scrollDown()
  }
}

function openEmail(m: ChatMsg) {
  emailFor.value = emailable.value.indexOf(m)
}
</script>

<template>
  <Transition name="pop">
    <button v-if="showHint && !open" type="button" class="cw-hint" @click="openChat">
      ¿Dudas con tu coche? Pregunta al mecánico interactivo.
      <span class="x" role="button" aria-label="Cerrar aviso" @click.stop="showHint = false"><Icon name="close" :size="14" /></span>
    </button>
  </Transition>

  <Transition name="pop">
    <button v-if="!open" type="button" class="cw-launcher" aria-haspopup="dialog" @click="openChat">
      <span class="dot"><Icon name="wrench" :size="20" /></span>
      <span>Mecánico interactivo</span>
    </button>
  </Transition>

  <Transition name="pop">
    <section v-if="open" class="cw-panel" role="dialog" aria-label="Mecánico interactivo">
      <header class="cw-head">
        <span class="logo"><Icon name="wrench" :size="20" /></span>
        <div class="t">
          <h2>Mecánico interactivo</h2>
          <p>Respuestas con la base de conocimiento del taller</p>
        </div>
        <button type="button" class="icon-btn" aria-label="Cerrar el chat" @click="close"><Icon name="close" :size="18" /></button>
      </header>

      <div ref="scroller" class="cw-scroll" aria-live="polite">
        <div class="cw-feed">
          <section v-if="!messages.length" class="cw-welcome">
            <h3>¿En qué te ayudo?</h3>
            <p>Pregunta por un síntoma, una pieza o un procedimiento. Cada respuesta indica su fuente.</p>
            <button v-for="s in suggestions" :key="s" type="button" class="cw-suggest" @click="send(s)">
              <span>{{ s }}</span><Icon name="arrow" :size="16" />
            </button>
          </section>

          <MessageBubble
            v-for="(m, i) in messages"
            :key="i"
            :role="m.role"
            :text="m.text"
            :sources="m.sources"
            :error="m.error"
            :can-email="emailEnabled && !!m.sources?.length"
            @email="openEmail(m)"
          />

          <div v-if="loading" class="msg assistant">
            <div class="avatar" aria-hidden="true"><Icon name="wrench" :size="16" /></div>
            <div class="bubble typing" role="status" aria-label="Buscando en la base de conocimiento"><span /><span /><span /></div>
          </div>
        </div>
      </div>

      <form class="cw-dock" @submit.prevent="send()">
        <div class="composer">
          <label class="sr" for="cw-q">Tu pregunta</label>
          <textarea id="cw-q" ref="box" v-model="input" rows="1" maxlength="500" placeholder="Escribe tu pregunta…" @keydown="onKey" @input="autosize" />
          <button type="submit" class="send" aria-label="Enviar pregunta" :disabled="loading || !input.trim()"><Icon name="send" :size="18" /></button>
        </div>
        <p class="hint">Información orientativa; no sustituye el diagnóstico de un mecánico.</p>
      </form>

      <EmailPanel v-if="emailFor !== null" :pairs="emailable" :initial="emailFor" @close="emailFor = null" />
    </section>
  </Transition>
</template>
