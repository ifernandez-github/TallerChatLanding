<script setup lang="ts" generic="T extends string">
import { computed } from 'vue'
import Icon from './Icon.vue'

const props = defineProps<{ modelValue: T; options: { value: T; label: string; icon?: string }[]; label: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: T] }>()
const index = computed(() => Math.max(0, props.options.findIndex((o) => o.value === props.modelValue)))
</script>

<template>
  <div class="segmented" role="radiogroup" :aria-label="label" :style="{ '--n': options.length, '--idx': index }">
    <span class="seg-pill" aria-hidden="true" />
    <button v-for="o in options" :key="o.value" type="button" role="radio" :aria-checked="o.value === modelValue"
      :class="{ on: o.value === modelValue }" @click="emit('update:modelValue', o.value)">
      <Icon v-if="o.icon" :name="o.icon" :size="16" /><span>{{ o.label }}</span>
    </button>
  </div>
</template>
