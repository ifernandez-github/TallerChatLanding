// Todo el contenido de la web vive aquí. DATOS DE EJEMPLO: sustituir por los reales.

export interface ServiceSpec {
  /** Identificador: debe coincidir con Services/Catalog.cs del backend y con el icono de Icon.vue. */
  icon: string
  title: string
  /** Resumen corto para la tarjeta de la portada. */
  text: string
  /** Foto de cabecera (en frontend/public/img/services). Ya lleva rotulado el nombre del servicio. */
  image: string
  /** Entradilla de la página de detalle. */
  lead: string
  /** Por qué importa: cada punto es un titular + explicación. */
  why: { title: string; text: string }[]
  /** Cada cuánto se hace. Vacío en los servicios que no son de mantenimiento periódico. */
  periodicity: { label: string; value: string }[]
  /** Aviso final de la página (seguridad, normativa o consejo). */
  note: string
}

export const site = {
  name: 'Taller Torque',
  brand: { first: 'Taller', second: 'Torque' },
  tagline: 'Mecánica de precisión. Diagnóstico honesto.',
  intro: 'Reparamos y mantenemos tu coche con equipos de diagnóstico actuales, repuestos de calidad y presupuestos claros antes de tocar nada.',
  eyebrow: 'Taller multimarca en Madrid',

  // Fotos reales. Si se vacían, se usan las ilustraciones SVG de respaldo.
  heroImage: '/img/hero.jpg' as string,
  aboutImage: '' as string,

  heroPoints: ['Presupuesto antes de empezar', 'Garantía de 24 meses', 'Cita en 24 horas'],
  heroBadge: { value: '4,9/5', label: 'valoración media de 1.200 clientes' },

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
    {
      icon: 'oil',
      title: 'Mantenimiento y aceite',
      text: 'Cambio de aceite y filtros, niveles y revisiones según el plan del fabricante.',
      image: '/img/services/oil.jpg',
      lead: 'El mantenimiento periódico es lo más barato que le puedes hacer a tu coche y lo que más vida útil le da. Un aceite en buen estado y unos filtros limpios evitan averías que cuestan diez veces más que la revisión.',
      why: [
        { title: 'El aceite se degrada aunque no conduzcas', text: 'Pierde propiedades por oxidación y por los restos de combustión. Un aceite agotado deja de lubricar bien el turbo y los cojinetes, que son las piezas más caras del motor.' },
        { title: 'Los filtros protegen lo que no se ve', text: 'El filtro de aire evita que entre suciedad a los cilindros; el de combustible protege los inyectores; el de habitáculo es el que respiras tú.' },
        { title: 'Mantiene la garantía y el valor de reventa', text: 'Un historial de mantenimiento al día conserva la garantía del fabricante y es lo primero que mira un comprador de segunda mano.' }
      ],
      periodicity: [
        { label: 'Aceite y filtro de aceite', value: 'Cada 15.000 km o 1 año (hasta 30.000 km con aceite long-life)' },
        { label: 'Filtro de aire', value: 'Cada 30.000 km, o antes en zonas con mucho polvo' },
        { label: 'Filtro de habitáculo', value: 'Cada 15.000–20.000 km o una vez al año' },
        { label: 'Filtro de combustible', value: 'Cada 40.000–60.000 km (diésel, según fabricante)' },
        { label: 'Bujías (gasolina)', value: 'Cada 30.000–60.000 km según tipo' }
      ],
      note: 'Respetamos siempre el plan de mantenimiento y la especificación de aceite de tu fabricante; te la indicamos en la factura junto con el siguiente intervalo.'
    },
    {
      icon: 'brake',
      title: 'Frenos',
      text: 'Pastillas, discos, líquido y revisión completa del sistema para frenar con seguridad.',
      image: '/img/services/brake.jpg',
      lead: 'El sistema de frenos es el elemento de seguridad activa más importante del coche. No avisa con antelación: cuando notas que frena peor, el margen de seguridad ya se ha reducido.',
      why: [
        { title: 'La distancia de frenado se dispara', text: 'Unas pastillas al límite o unos discos rayados alargan varios metros la frenada de emergencia, justo cuando más falta hacen.' },
        { title: 'El líquido absorbe agua con el tiempo', text: 'Es higroscópico: con humedad hierve a menor temperatura y, en una bajada larga, el pedal puede irse al fondo. Por eso se cambia por tiempo, no por kilómetros.' },
        { title: 'Un desgaste desigual indica otra avería', text: 'Si un lado se gasta más, suele haber una pinza agarrotada o una guía seca. Detectarlo a tiempo evita tener que cambiar el disco y la pinza.' }
      ],
      periodicity: [
        { label: 'Revisión del sistema', value: 'En cada mantenimiento o cada 15.000 km' },
        { label: 'Pastillas delanteras', value: 'Cada 30.000–50.000 km según conducción' },
        { label: 'Discos', value: 'Cada dos juegos de pastillas o 60.000–80.000 km' },
        { label: 'Líquido de frenos', value: 'Cada 2 años, independientemente del kilometraje' }
      ],
      note: 'Si notas el pedal esponjoso, vibraciones al frenar o el coche se va hacia un lado, no esperes a la próxima revisión: tráelo y lo miramos el mismo día.'
    },
    {
      icon: 'scan',
      title: 'Diagnosis electrónica',
      text: 'Lectura de averías con equipos actuales y comprobación de sensores y centralitas.',
      image: '/img/services/scan.jpg',
      lead: 'Un coche actual lleva decenas de centralitas hablando entre sí. Cuando se enciende un testigo, el código de avería es solo el punto de partida: lo importante es interpretar los datos en vivo y confirmar la causa antes de cambiar ninguna pieza.',
      why: [
        { title: 'Evita cambiar piezas a ciegas', text: 'Un código de sonda lambda no siempre significa sonda averiada: puede ser una entrada de aire o un inyector. Medimos antes de sustituir.' },
        { title: 'Detecta problemas antes del testigo', text: 'La memoria de averías guarda fallos esporádicos que todavía no encienden ninguna luz. Leerla en cada revisión adelanta reparaciones pequeñas.' },
        { title: 'Necesario tras muchas reparaciones', text: 'Cambiar una batería, una pastilla de freno electrónica o una suspensión exige recalibrar o reaprender valores con el equipo de diagnosis.' }
      ],
      periodicity: [
        { label: 'Lectura de memoria de averías', value: 'En cada revisión anual, incluida en el mantenimiento' },
        { label: 'Diagnosis bajo demanda', value: 'Siempre que se encienda un testigo o notes un comportamiento raro' }
      ],
      note: 'Si se enciende el testigo de motor en naranja puedes circular con precaución hasta el taller. Si se enciende en rojo o parpadeando, detén el coche en un lugar seguro y llámanos.'
    },
    {
      icon: 'belt',
      title: 'Distribución y embrague',
      text: 'Sustitución de correa o cadena de distribución y de embrague con piezas de calidad.',
      image: '/img/services/belt.jpg',
      lead: 'La correa de distribución es la pieza cuyo fallo sale más caro de todo el coche. Si se rompe en marcha, los pistones golpean las válvulas y el motor se destruye por dentro. Se cambia por prevención, nunca por síntoma.',
      why: [
        { title: 'No avisa antes de romperse', text: 'La correa se degrada por dentro, por calor y por antigüedad. Por eso el intervalo cuenta también los años, no solo los kilómetros.' },
        { title: 'Se cambia el kit completo', text: 'Junto a la correa se sustituyen rodillos, tensor y, si la arrastra la correa, la bomba de agua. Montar solo la correa obliga a repetir toda la mano de obra en poco tiempo.' },
        { title: 'El embrague se mide por uso, no por km', text: 'Depende mucho de la conducción. Patinamiento al acelerar en marchas largas, olor a quemado o un punto de agarre muy alto son los avisos.' }
      ],
      periodicity: [
        { label: 'Correa de distribución', value: 'Entre 100.000 y 160.000 km, o cada 5–7 años (lo que ocurra antes)' },
        { label: 'Kit completo con bomba de agua', value: 'En la misma intervención que la correa' },
        { label: 'Cadena de distribución', value: 'No tiene intervalo fijo: se revisa tensor y ruido a partir de 150.000 km' },
        { label: 'Embrague', value: 'Por desgaste; habitualmente entre 120.000 y 200.000 km' }
      ],
      note: 'Consulta siempre el intervalo exacto de tu motor: dos coches de la misma marca pueden tener plazos muy distintos. Te lo confirmamos con el número de bastidor.'
    },
    {
      icon: 'wheel',
      title: 'Neumáticos y alineado',
      text: 'Montaje, equilibrado, geometría y revisión de la suspensión y la dirección.',
      image: '/img/services/wheel.jpg',
      lead: 'Los neumáticos son el único contacto del coche con el asfalto: cuatro superficies del tamaño de un folio. Su estado y la geometría de las ruedas deciden cómo frena, cómo agarra en mojado y cuánto consume.',
      why: [
        { title: 'La presión correcta ahorra y protege', text: 'Un neumático con poca presión se calienta, se desgasta por los hombros, aumenta el consumo y puede reventar en autovía.' },
        { title: 'La geometría se descuadra con un bordillo', text: 'Un golpe basta para desalinear la dirección. El coche tira hacia un lado y los neumáticos se comen por dentro o por fuera en pocos miles de kilómetros.' },
        { title: 'La goma envejece aunque no ruede', text: 'Con los años se endurece y pierde adherencia en mojado, incluso con dibujo suficiente. Comprobamos la fecha de fabricación grabada en el flanco.' }
      ],
      periodicity: [
        { label: 'Presión de inflado', value: 'Revisar una vez al mes y antes de cada viaje largo' },
        { label: 'Rotación de neumáticos', value: 'Cada 10.000 km para igualar el desgaste' },
        { label: 'Alineado y equilibrado', value: 'Cada 20.000 km, al montar neumáticos nuevos o tras un golpe' },
        { label: 'Sustitución por dibujo', value: 'Mínimo legal 1,6 mm; recomendamos cambiar a partir de 3 mm' },
        { label: 'Sustitución por antigüedad', value: 'A partir de 6 años, aunque conserven dibujo' }
      ],
      note: 'Montamos siempre los neumáticos más nuevos en el eje trasero, aunque el coche sea de tracción delantera: es lo que recomienda el fabricante para evitar que el tren trasero pierda agarre en mojado.'
    },
    {
      icon: 'snow',
      title: 'Aire acondicionado',
      text: 'Carga, detección de fugas, desinfección y cambio del filtro de habitáculo.',
      image: '/img/services/snow.jpg',
      lead: 'El aire acondicionado no es solo confort: es el sistema que desempaña el parabrisas en invierno y el que filtra lo que respiras dentro del coche. Pierde gas de forma natural, así que necesita revisión aunque nunca se haya averiado.',
      why: [
        { title: 'Pierde gas todos los años', text: 'El circuito pierde en torno a un 10 % de refrigerante al año por las juntas. Con poca carga, el compresor trabaja mal lubricado y acaba gripándose.' },
        { title: 'Es clave para la seguridad en invierno', text: 'El aire acondicionado seca el aire: es lo que quita el vaho del parabrisas en segundos. Sin él, la visibilidad en días húmedos empeora mucho.' },
        { title: 'Los olores son bacterias en el evaporador', text: 'La humedad del evaporador acumula hongos y bacterias que acaban en el habitáculo. Se resuelve con desinfección del circuito y filtro nuevo.' }
      ],
      periodicity: [
        { label: 'Revisión de rendimiento', value: 'Una vez al año, preferiblemente antes del verano' },
        { label: 'Filtro de habitáculo', value: 'Cada 15.000 km o una vez al año' },
        { label: 'Recarga de gas', value: 'Cada 2–3 años, o antes si enfría menos' },
        { label: 'Desinfección del circuito', value: 'Cuando aparecen olores, y como mantenimiento cada 2 años' }
      ],
      note: 'Si el aire enfría menos de lo normal, no lo dejes para el verano: una fuga pequeña detectada a tiempo evita tener que sustituir el compresor.'
    },
    {
      icon: 'bolt',
      title: 'Eléctricos e híbridos',
      text: 'Mantenimiento de alta tensión con protocolos de desenergización y seguridad.',
      image: '/img/services/bolt.jpg',
      lead: 'Un eléctrico o un híbrido necesita menos mantenimiento mecánico, pero el que necesita es más exigente: trabajar en circuitos de cientos de voltios requiere formación específica, herramienta aislada y un procedimiento de desenergización estricto.',
      why: [
        { title: 'La alta tensión no admite improvisación', text: 'Antes de tocar nada se desconecta y se verifica ausencia de tensión siguiendo el protocolo del fabricante. Es un trabajo para técnicos con certificación.' },
        { title: 'Los frenos duran más pero se oxidan', text: 'Con la frenada regenerativa las pastillas apenas se usan, y precisamente por eso discos y guías tienden a oxidarse. Hay que limpiarlos y engrasarlos periódicamente.' },
        { title: 'La batería tiene su propio circuito térmico', text: 'La refrigeración de la batería es lo que conserva su capacidad con los años. Su líquido y su circuito tienen un intervalo propio de revisión.' }
      ],
      periodicity: [
        { label: 'Revisión general', value: 'Cada 12 meses o 30.000 km' },
        { label: 'Líquido de frenos', value: 'Cada 2 años, igual que en un térmico' },
        { label: 'Filtro de habitáculo', value: 'Una vez al año' },
        { label: 'Circuito de refrigeración de la batería', value: 'Según fabricante, habitualmente entre 4 y 8 años' },
        { label: 'Aceite de la reductora', value: 'Según fabricante; muchos modelos no lo requieren' }
      ],
      note: 'Trabajamos con protocolos de desenergización y equipos de protección específicos para alta tensión. Nunca manipules por tu cuenta los cables naranjas del vehículo.'
    },
    {
      icon: 'check-list',
      title: 'Pre-ITV y revisión',
      text: 'Repasamos los puntos que se inspeccionan para que pases a la primera.',
      image: '/img/services/check-list.jpg',
      lead: 'Volver de la ITV con un desfavorable cuesta tiempo, otra cita y otra tasa. Revisamos antes los mismos puntos que mira el inspector y te decimos exactamente qué hay que corregir para pasar a la primera.',
      why: [
        { title: 'La mayoría de rechazos son evitables', text: 'Luces mal reguladas, holguras en la dirección, emisiones y neumáticos concentran casi todos los desfavorables. Son fallos baratos de corregir si se detectan antes.' },
        { title: 'Circular con la ITV caducada se sanciona', text: 'Además de la multa, un seguro puede discutir la cobertura en caso de siniestro si la inspección no estaba en vigor.' },
        { title: 'Te damos el informe por escrito', text: 'Sales del taller sabiendo qué está correcto, qué conviene vigilar y qué hay que arreglar sí o sí antes de la inspección.' }
      ],
      periodicity: [
        { label: 'Primera ITV (turismo gasolina o diésel)', value: 'A los 4 años desde la matriculación' },
        { label: 'De 4 a 10 años', value: 'Cada 2 años' },
        { label: 'A partir de 10 años', value: 'Todos los años' },
        { label: 'Pre-ITV en el taller', value: 'Unos días antes de cada cita de inspección' }
      ],
      note: 'Si tu vehículo es furgoneta, está destinado a transporte de personas o es un clásico, los plazos cambian. Dinos la matrícula y te confirmamos cuándo te toca.'
    }
  ] as ServiceSpec[],

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

export const findService = (id?: string | string[]) =>
  site.services.find((s) => s.icon === id)
