<script setup lang="ts">
import { ref } from 'vue'
import { errMsg, type Vehicle, type VehicleInput } from '../appApi'
import { fmtKm, fuelLabel } from '../format'
import Icon from './Icon.vue'
import VehicleDialog from './VehicleDialog.vue'

// Las operaciones llegan como funciones para poder reutilizar este bloque tal cual
// en el área de cliente (sobre la propia cuenta) y en administración (sobre otra).
const props = defineProps<{
  vehicles: Vehicle[]
  add: (v: VehicleInput) => Promise<void>
  update: (id: string, v: VehicleInput) => Promise<void>
  remove: (id: string) => Promise<void>
  max?: number
}>()

const open = ref(false)
const editing = ref<Vehicle | null>(null)
const busy = ref(false)
const error = ref('')
const confirmId = ref('')
const removing = ref('')
const listError = ref('')

function start(vehicle: Vehicle | null) {
  editing.value = vehicle
  error.value = ''
  open.value = true
}

async function save(input: VehicleInput) {
  busy.value = true
  error.value = ''
  try {
    if (editing.value) await props.update(editing.value.id, input)
    else await props.add(input)
    open.value = false
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    busy.value = false
  }
}

async function confirmRemove(id: string) {
  removing.value = id
  listError.value = ''
  try {
    await props.remove(id)
    confirmId.value = ''
  } catch (e) {
    listError.value = errMsg(e)
  } finally {
    removing.value = ''
  }
}

const specs = (v: Vehicle) => [
  v.year ? String(v.year) : '',
  fmtKm(v.km),
  fuelLabel(v.fuel),
  v.vin ? `VIN ${v.vin}` : ''
].filter(Boolean)
</script>

<template>
  <div>
    <div class="block-head">
      <h2>Vehículos</h2>
      <button type="button" class="chip-btn" :disabled="max !== undefined && vehicles.length >= max" @click="start(null)">
        <Icon name="plus" :size="16" />Añadir vehículo
      </button>
    </div>

    <p v-if="listError" class="form-error" role="alert">{{ listError }}</p>

    <div v-if="!vehicles.length" class="card empty-card">
      <Icon name="car" :size="28" />
      <p>Todavía no hay ningún vehículo guardado.</p>
      <button type="button" class="btn btn-primary" @click="start(null)"><Icon name="plus" :size="18" />Añadir el primero</button>
    </div>

    <ul v-else class="veh-list">
      <li v-for="v in vehicles" :key="v.id" class="veh-card">
        <div class="veh-head">
          <span class="veh-ico"><Icon name="car" :size="20" /></span>
          <div>
            <p class="veh-name">{{ v.make }} {{ v.model }}</p>
            <span class="veh-plate">{{ v.plate }}</span>
          </div>
        </div>
        <ul v-if="specs(v).length" class="veh-specs">
          <li v-for="s in specs(v)" :key="s">{{ s }}</li>
        </ul>
        <div class="veh-actions">
          <button type="button" class="mini" @click="start(v)"><Icon name="edit" :size="14" />Editar</button>
          <template v-if="confirmId !== v.id">
            <button type="button" class="mini danger" @click="confirmId = v.id"><Icon name="trash" :size="14" />Borrar</button>
          </template>
          <template v-else>
            <button type="button" class="mini danger" :disabled="removing === v.id" @click="confirmRemove(v.id)">
              {{ removing === v.id ? 'Borrando…' : 'Confirmar' }}
            </button>
            <button type="button" class="mini" @click="confirmId = ''">Cancelar</button>
          </template>
        </div>
      </li>
    </ul>

    <p v-if="max !== undefined && vehicles.length >= max" class="note">
      Has alcanzado el máximo de {{ max }} vehículos guardados.
    </p>

    <VehicleDialog :open="open" :vehicle="editing" :busy="busy" :error="error"
      @save="save" @close="open = false" />
  </div>
</template>
