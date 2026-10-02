<script setup lang="ts">
// Ilustración original del taller (SVG): pared con herramientas, neón, puente elevador y coche.
// Las capas .l1–.l4 se mueven a distinta velocidad con el scroll (parallax) mediante --py.
defineProps<{ sign: string }>()
</script>

<template>
  <svg class="hero-scene" viewBox="0 0 1600 900" preserveAspectRatio="xMidYMid slice" role="img" aria-label="Coche elevado en un puente elevador dentro de un taller iluminado con neones">
    <defs>
      <linearGradient id="hs-wall" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0c1d28" /><stop offset="1" stop-color="#050c11" /></linearGradient>
      <linearGradient id="hs-floor" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0e1f2a" /><stop offset="1" stop-color="#02060a" /></linearGradient>
      <linearGradient id="hs-car" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#4a6f85" /><stop offset=".5" stop-color="#172a35" /><stop offset="1" stop-color="#091218" /></linearGradient>
      <linearGradient id="hs-glass" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#8fd8ea" stop-opacity=".55" /><stop offset="1" stop-color="#0d2530" stop-opacity=".92" /></linearGradient>
      <linearGradient id="hs-cone" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#c9fff6" stop-opacity=".26" /><stop offset="1" stop-color="#c9fff6" stop-opacity="0" /></linearGradient>
      <linearGradient id="hs-beam" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#fff3c4" stop-opacity=".5" /><stop offset="1" stop-color="#fff3c4" stop-opacity="0" /></linearGradient>
      <pattern id="hs-panels" width="200" height="180" patternUnits="userSpaceOnUse"><path d="M0 0H200V180" fill="none" stroke="#fff" stroke-opacity=".045" /></pattern>
      <pattern id="hs-dots" width="20" height="20" patternUnits="userSpaceOnUse"><circle cx="10" cy="10" r="1.6" fill="#000" fill-opacity=".45" /></pattern>
      <filter id="hs-blur" x="-30%" y="-60%" width="160%" height="220%"><feGaussianBlur stdDeviation="16" /></filter>
      <filter id="hs-neon" x="-20%" y="-60%" width="140%" height="220%"><feGaussianBlur stdDeviation="7" result="b" /><feMerge><feMergeNode in="b" /><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge></filter>
      <g id="hs-wheel">
        <circle r="46" fill="#070d11" />
        <circle r="31" fill="#1b2a33" stroke="#3b5566" stroke-width="3" />
        <path d="M0-27V27M-23-13.5 23 13.5M-23 13.5 23-13.5" stroke="#4c6a7a" stroke-width="5" stroke-linecap="round" />
        <circle r="8" fill="#0a1217" />
      </g>
    </defs>

    <rect width="1600" height="900" fill="url(#hs-wall)" />

    <!-- L1: pared trasera, herramientas, neón y luces de techo -->
    <g class="l1">
      <rect width="1600" height="730" fill="url(#hs-panels)" />
      <g fill="url(#hs-cone)" opacity=".7">
        <polygon points="180,44 440,44 540,730 80,730" />
        <polygon points="670,44 930,44 1030,730 570,730" />
        <polygon points="1160,44 1420,44 1520,730 1060,730" />
      </g>
      <g fill="#dffcf8"><rect x="180" y="34" width="260" height="10" rx="5" /><rect x="670" y="34" width="260" height="10" rx="5" /><rect x="1160" y="34" width="260" height="10" rx="5" /></g>

      <g transform="translate(60 170)">
        <rect width="330" height="300" rx="14" fill="#0e202b" stroke="#233a48" />
        <rect width="330" height="300" rx="14" fill="url(#hs-dots)" />
        <g fill="#35566a">
          <rect x="40" y="70" width="16" height="170" rx="8" /><circle cx="48" cy="62" r="22" /><rect x="42" y="36" width="12" height="24" fill="#0e202b" />
          <g transform="rotate(14 120 150)"><rect x="112" y="70" width="16" height="170" rx="8" /><circle cx="120" cy="62" r="22" /><rect x="114" y="36" width="12" height="24" fill="#0e202b" /></g>
          <rect x="190" y="80" width="12" height="150" rx="5" /><rect x="166" y="60" width="60" height="26" rx="5" />
        </g>
        <rect x="250" y="70" width="16" height="44" rx="5" fill="#ffb703" /><rect x="255" y="114" width="6" height="116" fill="#8aa1ad" />
        <rect x="285" y="70" width="16" height="44" rx="5" fill="#2ec4b0" /><rect x="290" y="114" width="6" height="116" fill="#8aa1ad" />
      </g>

      <g transform="translate(1230 190)">
        <g fill="#1d3442"><rect y="100" width="320" height="8" rx="3" /><rect y="210" width="320" height="8" rx="3" /></g>
        <rect x="20" y="40" width="46" height="60" rx="7" fill="#14323d" /><rect x="20" y="58" width="46" height="14" fill="#ffb703" opacity=".85" />
        <rect x="80" y="40" width="46" height="60" rx="7" fill="#14323d" /><rect x="80" y="58" width="46" height="14" fill="#2ec4b0" opacity=".85" />
        <rect x="140" y="52" width="40" height="48" rx="6" fill="#14323d" /><rect x="140" y="66" width="40" height="10" fill="#ffb703" opacity=".6" />
        <rect x="30" y="150" width="90" height="60" rx="5" fill="#112a35" /><rect x="140" y="170" width="70" height="40" rx="5" fill="#13303c" /><rect x="226" y="140" width="70" height="70" rx="5" fill="#112a35" />
      </g>

      <g class="sign" filter="url(#hs-neon)">
        <text x="800" y="200" text-anchor="middle" font-family="Bahnschrift, 'Arial Black', Impact, sans-serif" font-size="78" font-weight="800" textLength="760" lengthAdjust="spacingAndGlyphs" fill="#6ff5df">{{ sign }}</text>
        <rect x="520" y="228" width="560" height="6" rx="3" fill="#ffb703" />
      </g>
    </g>

    <!-- Suelo y reflejo del neón -->
    <rect y="720" width="1600" height="180" fill="url(#hs-floor)" />
    <ellipse cx="800" cy="765" rx="520" ry="34" fill="#2ec4b0" opacity=".2" filter="url(#hs-blur)" />

    <!-- L2: puente elevador -->
    <g class="l2">
      <rect x="430" y="280" width="36" height="445" rx="4" fill="#1b2e3a" stroke="#2c4757" />
      <rect x="1136" y="280" width="36" height="445" rx="4" fill="#1b2e3a" stroke="#2c4757" />
      <rect x="418" y="330" width="60" height="70" rx="6" fill="#223a48" /><rect x="1124" y="330" width="60" height="70" rx="6" fill="#223a48" />
      <rect x="466" y="582" width="170" height="14" rx="5" fill="#26404f" /><rect x="964" y="582" width="172" height="14" rx="5" fill="#26404f" />
      <rect x="404" y="716" width="88" height="14" rx="3" fill="#13222c" /><rect x="1110" y="716" width="88" height="14" rx="3" fill="#13222c" />
      <rect x="404" y="722" width="88" height="6" fill="#ffb703" opacity=".85" /><rect x="1110" y="722" width="88" height="6" fill="#ffb703" opacity=".85" />
    </g>

    <!-- L3: coche -->
    <g class="l3">
      <g transform="translate(0 -70)">
        <ellipse cx="800" cy="672" rx="340" ry="16" fill="#2ec4b0" opacity=".6" filter="url(#hs-blur)" />
        <polygon points="1105,598 1560,520 1560,720 1105,624" fill="url(#hs-beam)" />
        <path d="M480 650 L480 622 C480 604 495 594 520 590 L650 576 C690 524 735 500 800 497 L915 497 C955 500 985 540 1008 574 L1085 590 C1108 595 1120 608 1120 626 L1120 650 Z" fill="url(#hs-car)" stroke="#fff" stroke-opacity=".12" />
        <path d="M668 574 C700 534 738 512 800 509 L905 509 C935 512 958 544 975 574 Z" fill="url(#hs-glass)" />
        <path d="M838 509 V574" stroke="#0a141a" stroke-width="6" />
        <path d="M520 603 H1090" stroke="#fff" stroke-opacity=".22" stroke-width="2" />
        <rect x="740" y="586" width="34" height="6" rx="3" fill="#0a141a" opacity=".7" />
        <ellipse cx="1100" cy="606" rx="14" ry="7" fill="#fff6d0" /><ellipse cx="1100" cy="606" rx="30" ry="14" fill="#fff6d0" opacity=".35" filter="url(#hs-blur)" />
        <rect x="482" y="598" width="18" height="9" rx="3" fill="#ff4d4d" /><ellipse cx="486" cy="603" rx="20" ry="10" fill="#ff4d4d" opacity=".35" filter="url(#hs-blur)" />
        <use href="#hs-wheel" transform="translate(590 655)" /><use href="#hs-wheel" transform="translate(1010 655)" />
      </g>
    </g>

    <!-- L4: primer plano -->
    <g class="l4">
      <g transform="translate(70 700)">
        <rect x="10" y="-22" width="240" height="26" rx="8" fill="#14262f" stroke="#223846" />
        <rect width="260" height="150" rx="12" fill="#0f1c25" stroke="#223846" />
        <rect x="14" y="20" width="232" height="34" rx="6" fill="#0b151c" stroke="#223846" /><rect x="14" y="62" width="232" height="34" rx="6" fill="#0b151c" stroke="#223846" /><rect x="14" y="104" width="232" height="34" rx="6" fill="#0b151c" stroke="#223846" />
        <rect x="100" y="33" width="60" height="7" rx="3" fill="#ffb703" /><rect x="100" y="75" width="60" height="7" rx="3" fill="#ffb703" /><rect x="100" y="117" width="60" height="7" rx="3" fill="#ffb703" />
      </g>
      <g transform="translate(1410 830)">
        <rect x="-120" y="-72" width="240" height="72" fill="#0a1217" /><ellipse cx="0" cy="0" rx="120" ry="30" fill="#0a1217" stroke="#1d3340" />
        <path d="M-120 -36Q0 -6 120 -36" fill="none" stroke="#1d3340" stroke-width="2" />
        <ellipse cx="0" cy="-72" rx="120" ry="30" fill="#0d171d" stroke="#1d3340" /><ellipse cx="0" cy="-72" rx="62" ry="15" fill="#182a35" /><ellipse cx="0" cy="-72" rx="16" ry="4" fill="#070d11" />
      </g>
    </g>
  </svg>
</template>
