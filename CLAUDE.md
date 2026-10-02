# TallerChat

Chatbot web para un taller. El usuario pregunta en lenguaje natural; la API busca fragmentos en MongoDB
(`taller_db.conocimiento`) y Gemini redacta la respuesta usando solo esos fragmentos, citándolos como [1], [2]...

## Estructura
- `src/TallerChat.Api`: .NET 10 Minimal API (`Program.cs`, `Endpoints/`, `Services/`, `Models.cs`).
- `frontend/`: Vue 3 + Vite + TypeScript. Landing (`components/landing/`) + chat flotante (`components/ChatWidget.vue`).
  Contenido en `src/content/site.ts`. El build sale a `src/TallerChat.Api/wwwroot`.
- `scripts/mongo-setup.js`: índice de texto y usuario de solo lectura.

## Comandos
- API: `dotnet run --project src/TallerChat.Api` (http://localhost:5080)
- Web: `cd frontend && npm install && npm run dev` (http://localhost:5173, proxy de /api)
- Build web: `cd frontend && npm run build`
- Tests: `dotnet test` (cuando existan)

## Reglas
- Endpoints con `MapGroup`; una funcionalidad = una carpeta/archivo de endpoints.
- `IMongoClient` y `IMongoDatabase` son singletons; nunca crear `MongoClient` por petición.
- La API solo LEE de MongoDB. No añadir escrituras sin pedirlo.
- Toda llamada a Gemini pasa por `GeminiClient` (reintentos con backoff, modelos de respaldo y tiempos máximos).
- El LLM nunca responde sin fragmentos: si `$text` no devuelve nada, se responde sin llamar a Gemini.
- No registrar (log) cadenas de conexión, claves ni el contenido completo de las preguntas.
- Secretos solo en user-secrets o variables de entorno (`Mongo__ConnectionString`, `Gemini__ApiKey`). Nunca en git.
- El frontend no usa `v-html`; las citas [n] se convierten en botones desde el texto plano.
- Cambios en el esquema de `conocimiento`: actualizar `KnowledgeChunk`, `Sections()` y el índice de texto a la vez.
- Español en textos de UI, mensajes y comentarios.
- Email: nunca registrar ni guardar direcciones; asunto y plantilla fijos; todo se codifica en HTML (`EmailTemplate`);
  mantener validaciones, límite por IP y enfriamiento por destinatario en `EmailEndpoints`.
- Diseño del cliente: solo tokens CSS de `style.css` (modo claro/oscuro con `data-theme`); iconos en `Icon.vue`;
  sin librerías de UI ni fuentes externas.
- Landing: todo texto/dato del taller va en `content/site.ts`; animaciones solo CSS y respetando `prefers-reduced-motion`;
  el mapa de Google se carga solo tras un clic del usuario.
- El chat se abre/cierra con `composables/useChat.ts`; cualquier botón de la web puede llamar a `openChat()`.
