<template>
  <section class="page">
    <div class="lp-wrap narrow empty-state enter">
      <p class="eyebrow">Error 404</p>
      <h1 class="page-title">No encontramos esa página</h1>
      <p class="lp-lead">Puede que el enlace haya cambiado. Vuelve al inicio o pide tu cita.</p>
      <div class="row-gap">
        <RouterLink to="/" class="btn btn-primary">Ir al inicio</RouterLink>
        <RouterLink to="/cita" class="btn btn-outline">Pedir cita</RouterLink>
      </div>
    </div>
  </section>
</template>
