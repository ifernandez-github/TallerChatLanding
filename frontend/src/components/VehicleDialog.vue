<script setup lang="ts">
import { ref, watch } from 'vue'
import type { Vehicle, VehicleInput } from '../appApi'
import { fuels } from '../format'
import Icon from './Icon.vue'

const props = defineProps<{ open: boolean; vehicle: Vehicle | null; busy: boolean; error: string }>()
const emit = defineEmits<{ save: [VehicleInput]; close: [] }>()

const el = ref<HTMLDialogElement | null>(null)
const form = ref<VehicleInput>({ make: '', model: '', plate: '', year: null, km: null, fuel: '', vin: '' })

watch(() => props.open, (open) => {
  if (open) {
    const v = props.vehicle
    form.value = {
      make: v?.make ?? '', model: v?.model ?? '', plate: v?.plate ?? '',
      year: v?.year ?? null, km: v?.km ?? null, fuel: v?.fuel ?? '', vin: v?.vin ?? ''
    }
    if (!el.value?.open) el.value?.showModal()
  } else if (el.value?.open) {
    el.value.close()
  }
})

// Los números vacíos deben viajar como null, no como 0 ni como cadena.
const num = (v: unknown) => (v === '' || v === null || v === undefined ? null : Number(v))

function submit() {
  emit('save', {
    ...form.value,
    year: num(form.value.year),
    km: num(form.value.km),
    fuel: form.value.fuel || null,
    vin: form.value.vin?.trim() || null
  })
}
</script>

<template>
  <dialog ref="el" class="sheet" @cancel.prevent="emit('close')" @close="emit('close')">
    <form class="sheet-scroll" @submit.prevent="submit">
      <div class="sheet-head">
        <h2>{{ vehicle ? 'Editar vehículo' : 'Añadir vehículo' }}</h2>
        <button type="button" class="icon-btn" aria-label="Cerrar" @click="emit('close')"><Icon name="close" /></button>
      </div>

      <div class="grid-2">
        <label class="field">Marca
          <input v-model="form.make" type="text" maxlength="40" required placeholder="Seat" />
        </label>
        <label class="field">Modelo
          <input v-model="form.model" type="text" maxlength="40" required placeholder="León 1.5 TSI" />
        </label>
      </div>
      <label class="field">Matrícula
        <input v-model="form.plate" type="text" maxlength="12" required autocapitalize="characters" placeholder="1234ABC" />
      </label>
      <div class="grid-3">
        <label class="field">Año <span class="muted">(opcional)</span>
          <input v-model="form.year" type="number" min="1900" :max="new Date().getFullYear() + 1" placeholder="2021" />
        </label>
        <label class="field">Kilómetros <span class="muted">(opcional)</span>
          <input v-model="form.km" type="number" min="0" max="2000000" placeholder="52400" />
        </label>
        <label class="field">Combustible <span class="muted">(opcional)</span>
          <select v-model="form.fuel">
            <option value="">Sin indicar</option>
            <option v-for="f in fuels" :key="f.value" :value="f.value">{{ f.label }}</option>
          </select>
        </label>
      </div>
      <label class="field">VIN / bastidor <span class="muted">(opcional)</span>
        <input v-model="form.vin" type="text" maxlength="17" autocapitalize="characters" placeholder="VSSZZZ5FZMR123456" />
      </label>
      <p class="note">El VIN tiene 17 caracteres y no incluye las letras I, O ni Q. Lo encuentras en la ficha técnica.</p>

      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <button class="primary" type="submit" :disabled="busy">{{ busy ? 'Guardando…' : 'Guardar vehículo' }}</button>
    </form>
  </dialog>
</template>
