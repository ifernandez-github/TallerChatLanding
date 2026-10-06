# TallerChat

Web de un taller con tres partes: landing, chatbot sobre la base de conocimiento y gestión de citas/usuarios.

- **Chatbot**: pregunta → la API busca con `$text` en MongoDB (top 5) → Gemini redacta la respuesta usando SOLO esos
  fragmentos y los cita como [n] → la web muestra la respuesta y las fuentes. Si MongoDB no devuelve nada, no se
  llama al modelo y se avisa al usuario.
- **Citas**: el cliente elige servicio, día y hora en `/cita`; la solicitud queda "pendiente de confirmar" y el taller
  la confirma o cancela desde `/admin`. Cada cambio de estado envía un email (con el evento adjunto en `.ics`).
- **Cuentas**: registro con email y contraseña; cada cliente ve sus citas (próximas e historial) en `/mi-cuenta`,
  puede cancelarlas y reenviárselas por email. Todo se guarda en MongoDB Atlas.

## 1. Requisitos
- Visual Studio Community 2026 (carga de trabajo "ASP.NET y desarrollo web") o .NET SDK 10
- Node.js 20+ (para Vue)
- Un clúster de MongoDB Atlas (o cualquier MongoDB 6+) con usuario y contraseña
- Clave de API de Gemini (Google AI Studio → "Get API key")

## 2. Preparar MongoDB
Los índices de `users` y `appointments` los crea la propia API al arrancar (ver `Data/Db.cs`); no hace falta
ejecutar nada para ellos. Solo la búsqueda del chatbot necesita un índice de texto, que sí hay que crear a mano
(una colección solo admite uno):

```
mongosh "mongodb+srv://ADMIN:CLAVE@tu-cluster.mongodb.net/admin" scripts/mongo-setup.js
```

Crea el índice `conocimiento_text` (español) y un usuario `chat_reader` de solo lectura sobre `conocimiento`. Si ya
tienes un usuario o un índice de texto, adapta el script (`tdb.conocimiento.getIndexes()`).

**Usuario de la app**: la API necesita además un usuario con permisos de lectura y escritura sobre `taller_db` (para
`users` y `appointments`). En Atlas: *Database Access* → *Add New Database User* → rol `readWrite` sobre
`taller_db`. Puedes usar un único usuario con `readWrite` para todo, o mantener `chat_reader` solo para el chat y
crear otro para citas/usuarios.

Cadena de conexión de Atlas (codifica en URL los caracteres especiales de la contraseña):
```
mongodb+srv://usuario:CLAVE@tu-cluster.mongodb.net/taller_db?retryWrites=true&w=majority
```

## 3. Configurar secretos
```
cd src/TallerChat.Api
dotnet user-secrets set "Mongo:ConnectionString" "mongodb+srv://usuario:CLAVE@tu-cluster.mongodb.net/taller_db?retryWrites=true&w=majority"
dotnet user-secrets set "Gemini:ApiKey" "TU_CLAVE"
```
En servidor: variables de entorno `Mongo__ConnectionString` y `Gemini__ApiKey` (y las de abajo si aplican).

Los modelos de Gemini se configuran en `appsettings.json` → `Gemini:Models`, por orden de preferencia. La API
reintenta cada modelo con espera exponencial ante errores transitorios (429, 5xx, timeout; respeta `Retry-After`) y,
si el modelo no existe o tu clave no tiene acceso (403/404), pasa al siguiente. Con `Gemini:MaxRetries`,
`RetryBaseDelayMs`, `AttemptTimeoutSeconds` y `TotalTimeoutSeconds` ajustas reintentos y tiempos. En desarrollo,
`GET /api/models` lista los modelos que admite tu clave.

### Administrador inicial
```
dotnet user-secrets set "Seed:AdminEmail" "admin@tutaller.com"
dotnet user-secrets set "Seed:AdminPassword" "UnaClaveLargaConNumeros123"
```
Si ambos valores son válidos (la contraseña necesita 10+ caracteres con letras y números) y no existe ya esa
cuenta, la API crea el administrador la primera vez que arranca. Inicia sesión en `/acceso` con esas credenciales
para llegar a `/admin`.

### Datos de ejemplo (opcional, solo para probar)
`appsettings.Development.json` trae `Seed:Demo = true`: crea 4 clientes de ejemplo (`nombre.apellido@demo.taller`,
contraseña `Demo1234!`) con un historial de citas variado (completadas, canceladas, confirmadas y pendientes).
`Notifier` nunca envía correos a direcciones `@demo.taller`. Para producción, pon `Seed:Demo` a `false` (o quítalo:
el valor por defecto ya es `false` en `appsettings.json`).

## 4. Ejecutar en desarrollo
1. Abre `TallerChat.slnx` en Visual Studio, proyecto de inicio `TallerChat.Api`, perfil `http` (F5).
   Prueba con `chat.http` o abre http://localhost:5080/openapi/v1.json.
2. En otra terminal:
```
cd frontend
npm install
npm run dev
```
Abre http://localhost:5173 (Vite redirige `/api` a la API).

## Páginas de la web
- `/` — landing (hero, quiénes somos, servicios, contacto) con el mecánico interactivo.
- `/servicios`, `/nosotros`, `/contacto` — mismas secciones como páginas independientes (enlazables y para SEO).
- `/cita` — reserva de cita: servicio → día y hora disponibles → datos del vehículo → confirmación.
- `/acceso` — iniciar sesión o crear cuenta.
- `/mi-cuenta` — próximas citas (cancelar, reenviar por email), historial y datos de la cuenta. Requiere sesión.
- `/admin` — agenda del día (o solo pendientes) con acciones de confirmar/cancelar/completar, y gestión de usuarios
  (activar/desactivar). Requiere sesión de administrador.

**Personalizar (todo en `frontend/src/content/site.ts`, son datos de ejemplo):** nombre, textos, servicios,
estadísticas, teléfono, email, horario y dirección. La ubicación del mapa sale de `address.lat` y `address.lng`.
Actualiza también el JSON-LD de `frontend/index.html` (SEO local) para que coincida.

**Horario y reglas de la agenda** (`appsettings.json` → `Booking`): zona horaria, duración de cada franja
(`SlotMinutes`), número de elevadores/puestos simultáneos (`Bays`), antelación mínima y máxima, horario de apertura
entre semana y sábados, y máximo de citas activas por cliente.

**Datos del taller en los correos** (`appsettings.json` → `Shop`): nombre, dirección, teléfono y la URL pública de
la web (el botón "Ver mis citas" de los emails).

**Imágenes:** el hero y "Quiénes somos" usan ilustraciones SVG propias (`HeroScene.vue`, `AboutArt.vue`). Para usar
fotos reales, copia los archivos a `frontend/public/img/` y rellena `heroImage` y `aboutImage` en `site.ts`.

## Envío por email
### Configurar SMTP
```
cd src/TallerChat.Api
dotnet user-secrets set "Smtp:Host" "smtp.tudominio.com"
dotnet user-secrets set "Smtp:User" "usuario"
dotnet user-secrets set "Smtp:Password" "CLAVE"
dotnet user-secrets set "Smtp:From" "citas@tudominio.com"
```
- Puerto 587 con STARTTLS (por defecto, `Smtp:Security: Auto`) o 465 con SSL. Con Gmail hace falta una contraseña
  de aplicación (verificación en dos pasos); Microsoft 365 suele tener desactivada la autenticación SMTP básica.
- Para que los correos no caigan en spam, usa un remitente de tu dominio con SPF y DKIM configurados.
- Pruebas locales sin enviar nada real: smtp4dev o MailHog con `Smtp:Security: "None"` y su puerto.
- Si `Smtp:Host` o `Smtp:From` están vacíos, todo el envío de emails queda desactivado (chat y citas): la web oculta
  el botón del chat y, al reservar o cancelar una cita, simplemente no se envía el correo (la cita se crea igual).

### Qué correos se envían
- **Chat** (`/api/email`): el visitante elige qué respuestas incluir (hasta 5) y escribe su dirección; asunto y
  plantilla fijos, protegido con límite por IP y enfriamiento por destinatario (`EmailEndpoints`).
- **Citas**: solicitud recibida, confirmación (con `.ics` adjunto), cancelación y recordatorio 24 h antes
  (`ReminderService`, revisa cada 15 minutos). Siempre al correo de la cuenta; el cliente nunca elige el
  destinatario ni el contenido (`AppointmentEmails`, `Notifier`).
- **"Enviarme mis citas"** (desde `/mi-cuenta`): un resumen de las próximas citas, al correo de la cuenta.

## 5. Publicar (un solo servicio)
```
cd frontend && npm run build          # genera src/TallerChat.Api/wwwroot
dotnet publish src/TallerChat.Api -c Release -o out
```
La API sirve la web y `/api/*` desde el mismo origen. Si la pones detrás de un proxy inverso, configura
`ForwardedHeaders` para que el límite de peticiones y la cookie segura usen la IP/esquema reales.

## 6. Ajustes habituales
| Qué | Dónde |
|---|---|
| Nº de fragmentos enviados al modelo (chat) | `Mongo:MaxResults` |
| Peso de cada campo en la búsqueda (chat) | `weights` en `scripts/mongo-setup.js` |
| Instrucciones del modelo (chat) | `SystemPrompt` en `Services/GeminiClient.cs` |
| Reintentos, tiempos y modelos de Gemini | `Gemini:*` en `appsettings.json` |
| Horario, franjas, elevadores y antelación | `Booking:*` en `appsettings.json` |
| Datos del taller en los correos | `Shop:*` en `appsettings.json` |
| Catálogo de servicios reservables | `Services/Catalog.cs` (ids deben coincidir con `site.ts`) |
| Administrador inicial / datos de ejemplo | `Seed:*` en user-secrets / `appsettings.Development.json` |
| Límite de envíos de email del chat (3/hora por IP) | política `email` en `Program.cs` |
| Límite de reservas (10/hora por usuario) y "enviarme mis citas" (3/hora) | políticas `booking`/`mailme` en `Program.cs` |
| Límite de login/registro (10/min por IP) | política `auth` en `Program.cs` |
| Límite de peticiones al chat (10/min por IP) | política `chat` en `Program.cs` |
| Longitud máxima de pregunta del chat (500) | `Endpoints/ChatEndpoints.cs` |

## 7. Problemas frecuentes
- `text index required`: falta el índice del chat; ejecuta `scripts/mongo-setup.js`.
- `Authentication failed` (Mongo): revisa el usuario/contraseña, `authSource` (en Atlas no suele hacer falta) y la
  codificación de caracteres especiales en la contraseña.
- Siempre "No he encontrado información" en el chat: la búsqueda `$text` no admite sinónimos; añade términos a
  `tags` o a los campos indexados.
- `404` de Gemini con un modelo concreto: Google limita el acceso a ciertos modelos; pon uno de `/api/models`
  primero en `Gemini:Models`.
- El botón de email del chat no aparece, o no se reciben emails de citas: falta `Smtp:Host` o `Smtp:From`.
- Un email falla (502) o no llega: mira el log de la API (solo muestra el tipo de excepción, sin direcciones);
  revisa host, puerto, `Smtp:Security` y credenciales.
- "Ya tienes una cita a esa hora" / "Ese hueco acaba de ocuparse": el índice único de `appointments` está
  funcionando como se espera (evita dobles reservas); el cliente debe elegir otra franja.
- No se crea el administrador: revisa que `Seed:AdminEmail` sea un correo válido y `Seed:AdminPassword` tenga 10+
  caracteres con letras y números; el log de arranque avisa si los valores no son válidos.

## 8. Seguridad y privacidad
- Contraseñas con PBKDF2-SHA256 (210.000 iteraciones) y sal aleatoria; nunca en texto plano ni en los logs.
- Sesión por cookie HttpOnly, `SameSite=Lax`, del mismo origen que la API; sin tokens en `localStorage`.
- El chat solo lee de MongoDB con un usuario de mínimo privilegio; secretos fuera del repositorio.
- No se usa `v-html`; el texto del modelo se pinta como texto plano.
- El nivel gratuito de Gemini puede usar los datos enviados para mejorar los productos de Google (revisa sus
  condiciones): no incluyas en la base de conocimiento datos personales ni confidenciales.
- El asistente orienta con la información del taller; no sustituye el diagnóstico de un mecánico en sistemas
  críticos (frenos, alta tensión).
