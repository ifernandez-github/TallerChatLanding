<script setup lang="ts">
import { computed } from 'vue'
import { useCountUp } from '../composables/useCountUp'

const props = defineProps<{ value: number; decimals?: number; suffix?: string; signed?: boolean }>()
const shown = useCountUp(() => props.value, props.decimals ?? 0)
const text = computed(() => {
  const s = new Intl.NumberFormat('es-ES', { minimumFractionDigits: props.decimals ?? 0, maximumFractionDigits: props.decimals ?? 0 }).format(shown.value)
  return `${props.signed && shown.value > 0 ? '+' : ''}${s}${props.suffix ?? ''}`
})
</script>

<template><b class="anim-num">{{ text }}</b></template>
