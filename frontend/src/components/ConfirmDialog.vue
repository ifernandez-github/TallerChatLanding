<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import Icon from './Icon.vue'

/**
 * Aviso de confirmación para acciones que no se pueden deshacer.
 * Enumera lo que se va a perder y obliga a escribir algo (la contraseña, o el correo de la cuenta)
 * para que no baste con un clic por inercia.
 */
const props = defineProps<{
  open: boolean
  title: string
  intro: string
  items: string[]
  /** Etiqueta del campo que hay que rellenar para confirmar. */
  requireLabel: string
  requireHint?: string
  requirePassword?: boolean
  /** Si se indica, el texto escrito debe coincidir exactamente (sin distinguir mayúsculas). */
  expected?: string
  confirmLabel: string
  busy: boolean
  error: string
}>()
const emit = defineEmits<{ confirm: [string]; close: [] }>()

// Ids propios de cada instancia: puede haber más de un diálogo montado en la misma página.
const uid = useId()
const titleId = `${uid}-title`
const introId = `${uid}-intro`
const hintId = `${uid}-hint`

const el = ref<HTMLDialogElement | null>(null)
const value = ref('')

watch(() => props.open, (open) => {
  value.value = '' // nunca se queda escrita una contraseña en el DOM tras cerrar
  if (open) {
    if (!el.value?.open) el.value?.showModal()
  } else if (el.value?.open) {
    el.value.close()
  }
})

const ready = computed(() => {
  const v = value.value.trim()
  if (!v) return false
  return props.expected ? v.toLowerCase() === props.expected.trim().toLowerCase() : true
})

function submit() {
  if (ready.value && !props.busy) emit('confirm', value.value)
}
</script>

<template>
  <dialog ref="el" class="sheet danger-sheet" :aria-labelledby="titleId" :aria-describedby="introId"
    @cancel.prevent="emit('close')" @close="emit('close')">
    <form class="sheet-scroll" @submit.prevent="submit">
      <div class="sheet-head">
        <h2 :id="titleId"><Icon name="trash" :size="18" />{{ title }}</h2>
        <button type="button" class="icon-btn" aria-label="Cerrar" @click="emit('close')"><Icon name="close" /></button>
      </div>

      <p :id="introId" class="danger-intro">{{ intro }}</p>

      <ul v-if="items.length" class="danger-list">
        <li v-for="item in items" :key="item"><Icon name="close" :size="14" />{{ item }}</li>
      </ul>

      <p class="danger-warn">Esta acción no se puede deshacer.</p>

      <label class="field">{{ requireLabel }}
        <!-- El foco empieza aquí y no en el aspa de cerrar: lo primero que hay que hacer es escribir. -->
        <input v-model="value" :type="requirePassword ? 'password' : 'text'"
          :autocomplete="requirePassword ? 'current-password' : 'off'"
          :maxlength="requirePassword ? 100 : 254" :aria-describedby="requireHint ? hintId : undefined"
          autofocus autocapitalize="off" autocorrect="off" spellcheck="false" required />
      </label>
      <p v-if="requireHint" :id="hintId" class="note">{{ requireHint }}</p>

      <p v-if="error" class="form-error" role="alert">{{ error }}</p>

      <div class="danger-actions">
        <button type="button" class="btn btn-outline" @click="emit('close')">Cancelar</button>
        <button class="primary danger-btn" type="submit" :disabled="busy || !ready"
          :aria-describedby="requireHint ? hintId : undefined">
          {{ busy ? 'Eliminando…' : confirmLabel }}
        </button>
      </div>
    </form>
  </dialog>
</template>
