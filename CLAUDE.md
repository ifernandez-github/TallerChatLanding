# TallerChat

App web de un taller: landing, chatbot sobre la base de conocimiento, gestión de citas y cuentas de cliente.

- **Chatbot**: el usuario pregunta en lenguaje natural; la API busca fragmentos en MongoDB
  (`taller_db.conocimiento`) y Gemini redacta la respuesta usando solo esos fragmentos, citándolos como [1], [2]...
- **Citas**: los clientes reservan día y hora desde la web; el taller las confirma o cancela desde el panel de
  administración. Todo se guarda en `taller_db.appointments` (MongoDB Atlas).
- **Usuarios**: cuenta con email y contraseña (`taller_db.users`), con teléfono, dirección y sus vehículos; un cliente
  ve y cancela sus propias citas, edita sus datos y gestiona sus vehículos; un administrador gestiona la agenda
  (con calendario de ocupación), las fichas de cliente y sus vehículos.

## Estructura
- `src/TallerChat.Api`: .NET 10 Minimal API.
  - `Endpoints/`: `ChatEndpoints`, `EmailEndpoints` (chat), `AuthEndpoints`, `ProfileEndpoints` (datos y vehículos del
    cliente, con `UserAdmin` compartido), `AppointmentEndpoints`, `AdminEndpoints`.
  - `Services/`: `KnowledgeService`/`GeminiClient` (chat); `BookingService`, `PasswordHasher`, `Validation`, `Catalog`,
    `Notifier`, `AppointmentEmails`, `ReminderService`, `StartupService`, `AuthSetup` (citas y usuarios).
  - `Data/`: `Entities.cs` (documentos Mongo: `AppUser` con `Address` y `Vehicle[]`, `Appointment`), `Db.cs`
    (colecciones e índices), `Dtos.cs` (DTOs y los `Mappers` que convierten entidad → DTO).
  - `Models.cs`, `Options.cs`: modelos y opciones de configuración (chat y citas).
- `frontend/`: Vue 3 + Vite + TypeScript + Vue Router.
  - `views/`: páginas (inicio, servicios, detalle de cada servicio, quiénes somos, contacto, pedir cita, acceso,
    mi cuenta, administración).
  - `components/VehicleDialog.vue` y `VehicleManager.vue`: alta/edición/borrado de vehículos, reutilizados tal cual
    por el área de cliente y por la ficha de administración (las operaciones llegan como props de función).
  - `components/landing/`: secciones de la portada. `components/`: cabecera, pie, icono y chat flotante.
  - `composables/`: `useAuth` (sesión), `useChat` (chat flotante), `useReveal` (animación al hacer scroll), `useTheme`.
  - `appApi.ts`: cliente de la API de usuarios/citas. `api.ts`: cliente de la API del chat.
  - `content/site.ts`: todos los textos y datos del taller, incluido el contenido de cada página de servicio
    (`ServiceSpec`: foto, entradilla, por qué importa, periodicidad y aviso). `router.ts`: rutas y guardas de sesión/rol.
  - `public/img/`: `hero.jpg` (portada) y `services/<id>.jpg`, una por servicio. Las fotos de servicio ya llevan
    rotulado el nombre del servicio, por eso la cabecera de esas páginas NO superpone ningún texto encima.
  - El build sale a `src/TallerChat.Api/wwwroot`.
- `scripts/mongo-setup.js`: índice de texto de `conocimiento` y usuario de solo lectura para el chat. Los índices de
  `users` y `appointments` los crea la propia API al arrancar (`StartupService`, ver `Data/Db.cs`).

## Comandos
- API: `dotnet run --project src/TallerChat.Api` (http://localhost:5080)
- Web: `cd frontend && npm install && npm run dev` (http://localhost:5173, proxy de /api)
- Build web: `cd frontend && npm run build`
- Tests: `dotnet test` (cuando existan)

## Reglas
### General
- Endpoints con `MapGroup`; una funcionalidad = una carpeta/archivo de endpoints.
- `IMongoClient` e `IMongoDatabase` son singletons; nunca crear `MongoClient` por petición. `Db` (colecciones de
  usuarios/citas) también es singleton.
- Secretos solo en user-secrets o variables de entorno (`Mongo__ConnectionString`, `Gemini__ApiKey`, `Smtp__*`,
  `Seed__AdminEmail`, `Seed__AdminPassword`). Nunca en git.
- No registrar (log) cadenas de conexión, claves, contraseñas ni direcciones de email.
- Español en textos de UI, mensajes y comentarios.
- Diseño del cliente: solo tokens CSS de `style.css`/`app.css` (modo claro/oscuro con `data-theme`); iconos en
  `Icon.vue`; sin librerías de UI ni fuentes externas. Animaciones: curvas y duraciones de `app.css`
  (`--ease-out`/`--ease-in-out`), nada por encima de 300 ms en la UI, hover solo bajo `(hover: hover)`, y siempre con
  `prefers-reduced-motion`.
- Paleta clara: la portada es foto a la derecha y texto a la izquierda (nada de texto sobre foto, que con fotos
  claras pierde legibilidad). La marca se escribe siempre con `site.brand` (`Taller` + `Torque` en color de marca).

### Chatbot
- La API solo LEE de `conocimiento`. No añadir escrituras sin pedirlo.
- Toda llamada a Gemini pasa por `GeminiClient` (reintentos con backoff, modelos de respaldo y tiempos máximos).
- El LLM nunca responde sin fragmentos: si `$text` no devuelve nada, se responde sin llamar a Gemini.
- El frontend no usa `v-html`; las citas [n] se convierten en botones desde el texto plano.
- Cambios en el esquema de `conocimiento`: actualizar `KnowledgeChunk`, `Sections()` y el índice de texto a la vez.
- Email del chat: nunca registrar ni guardar direcciones; asunto y plantilla fijos; todo se codifica en HTML
  (`EmailTemplate`); mantener validaciones, límite por IP y enfriamiento por destinatario en `EmailEndpoints`.
- El chat se abre/cierra con `composables/useChat.ts`; cualquier botón de la web puede llamar a `openChat()`.

### Citas y usuarios
- Confirmación de cuenta y contraseña olvidada: los enlaces se emiten y se gastan SIEMPRE por `TokenService`
  (token aleatorio, solo se guarda el hash, un uso, con caducidad). Nunca guardar el token en claro ni reutilizarlo.
- `/api/auth/forgot` y `/api/auth/resend` deben responder exactamente igual exista o no la cuenta: no se puede
  revelar qué correos están registrados. El enfriamiento por destinatario se consume ANTES de consultar la base de
  datos, para que el tiempo de respuesta tampoco lo delate.
- Cambiar la contraseña rota `SecurityStamp`, que viaja también en la cookie: así caen las sesiones de otros
  dispositivos. Tras tocar el estado de un usuario hay que dejar el valor nuevo en `IMemoryCache`
  (`AuthEndpoints.Refresh`), no solo borrar la entrada.
- Contraseñas: siempre `PasswordHasher.Hash`/`Verify` (PBKDF2); nunca comparar en texto plano. `Login` verifica
  contra `PasswordHasher.Dummy` cuando el usuario no existe, para no delatar por tiempo de respuesta si el correo
  está registrado.
- Sesión por cookie HttpOnly del mismo origen (`AuthSetup`); el frontend nunca guarda tokens en `localStorage`.
- Catálogo de servicios reservables: `Services/Catalog.cs`. Sus identificadores deben coincidir siempre con el campo
  `icon` de `services` en `frontend/src/content/site.ts`, con el nombre del icono en `Icon.vue` y con el nombre del
  archivo en `public/img/services/`.
- Vehículos: viven dentro del documento del usuario (`AppUser.Vehicles`), no en una colección aparte. La cita guarda
  una COPIA de matrícula y descripción (`Plate`, `Vehicle`) además del `VehicleId`, para que borrar o editar un
  vehículo no altere el historial ni los correos ya enviados.
- Toda la edición de ficha (datos y vehículos) pasa por `UserAdmin`, que devuelve el usuario completo ya actualizado;
  la web sustituye su estado con esa respuesta en vez de recargar.
- Validación de datos de cliente y vehículo: siempre en `Services/Validation.cs`, nunca suelta en los endpoints.
- Una cita ocupa un hueco (franja + elevador). La doble reserva se evita con el índice único parcial
  `appointments_slot_bay_unique` (`Data/Db.cs`), no solo con comprobaciones en memoria.
- Los emails de citas (solicitud, confirmación, cancelación, recordatorio) los genera siempre `AppointmentEmails`
  en el servidor; el cliente nunca envía el asunto ni el cuerpo. Un fallo de SMTP (`Notifier`) nunca debe romper la
  reserva, cancelación o cambio de estado que lo origina.
- Una solicitud nueva o una cancelación (`AppointmentEndpoints.BookAsync`/`CancelAsync`) también avisan a la
  administración con `Notifier.NotifyAdminsAsync`, independientemente del correo al cliente y sin que un fallo en
  uno afecte al otro. Va a todas las cuentas `Role == Admin` activas y con el correo confirmado; antes de enviar,
  `ResolveAdminAddress` sustituye las que usan `Shop:PlaceholderDomain` (dominio de ejemplo, no un buzón real) por
  `Smtp:From`, y las direcciones resultantes se deduplican antes de mandar nada.
- El enlace de esos avisos (`AppointmentEmails.AdminLink`) usa `BookingService.LocalDateIso`, NUNCA la fecha UTC de
  `Appointment.Start`: el día que abre en `/admin?fecha=…` tiene que ser el mismo día local en el que esa cita
  aparece en el calendario.
- "Enviarme mis citas por email" (`/api/appointments/mine/email`) va siempre al correo de la cuenta autenticada;
  nunca a una dirección que envíe el cliente.
- Cambios de estado de una cita solo a través de las transiciones de `AdminEndpoints.Transitions` (pendiente →
  confirmada/cancelada, confirmada → completada/cancelada).
- Al desactivar una cuenta (`/api/admin/users/{id}/active`), limpiar su entrada de `IMemoryCache`
  (`AuthSetup.CacheKey`) para que la sesión deje de valer de inmediato.
- Borrar una cuenta pasa SIEMPRE por `AccountDeletion` (no borrar usuarios suelto desde un endpoint). Orden fijo:
  citas → tokens → documento del usuario. Sin transacción: si algo falla a mitad, ese orden deja los huecos libres y
  la cuenta todavía borrable, en vez de citas huérfanas que nadie puede cancelar.
- Tras borrar hay que dejar un `UserState(false, "", "")` en `IMemoryCache` (no solo quitar la entrada), igual que en
  `AuthEndpoints.Refresh`: si no, una petición en vuelo puede volver a cachear un estado activo durante un minuto.
- Las bajas se serializan con un `SemaphoreSlim` dentro de `AccountDeletion`, porque comprobar "¿queda otro
  administrador?" y borrar tienen que ocurrir juntos.
- `IsLastAdminAsync` solo cuenta administradores **activos y con el correo confirmado**: los demás no superan el
  inicio de sesión, así que contarlos dejaría la administración inaccesible. La misma comprobación bloquea degradar
  y desactivar al último administrador, no solo borrarlo.
- La baja propia exige la contraseña y la de administración exige teclear el correo exacto de la cuenta: la cookie
  de sesión por sí sola nunca basta para una acción irreversible.
- `@demo.taller` está reservado a los datos de ejemplo: `Notifier.Deliverable` lo descarta y el registro lo rechaza
  (si no, esa cuenta nunca recibiría el enlace de confirmación y quedaría bloqueada).
- Datos de ejemplo (`Seed:Demo`, ver `StartupService`): usuarios `@demo.taller`; `Notifier` nunca les envía correos
  (devuelve `SendResult.Skipped`, que `ReminderService` distingue de `Failed` para no reintentar en cada pasada).
- El calendario de administración cuenta `Statuses.Booked` (incluye las completadas) para que un día pasado y lleno
  no aparezca como libre; la disponibilidad de reserva usa `Statuses.Active`.
- `MapFallbackToFile` está limitado a las rutas que NO empiezan por `api/`: una ruta de API inexistente debe devolver
  404, nunca el HTML del SPA.
