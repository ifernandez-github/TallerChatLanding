# TallerChat

Chatbot sobre la base de conocimiento del taller (`taller_db.conocimiento`).

**Flujo:** pregunta → la API busca con `$text` en MongoDB (top 5) → Gemini redacta la respuesta usando SOLO esos
fragmentos y los cita como [n] → la web muestra la respuesta y las fuentes (título, categoría, campos y `chunk_id`).
Si MongoDB no devuelve nada, no se llama al modelo y se avisa al usuario. Si Gemini falla (límite gratuito), se
devuelven igualmente las fuentes.

## 1. Requisitos
- Visual Studio Community 2026 (carga de trabajo "ASP.NET y desarrollo web") o .NET SDK 10
- Node.js 20+ (para Vue)
- Acceso a MongoDB con usuario y contraseña
- Clave de API de Gemini (Google AI Studio → "Get API key")

## 2. Preparar MongoDB
La búsqueda necesita un índice de texto (solo se permite uno por colección):

```
mongosh "mongodb://ADMIN:CLAVE@HOST:27017/admin" scripts/mongo-setup.js
```
Crea el índice `conocimiento_text` (español) y un usuario `chat_reader` de solo lectura. Si ya tienes un usuario o
un índice de texto, adapta el script (`tdb.conocimiento.getIndexes()`).

Cadena de conexión (codifica en URL los caracteres especiales de la clave; ajusta `authSource` a la base donde
creaste el usuario):
```
mongodb://chat_reader:CLAVE@HOST:27017/taller_db?authSource=taller_db
```

## 3. Configurar secretos
```
cd src/TallerChat.Api
dotnet user-secrets set "Mongo:ConnectionString" "mongodb://chat_reader:CLAVE@HOST:27017/taller_db?authSource=taller_db"
dotnet user-secrets set "Gemini:ApiKey" "TU_CLAVE"
```
En servidor: variables de entorno `Mongo__ConnectionString` y `Gemini__ApiKey`.
Los modelos se configuran en `appsettings.json` → `Gemini:Models`, por orden de preferencia. La API reintenta
cada modelo con espera exponencial ante errores transitorios (429, 5xx, timeout; respeta `Retry-After`) y, si el
modelo no existe o tu clave no tiene acceso (403/404), pasa al siguiente. Con `Gemini:MaxRetries`,
`RetryBaseDelayMs`, `AttemptTimeoutSeconds` y `TotalTimeoutSeconds` ajustas reintentos y tiempos.
Los identificadores y el acceso al nivel gratuito cambian con frecuencia: en desarrollo, `GET /api/models`
lista los modelos que admite tu clave; elige de esa lista.

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

## Landing del taller + mecánico interactivo
La web es ahora una landing con hero animado, "Quiénes somos", "Servicios" y "Contacto" (con Google Maps).
El chat vive en un botón flotante abajo a la derecha ("Mecánico interactivo") que abre una ventana emergente
(pantalla completa en móvil; se cierra con Esc o con la X).

**Personalizar (todo en `frontend/src/content/site.ts`, son datos de ejemplo):** nombre, textos, servicios,
estadísticas, teléfono, email, horario y dirección. La ubicación del mapa sale de `address.lat` y `address.lng`
(las de ejemplo apuntan a un punto de Madrid; pon las coordenadas reales). Actualiza también el JSON-LD de
`frontend/index.html` (SEO local) para que coincida.

**Imágenes:** el hero y "Quiénes somos" usan ilustraciones SVG propias (`HeroScene.vue`, `AboutArt.vue`). Para usar
fotos reales, copia los archivos a `frontend/public/img/` y rellena `heroImage` y `aboutImage` en `site.ts`
(p. ej. `'/img/hero.jpg'`); el efecto de "pantalla que se enciende" se aplica igual. Si las sacas de bancos como
Unsplash o Pexels, revisa su licencia.

**Efectos:** el hero se enciende (punto → línea → pantalla completa) y las capas hacen parallax con el scroll.
Con "reducir movimiento" activado en el sistema, todo se muestra sin animaciones.

**Mapa y privacidad:** el iframe de Google Maps solo se carga cuando el visitante pulsa "Cargar mapa interactivo"
(evita cookies de terceros antes del consentimiento). Cuando publiques la web, añade tu política de privacidad.

## Envío por email (opcional)
Bajo cada respuesta con fuentes aparece "Enviar por email". El usuario elige qué respuestas incluir (hasta 5) y
escribe su dirección; el correo lleva las respuestas y sus fuentes. Si `Smtp:Host` o `Smtp:From` están vacíos, la
función queda desactivada y la web oculta el botón (`GET /api/features`).

```
cd src/TallerChat.Api
dotnet user-secrets set "Smtp:Host" "smtp.tudominio.com"
dotnet user-secrets set "Smtp:User" "usuario"
dotnet user-secrets set "Smtp:Password" "CLAVE"
dotnet user-secrets set "Smtp:From" "asistente@tudominio.com"
```
- Puerto 587 con STARTTLS (por defecto, `Smtp:Security: Auto`) o 465 con SSL. Con Gmail hace falta una contraseña
  de aplicación (verificación en dos pasos); Microsoft 365 suele tener desactivada la autenticación SMTP básica.
- Para que los correos no caigan en spam, usa un remitente de tu dominio con SPF y DKIM configurados
  (o un proveedor SMTP transaccional).
- Pruebas locales sin enviar nada real: smtp4dev o MailHog con `Smtp:Security: "None"` y su puerto.
- Protecciones: 3 envíos por hora y por IP, un enfriamiento de 5 minutos por destinatario (solo se guarda un hash
  en memoria), asunto y plantilla fijos, todo el contenido se codifica en HTML, límites de tamaño y sin registrar
  ni guardar direcciones.
- Limitación conocida: el servidor no comprueba que el texto lo haya generado el asistente; un cliente
  manipulado podría enviar texto propio (dentro de los límites anteriores). Si la web es pública y esto te
  preocupa, hay que guardar en el servidor cada respuesta y enviar solo por identificador.

## 5. Publicar (un solo servicio)
```
cd frontend && npm run build          # genera src/TallerChat.Api/wwwroot
dotnet publish src/TallerChat.Api -c Release -o out
```
La API sirve la web y `/api/*` desde el mismo origen. Si la pones detrás de un proxy inverso, configura
`ForwardedHeaders` para que el límite de peticiones use la IP real.

## 6. Ajustes habituales
| Qué | Dónde |
|---|---|
| Nº de fragmentos enviados al modelo | `Mongo:MaxResults` |
| Peso de cada campo en la búsqueda | `weights` en `scripts/mongo-setup.js` |
| Instrucciones del modelo | `SystemPrompt` en `Services/GeminiClient.cs` |
| Reintentos, tiempos y modelos de Gemini | `Gemini:*` en `appsettings.json` |
| Límite de envíos de email (3/hora por IP) | política `email` en `Program.cs` |
| Límite de peticiones (10/min por IP) | `AddRateLimiter` en `Program.cs` |
| Longitud máxima de pregunta (500) | `Endpoints/ChatEndpoints.cs` |

## 7. Problemas frecuentes
- `text index required`: falta el índice; ejecuta `scripts/mongo-setup.js`.
- `Authentication failed`: revisa `authSource` y la codificación de la clave.
- Siempre "No he encontrado información": la búsqueda `$text` no admite sinónimos; añade términos a `tags`
  o a los campos indexados. Para búsqueda semántica (embeddings + Atlas Vector Search) habría que ampliar el diseño.
- `404` de Gemini con `gemini-2.5-flash`: Google limita el acceso a los modelos 2.5 a cuentas que ya los usaban; pon un modelo de `/api/models` primero en `Gemini:Models`.
- El botón de email no aparece: falta `Smtp:Host` o `Smtp:From`.
- El email falla (502): mira el log de la API (solo muestra el tipo de excepción, sin direcciones); revisa host, puerto, `Smtp:Security` y credenciales.
- Respuesta con solo fuentes: Gemini devolvió error o límite; mira el log de la API.

## 8. Seguridad y privacidad
- La API solo lee de MongoDB con un usuario de mínimo privilegio; secretos fuera del repositorio.
- No se usa `v-html`; el texto del modelo se pinta como texto plano.
- El nivel gratuito de Gemini puede usar los datos enviados para mejorar los productos de Google (revisa sus
  condiciones): no incluyas en la base de conocimiento datos personales ni confidenciales.
- El asistente orienta con la información del taller; no sustituye el diagnóstico de un mecánico en sistemas críticos
  (frenos, alta tensión).
