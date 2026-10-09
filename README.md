# TallerChat

Web de un taller con tres partes: landing, chatbot sobre la base de conocimiento y gestión de citas/usuarios.

- **Chatbot**: pregunta → la API busca con `$text` en MongoDB (top 5) → Gemini redacta la respuesta usando SOLO esos
  fragmentos y los cita como [n] → la web muestra la respuesta y las fuentes. Si MongoDB no devuelve nada, no se
  llama al modelo y se avisa al usuario.
- **Citas**: el cliente elige servicio, día y hora en `/cita`; la solicitud queda "pendiente de confirmar" y el taller
  la confirma o cancela desde `/admin`. Cada cambio de estado envía un email (con el evento adjunto en `.ics`).
- **Cuentas**: registro con email (que hay que escribir dos veces) y contraseña, con **confirmación por correo**:
  hasta que el cliente no pulsa el enlace que le llega, su cuenta no puede iniciar sesión. También hay
  **recuperación de contraseña** por enlace de un solo uso. En `/mi-cuenta` cada cliente ve sus citas (próximas e historial),
  las cancela, se las reenvía por email, edita sus datos de contacto y dirección, y gestiona sus vehículos
  (marca, modelo, matrícula, año, kilómetros, combustible y VIN). Todo se guarda en MongoDB Atlas.

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
- `/acceso` — iniciar sesión, crear cuenta o pedir un enlace para recuperar la contraseña.
- `/verificar?token=…` — confirma la cuenta (se abre desde el correo de alta).
- `/restablecer?token=…` — elige una contraseña nueva (se abre desde el correo de recuperación).
- `/mi-cuenta` — próximas citas (cancelar, reenviar por email), historial, datos de la cuenta y la opción de
  eliminarla. Requiere sesión.
- `/servicios/<id>` — página de cada servicio: por qué importa, cada cuánto toca, aviso y botón de "Pedir cita"
  que llega al formulario con ese servicio ya seleccionado.
- `/admin` — calendario mensual de ocupación con código de colores (libre / parcial / completo / cerrado) y el
  número de citas de cada día; al pulsar un día se abre su agenda con las acciones de confirmar, cancelar y
  completar. Segunda pestaña con la ficha completa de cada cliente (datos, dirección, rol y sus vehículos),
  activar/desactivar cuentas y eliminarlas. Requiere sesión de administrador.

**Personalizar (todo en `frontend/src/content/site.ts`, son datos de ejemplo):** nombre y marca (`brand`), textos,
servicios, estadísticas, teléfono, email, horario y dirección. La ubicación del mapa sale de `address.lat` y
`address.lng`. Actualiza también el JSON-LD de `frontend/index.html` (SEO local) para que coincida.

**Contenido de cada servicio:** cada entrada de `services` lleva su página completa (`lead`, `why`, `periodicity`
y `note`). El campo `icon` es el identificador: debe coincidir con `Services/Catalog.cs`, con el icono de
`Icon.vue` y con el nombre del archivo en `public/img/services/`. Para añadir un servicio nuevo hay que tocar
esos cuatro sitios.

**Horario y reglas de la agenda** (`appsettings.json` → `Booking`): zona horaria, duración de cada franja
(`SlotMinutes`), número de elevadores/puestos simultáneos (`Bays`), antelación mínima y máxima, horario de apertura
entre semana y sábados, y máximo de citas activas por cliente.

**Datos del taller en los correos** (`appsettings.json` → `Shop`): nombre, dirección, teléfono y la URL pública de
la web (el botón "Ver mis citas" de los emails).

**Imágenes:** `public/img/hero.jpg` es la foto de portada y `public/img/services/<id>.jpg` la cabecera de cada
servicio. Esas fotos ya llevan rotulado el nombre del servicio, por eso la cabecera no superpone ningún texto
encima. "Quiénes somos" compone un mosaico con cuatro de esas fotos; si prefieres una foto propia, ponla en
`public/img/` y rellena `aboutImage` en `site.ts`. Si `heroImage` se deja vacío, se usa la ilustración SVG de
respaldo (`HeroScene.vue`).

## Envío por email
### Configurar SMTP
`appsettings.json` ya trae los datos no secretos del servidor (`Host`, `Port`, `Security`, `FromName`), apuntando a
Gmail. Solo hay que añadir las credenciales y el remitente a los secretos de usuario:

```
cd src/TallerChat.Api
dotnet user-secrets set "Smtp:User" "tucuenta@gmail.com"
dotnet user-secrets set "Smtp:Password" "CONTRASEÑA_DE_APLICACION_16_CARACTERES"
dotnet user-secrets set "Smtp:From" "tucuenta@gmail.com"
```

**Con Gmail (cuenta personal @gmail.com):**
1. La cuenta necesita la **verificación en dos pasos activada**; sin ella Google no deja crear contraseñas de
   aplicación.
2. Crea una en <https://myaccount.google.com/apppasswords>. Google muestra 16 caracteres en cuatro bloques:
   cópialos **sin espacios**.
3. `Smtp:User` y `Smtp:From` deben ser **la misma dirección de la cuenta**. Gmail rechaza enviar con un remitente
   distinto salvo que sea un alias verificado en "Ver como" / "Send mail as".
4. Esa contraseña se revoca sola si cambias la contraseña principal de la cuenta; entonces hay que generar otra.
5. Las contraseñas de aplicación **no están disponibles** en cuentas de empresa o centro educativo (Workspace, salvo
   que lo permita el administrador), ni con Protección Avanzada, ni si la verificación en dos pasos usa solo llaves
   de seguridad.
6. Límite de envío de una cuenta gratuita: unos 500 correos al día.

**Otros proveedores:** puerto 587 con STARTTLS (`Smtp:Security: "StartTls"`) o 465 con SSL (`"SslOnConnect"`);
`"Auto"` deja que MailKit lo decida por el puerto. Microsoft 365 suele tener desactivada la autenticación SMTP
básica. Para producción con dominio propio compensa un proveedor transaccional (SPF y DKIM ya configurados).

**Pruebas locales sin enviar nada real:** smtp4dev o MailHog, con `Smtp:Host: "localhost"`, su puerto y
`Smtp:Security: "None"`.

**Enlace de los correos:** el botón "Ver mis citas" usa `Shop:PublicBaseUrl`; cámbialo al dominio real al publicar.
- Si `Smtp:Host` o `Smtp:From` están vacíos, todo el envío de emails queda desactivado (chat y citas): la web oculta
  el botón del chat y, al reservar o cancelar una cita, simplemente no se envía el correo (la cita se crea igual).

### Qué correos se envían
- **Chat** (`/api/email`): el visitante elige qué respuestas incluir (hasta 5) y escribe su dirección; asunto y
  plantilla fijos, protegido con límite por IP y enfriamiento por destinatario (`EmailEndpoints`).
- **Citas**: solicitud recibida, confirmación (con `.ics` adjunto), cancelación y recordatorio 24 h antes
  (`ReminderService`, revisa cada 15 minutos). Siempre al correo de la cuenta; el cliente nunca elige el
  destinatario ni el contenido (`AppointmentEmails`, `Notifier`).
- **Aviso a la administración**: cuando un cliente pide una cita o la cancela, todas las cuentas de administración
  activas reciben un correo aparte con un enlace directo a esa cita en la agenda (`/admin?fecha=…&cita=…`, que
  abre ese día y resalta la cita). Si la cuenta de administración usa el dominio de ejemplo del taller
  (`Shop:PlaceholderDomain`, "torque.es" por defecto — no es un buzón real), el aviso se manda en su lugar al
  remitente configurado en `Smtp:From`.
- **"Enviarme mis citas"** (desde `/mi-cuenta`): un resumen de las próximas citas, al correo de la cuenta.
- **Cuenta**: confirmación del alta, restablecimiento de contraseña y aviso de baja (`AccountEmails`). El de baja
  sale cuando el borrado ya ha terminado, y dice si lo ha pedido el propio cliente o el taller.

### Confirmación de cuenta y contraseña olvidada
- Al registrarse, el cliente escribe su correo **dos veces** y recibe un enlace de confirmación. Hasta que lo
  pulsa, el login responde que falta confirmar y ofrece reenviar el correo.
- Los enlaces viajan con un token aleatorio de 32 bytes; en la base de datos (`taller_db.auth_tokens`) solo se
  guarda su hash SHA-256. Son de un solo uso, caducan (`Auth:VerifyTokenHours`, 48 h; `Auth:ResetTokenMinutes`,
  60 min) y MongoDB los borra solo con un índice TTL.
- Al restablecer la contraseña se cambia el sello de seguridad de la cuenta, con lo que **se cierran las sesiones
  abiertas en otros dispositivos**; además la cuenta queda confirmada, porque quien llega ahí ha demostrado tener
  acceso al correo.
- `/api/auth/forgot` y `/api/auth/resend` responden siempre lo mismo exista o no la cuenta, para no revelar qué
  direcciones están registradas, y tienen un enfriamiento por destinatario (`Auth:EmailCooldownMinutes`).
  El alta sí avisa si el correo ya tiene cuenta: es una concesión deliberada a la usabilidad.
- **Sin SMTP configurado**: en desarrollo las cuentas nuevas se dan por confirmadas y se entra directamente (si no,
  no habría forma de usar la aplicación); fuera de desarrollo el registro responde 503 en lugar de saltarse la
  confirmación en silencio.
- Las cuentas creadas antes de esta función se marcan como confirmadas automáticamente al arrancar, así que
  nadie se queda fuera.

### Eliminar una cuenta
Una baja borra para siempre el usuario, **sus vehículos y todas sus citas** (pasadas y futuras) y los enlaces de
correo que tuviera pendientes. No hay papelera ni forma de recuperarlo.

- **El cliente** se da de baja desde `/mi-cuenta` → pestaña "Mis datos". Antes de confirmar ve cuántos vehículos y
  cuántas citas va a perder (y cuántas de esas citas todavía no han pasado) y tiene que **escribir su contraseña**:
  con una sesión olvidada abierta en un ordenador prestado no se puede borrar nada.
- **El taller** borra cualquier cuenta desde `/admin` → pestaña "Clientes" → "Eliminar". El aviso muestra lo mismo,
  contado en ese momento, y hay que **teclear el correo exacto** de la cuenta: así un clic en la fila equivocada no
  borra a quien no toca. El cliente recibe un correo avisándole de la baja.
- **Nunca se puede borrar (ni degradar, ni desactivar) la última cuenta de administración** con la que se pueda
  entrar; se cuenta solo a los administradores activos y con el correo confirmado. Si aun así te quedas sin ninguna,
  vuelve a arrancar con `Seed:AdminEmail` y `Seed:AdminPassword` configurados (ver "Administrador inicial").
- La sesión de la cuenta borrada deja de valer de inmediato, también en otros dispositivos.
- Borrar un cliente libera sus huecos en la agenda, así que el calendario de ocupación cambia: también desaparecen
  sus citas ya completadas, con lo que días pasados pueden volver a aparecer como libres.

## 5. Publicar (un solo servicio)
```
cd frontend && npm run build          # genera src/TallerChat.Api/wwwroot
dotnet publish src/TallerChat.Api -c Release -o out
```
La API sirve la web y `/api/*` desde el mismo origen. Si la pones detrás de un proxy inverso, activa
`ForwardedHeaders` (basta con la variable de entorno `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`). Sin eso todos
los clientes comparten la misma IP a ojos de la API: el límite de 10 peticiones por minuto de `/api/auth/*` se
aplicaría a todo el sitio a la vez y el login dejaría de funcionar con tráfico normal. Recuerda también poner
`Shop:PublicBaseUrl` con el dominio real, porque de ahí salen los enlaces de los correos.

### Correo por la API de Gmail (Render y otros hostings que bloquean SMTP)
**Por qué.** Los servicios web del plan gratuito de Render no pueden abrir conexiones salientes por los puertos 25, 465
ni 587. El SMTP de Gmail (587) acaba en `TimeoutException`, y la web responde 502 "No se pudo enviar el correo ahora
mismo". La API de Gmail va por HTTPS (puerto 443), que no está bloqueado.

**Cómo se elige.** Si `Smtp:Gmail:RefreshToken` tiene valor, todo el correo (chat, citas, confirmación de cuenta,
restablecer contraseña, baja) sale por la API de Gmail y se ignoran `Smtp:Host`, `Port`, `Security`, `User` y
`Password`. Si está vacío, se usa SMTP como siempre (en local no cambia nada). En ambos casos `Smtp:From` es
obligatorio y, con la API de Gmail, **debe ser la misma cuenta que autoriza el token**. El código está en
`Services/GmailApiSender.cs`; solo pide el permiso `gmail.send` (enviar), nunca lee el buzón.

**Variables.** En Render (*Environment*) o en user-secrets:

| Variable | Valor |
|---|---|
| `Smtp__Gmail__ClientId` | `client_id` del cliente OAuth |
| `Smtp__Gmail__ClientSecret` | `client_secret` del cliente OAuth |
| `Smtp__Gmail__RefreshToken` | refresh token obtenido en el paso 5 |
| `Smtp__From` | la cuenta de Gmail que envía (la que autorizó el token) |

Escribe los nombres con **dos guiones bajos** (`Smtp__Gmail__ClientId`), no con dos puntos: Render no admite `:` en
los nombres de variable y .NET convierte `__` en `:` al leerlas. Del JSON del cliente solo se usan `client_id` y
`client_secret`; `project_id`, `auth_uri`, `token_uri` y `auth_provider_x509_cert_url` no hacen falta.

#### Configuración en Google (una sola vez)
1. **Proyecto y API.** En [Google Cloud Console](https://console.cloud.google.com) crea un proyecto (p. ej.
   `TallerTorque`) y, en *APIs y servicios → Biblioteca*, activa **Gmail API**.
2. **Información de la marca** (*Google Auth Platform → Información de la marca*). Aunque Google los marque como
   opcionales, **hay que rellenar todo esto o el botón "Publicar app" no se activa**:
   - Nombre de la aplicación, correo de asistencia al usuario y correo de contacto del desarrollador.
   - **Página de la aplicación**: la URL pública de la web (p. ej. `https://tu-app.onrender.com`).
   - **Política de privacidad**: `https://tu-app.onrender.com/privacidad.html`.
   - **Condiciones del servicio**: `https://tu-app.onrender.com/terminos.html`.
   - **Dominios autorizados**: el dominio de la web (p. ej. `tu-app.onrender.com`).

   Las dos páginas legales son estáticas (`frontend/public/privacidad.html` y `terminos.html`, enlazadas en el pie de
   la web), así que **despliega primero** y comprueba que abren antes de pegar las URL. Revisa sus textos (titular y
   contacto) antes de publicar.
3. **Publicar la app** (*Público → Publicar app*, estado "En producción"). Mientras esté en "Prueba":
   - Google bloquea el acceso con `Error 403: access_denied` a cualquier cuenta que no sea *usuario de prueba*.
   - El refresh token **caduca a los 7 días**, aunque publiques después: publica **antes** de sacar el token.

   Una app en producción sin verificar funciona para uso propio (límite de 100 usuarios); Google solo muestra el
   aviso "app no verificada" al autorizar.
4. **Cliente OAuth** (*Clientes → Crear cliente*): tipo **Aplicación web**, y en *URIs de redirección autorizados*
   añade `https://developers.google.com/oauthplayground` (y pulsa **Guardar**; puede tardar unos minutos en
   aplicarse). **Copia el `client_secret` al crearlo**: Google no vuelve a mostrarlo (solo se ven los últimos
   caracteres). Si lo pierdes, añade un secreto nuevo.
5. **Refresh token** con [OAuth 2.0 Playground](https://developers.google.com/oauthplayground):
   1. Rueda de ajustes ⚙ → marca **Use your own OAuth credentials** y pega el client ID y el client secret. Esto se
      pierde al recargar la página; sin ello el Playground revoca el token a las 24 h.
   2. *Step 1*: en **Input your own scopes** escribe `https://www.googleapis.com/auth/gmail.send` y pulsa
      **Authorize APIs**.
   3. En Google elige la cuenta que enviará los correos. Si avisa de que la app no está verificada: *Avanzado →
      Ir a (nombre de la app)*. Acepta el permiso de enviar correo.
   4. Vuelves con el código ya escrito en *Step 2*: pulsa **Exchange authorization code for tokens** y copia el
      **Refresh token** (suele empezar por `1//`).
6. **Render.** Añade las variables de la tabla de arriba y vuelve a desplegar.
7. **Prueba.** Inicia sesión, ve a *Mi cuenta → Citas* y pulsa **Enviármelas por email** (hace falta una cita
   próxima). Debe llegar un correo y no salir el error 502.

#### Si algo falla
| Síntoma | Causa y arreglo |
|---|---|
| `Error 403: access_denied` al autorizar | La app sigue en "Prueba" y tu cuenta no es usuario de prueba. Publícala (paso 3) o añade la cuenta en *Público → Usuarios de prueba* (el token caducará a los 7 días). |
| "Publicar app" desactivado | Falta algo en *Información de la marca* (paso 2): sobre todo las URL de política de privacidad y condiciones. Recarga la página tras guardar. |
| Render dice que la clave no es válida | El nombre lleva `:` (`Smtp:Gmail:ClientId`). Usa `__`. |
| *Authorization code* vacío en el Playground | Aún no has hecho el *Step 1*, o has recargado la página: repite los pasos 5.1 a 5.3. |
| 502 "No se pudo enviar el correo" y en el log `TimeoutException` | Se está usando SMTP desde un hosting que lo bloquea: falta `Smtp__Gmail__RefreshToken` o está vacío. |
| 502 y en el log `HttpRequestException` | Google rechazó la petición: client ID/secret incorrectos, Gmail API sin activar, token revocado o caducado (`invalid_grant`), o `Smtp__From` distinto de la cuenta que autorizó. Repite el paso 5 con credenciales propias. |
| Dejó de funcionar al cabo de unos días | El token se generó con la app en "Prueba" (7 días), o se revocó el acceso en la cuenta de Google. Publica la app y genera uno nuevo. |

**Notas.** Los logs solo guardan el tipo de excepción, nunca la dirección ni el mensaje. El refresh token da permiso
para enviar correo como esa cuenta: guárdalo solo como variable secreta (nunca en git). Gmail limita el envío
(del orden de 500 mensajes al día en una cuenta gratuita); para más volumen compensa un proveedor transaccional.

### Docker / Render
El `Dockerfile` de la raíz compila la web y la API en una sola imagen (escucha en `PORT`, que Render inyecta; 8080 si
no existe) y ya activa `ASPNETCORE_FORWARDEDHEADERS_ENABLED`. En Render: *New → Web Service*, entorno **Docker**, y en
*Environment* las variables `Mongo__ConnectionString`, `Gemini__ApiKey`, `Shop__PublicBaseUrl` (la URL pública,
p. ej. `https://tu-app.onrender.com`), `Smtp__From` y, para el primer arranque, `Seed__AdminEmail` y
`Seed__AdminPassword`. Para el correo en el plan gratuito usa la API de Gmail (sección anterior); con SMTP añade
también `Smtp__User` y `Smtp__Password`. En MongoDB Atlas permite el acceso desde las IP de Render (o `0.0.0.0/0`).
Las claves de sesión viven dentro del contenedor: tras cada despliegue los usuarios tienen que volver a iniciar sesión.
```
docker build -t tallerchat .
docker run -p 8080:8080 -e Mongo__ConnectionString=... -e Gemini__ApiKey=... tallerchat
```

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
| Textos de cada página de servicio | `services` en `frontend/src/content/site.ts` |
| Máximo de vehículos por cliente (10) | `ProfileEndpoints.MaxVehicles` |
| Administrador inicial / datos de ejemplo | `Seed:*` en user-secrets / `appsettings.Development.json` |
| Límite de envíos de email del chat (3/hora por IP) | política `email` en `Program.cs` |
| Límite de reservas (10/hora por usuario) y "enviarme mis citas" (3/hora) | políticas `booking`/`mailme` en `Program.cs` |
| Límite de login/registro (10/min por IP) | política `auth` en `Program.cs` |
| Caducidad de los enlaces de correo | `Auth:*` en `appsettings.json` |
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
- En `/cita` no aparece ninguna hora para un día: solo se muestran las horas realmente libres. Si el día está lleno
  o es demasiado próximo (`Booking:MinHoursAhead`), no habrá ninguna. El botón "Coger este hueco" salta directamente
  al primer hueco libre de toda la agenda.
- En el log de arranque aparece "No se pudieron crear los índices de MongoDB": la API arranca igualmente, pero sin
  la protección contra reservas duplicadas. El índice parcial con `$in` necesita MongoDB 7.0 o superior; comprueba
  también que no exista ya otro índice con el mismo nombre y distintas opciones.
- No se crea el administrador: revisa que `Seed:AdminEmail` sea un correo válido y `Seed:AdminPassword` tenga 10+
  caracteres con letras y números; el log de arranque avisa si los valores no son válidos.

## 8. Seguridad y privacidad
- Contraseñas con PBKDF2-SHA256 (210.000 iteraciones) y sal aleatoria; nunca en texto plano ni en los logs.
- Los datos personales y los vehículos solo los puede leer y editar su propietario o un administrador; el cliente
  nunca puede cambiar su rol ni el correo de su cuenta desde la web.
- Sesión por cookie HttpOnly, `SameSite=Lax`, del mismo origen que la API; sin tokens en `localStorage`.
- El chat solo lee de MongoDB con un usuario de mínimo privilegio; secretos fuera del repositorio.
- No se usa `v-html`; el texto del modelo se pinta como texto plano.
- El nivel gratuito de Gemini puede usar los datos enviados para mejorar los productos de Google (revisa sus
  condiciones): no incluyas en la base de conocimiento datos personales ni confidenciales.
- El asistente orienta con la información del taller; no sustituye el diagnóstico de un mecánico en sistemas
  críticos (frenos, alta tensión).
