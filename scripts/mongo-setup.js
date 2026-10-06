// Ejecutar con un usuario administrador (en Atlas, "Connect" -> "Shell" te da la cadena exacta):
//   mongosh "mongodb+srv://ADMIN:CLAVE@tu-cluster.mongodb.net/admin" scripts/mongo-setup.js
const tdb = db.getSiblingDB('taller_db');

// Una colección solo admite UN índice de texto. Si ya tienes uno, revisa con: tdb.conocimiento.getIndexes()
tdb.conocimiento.createIndex(
  {
    title: 'text', tags: 'text', category: 'text', sub_category: 'text',
    sintomatologia: 'text', causas_probables: 'text', procedimiento_resolucion: 'text',
    requisitos_previos: 'text', buenas_practicas: 'text', intervalos_servicio: 'text',
    normativas_clave: 'text', pasos_desenergizacion: 'text',
    'especificaciones.cadena_distribucion': 'text', 'especificaciones.correa_distribucion': 'text'
  },
  {
    name: 'conocimiento_text',
    default_language: 'spanish',
    weights: { title: 10, tags: 8, sintomatologia: 5, category: 3, sub_category: 3 }
  }
);

// Usuario de solo lectura para la API (mínimo privilegio).
tdb.createUser({ user: 'chat_reader', pwd: passwordPrompt(), roles: [{ role: 'read', db: 'taller_db' }] });
