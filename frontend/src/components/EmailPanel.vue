<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { sendEmail, type ChatMsg } from '../api'
import Icon from './Icon.vue'

const MAX = 5
const props = defineProps<{ pairs: ChatMsg[]; initial: number }>()
const emit = defineEmits<{ close: [] }>()

const dlg = ref<HTMLDialogElement | null>(null)
const selected = ref<Set<number>>(new Set(props.initial >= 0 ? [props.initial] : []))
const email = ref('')
const status = ref<'idle' | 'sending' | 'sent' | 'error'>('idle')
const error = ref('')

const canSend = computed(() => status.value !== 'sending' && selected.value.size > 0 && selected.value.size <= MAX && /^\S+@\S+\.\S{2,}$/.test(email.value))

onMounted(() => dlg.value?.showModal())

function toggle(i: number) {
  const next = new Set(selected.value)
  if (next.has(i)) next.delete(i)
  else if (next.size < MAX) next.add(i)
  selected.value = next
}
function selectAll() {
  selected.value = new Set(props.pairs.map((_, i) => i).slice(0, MAX))
}

async function submit() {
  if (!canSend.value) return
  status.value = 'sending'
  try {
    const items = [...selected.value].sort((a, b) => a - b).map((i) => ({
      question: props.pairs[i].question ?? '',
      answer: props.pairs[i].text,
      sources: props.pairs[i].sources ?? []
    }))
    await sendEmail(email.value.trim(), items)
    status.value = 'sent'
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'No se pudo enviar el correo.'
    status.value = 'error'
  }
}
</script>

<template>
  <dialog ref="dlg" class="sheet" aria-labelledby="mail-title" @close="emit('close')" @click.self="dlg?.close()">
    <form @submit.prevent="submit">
      <div class="sheet-head">
        <h2 id="mail-title">Enviar por email</h2>
        <button type="button" class="icon-btn" aria-label="Cerrar" @click="dlg?.close()"><Icon name="close" /></button>
      </div>

      <div v-if="status === 'sent'" class="done">
        <span class="done-mark"><Icon name="check" :size="26" /></span>
        <p>Enviado. Revisa tu bandeja de entrada y, si no aparece, la carpeta de spam.</p>
        <button type="button" class="primary" @click="dlg?.close()">Cerrar</button>
      </div>

      <template v-else>
        <fieldset>
          <legend>Qué incluir <span class="muted">(máximo {{ MAX }})</span></legend>
          <label v-for="(p, i) in pairs" :key="i" class="opt">
            <input type="checkbox" :checked="selected.has(i)" @change="toggle(i)" />
            <span>{{ p.question }}</span>
          </label>
          <button v-if="pairs.length > 1" type="button" class="link" @click="selectAll">Seleccionar todas</button>
        </fieldset>

        <label class="field">
          <span>Tu correo electrónico</span>
          <input v-model.trim="email" type="email" required autocomplete="email" maxlength="254" placeholder="nombre@ejemplo.com" />
        </label>
        <p class="note">Solo usamos tu dirección para enviar este correo. No la guardamos.</p>

        <p v-if="status === 'error'" class="form-error" role="alert">{{ error }}</p>
        <button type="submit" class="primary" :disabled="!canSend">
          <Icon name="send" :size="16" />{{ status === 'sending' ? 'Enviando…' : 'Enviar correo' }}
        </button>
      </template>
    </form>
  </dialog>
</template>
