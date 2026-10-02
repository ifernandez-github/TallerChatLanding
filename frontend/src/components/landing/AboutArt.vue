<script setup lang="ts">
// Composición de engranajes (el "diente" se dibuja con un trazo discontinuo) y una llave inglesa.
</script>

<template>
  <svg viewBox="0 0 400 400" role="img" aria-label="Ilustración de engranajes y una llave inglesa">
    <g class="gear" opacity=".95">
      <circle cx="170" cy="215" r="108" fill="none" stroke="#2ec4b0" stroke-width="26" stroke-dasharray="20 12.3" />
      <circle cx="170" cy="215" r="94" fill="#0b2a33" stroke="#2ec4b0" stroke-width="6" />
      <circle cx="170" cy="215" r="28" fill="none" stroke="#2ec4b0" stroke-width="6" />
      <path d="M170 187v-78M170 243v78M142 215H64M198 215h78M150 195l-55-55M190 235l55 55M190 195l55-55M150 235l-55 55" stroke="#2ec4b0" stroke-width="5" stroke-linecap="round" opacity=".55" />
    </g>
    <g class="gear rev">
      <circle cx="306" cy="104" r="62" fill="none" stroke="#ffb703" stroke-width="18" stroke-dasharray="18 14.5" />
      <circle cx="306" cy="104" r="52" fill="#101c24" stroke="#ffb703" stroke-width="5" />
      <circle cx="306" cy="104" r="14" fill="none" stroke="#ffb703" stroke-width="5" />
    </g>
    <g class="gear">
      <circle cx="318" cy="302" r="44" fill="none" stroke="#6ff5df" stroke-width="14" stroke-dasharray="14 11.8" opacity=".85" />
      <circle cx="318" cy="302" r="36" fill="#0b2a33" stroke="#6ff5df" stroke-width="4" opacity=".85" />
    </g>
    <g transform="rotate(-38 200 250)" opacity=".95">
      <rect x="188" y="196" width="24" height="170" rx="12" fill="#e8f0f3" />
      <circle cx="200" cy="192" r="34" fill="#e8f0f3" />
      <rect x="190" y="150" width="20" height="38" fill="#0a1c24" />
      <circle cx="200" cy="348" r="9" fill="#0a1c24" />
    </g>
  </svg>
</template>
