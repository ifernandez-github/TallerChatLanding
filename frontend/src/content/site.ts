// Todo el contenido de la web vive aquí. DATOS DE EJEMPLO: sustituir por los reales.
export const site = {
  name: 'Taller Torque',
  tagline: 'Mecánica de precisión. Diagnóstico honesto.',
  intro: 'Reparamos y mantenemos tu coche con equipos de diagnóstico actuales, repuestos de calidad y presupuestos claros antes de tocar nada.',

  // Si pones rutas de fotos reales (en frontend/public/img), sustituyen a las ilustraciones. Ej.: '/img/hero.jpg'
  heroImage: '' as string,
  aboutImage: '' as string,

  heroPoints: ['Presupuesto antes de empezar', 'Garantía de 24 meses', 'Cita en 24 horas'],

  about: {
    lead: 'Un taller de barrio con la tecnología de uno grande.',
    paragraphs: [
      'Somos un equipo de mecánicos que cree que arreglar un coche empieza por explicar bien qué le pasa. Antes de reparar, diagnosticamos, te enseñamos la avería y te damos un presupuesto cerrado.',
      'Trabajamos con turismos de combustión, híbridos y eléctricos, siguiendo los intervalos y procedimientos del fabricante y los protocolos de seguridad que exige cada sistema.'
    ]
  },
  values: [
    { title: 'Transparencia', text: 'Te mostramos la pieza y la avería. Sin sorpresas en la factura.' },
    { title: 'Repuestos con garantía', text: 'Piezas homologadas y 24 meses en mano de obra y recambios.' },
    { title: 'Formación continua', text: 'Técnicos al día en electrónica, alta tensión y nuevas motorizaciones.' }
  ],
  stats: [
    { value: '18', label: 'años de experiencia' },
    { value: '12.000+', label: 'vehículos atendidos' },
    { value: '24 meses', label: 'de garantía' },
    { value: '4,9/5', label: 'valoración media' }
  ],

  services: [
    { icon: 'oil', title: 'Mantenimiento y aceite', text: 'Cambio de aceite y filtros, niveles y revisiones según el plan del fabricante.' },
    { icon: 'brake', title: 'Frenos', text: 'Pastillas, discos, líquido y revisión completa del sistema para frenar con seguridad.' },
    { icon: 'scan', title: 'Diagnosis electrónica', text: 'Lectura de averías con equipos actuales y comprobación de sensores y centralitas.' },
    { icon: 'belt', title: 'Distribución y embrague', text: 'Sustitución de correa o cadena de distribución y de embrague con piezas de calidad.' },
    { icon: 'wheel', title: 'Neumáticos y alineado', text: 'Montaje, equilibrado, geometría y revisión de la suspensión y la dirección.' },
    { icon: 'snow', title: 'Aire acondicionado', text: 'Carga, detección de fugas, desinfección y cambio del filtro de habitáculo.' },
    { icon: 'bolt', title: 'Eléctricos e híbridos', text: 'Mantenimiento de alta tensión con protocolos de desenergización y seguridad.' },
    { icon: 'check-list', title: 'Pre-ITV y revisión', text: 'Repasamos los puntos que se inspeccionan para que pases a la primera.' }
  ],

  contact: {
    phone: '+34 910 000 000',
    phoneHref: 'tel:+34910000000',
    email: 'info@tallertorque.example',
    address: { line: 'Calle del Motor 24', city: '28045 Madrid, España', lat: 40.4066, lng: -3.6935 },
    hours: [
      { days: 'Lunes a viernes', time: '08:30 – 18:30' },
      { days: 'Sábados', time: '09:00 – 13:30' },
      { days: 'Domingos y festivos', time: 'Cerrado' }
    ]
  }
}
