# Pruebas de la API

Proyecto: `tests/TallerChat.Api.Tests` (xUnit, .NET 10). Cubre usuarios, citas, roles (Cliente / Gestor / Administrador),
emails con la URL base y el chatbot sobre MongoDB. No hace ninguna llamada a internet: Gemini y Gmail se sustituyen por
dobles HTTP, y MongoDB es real (MongoDB 7 en contenedor, o el que indiques).

## Ejecutarlas

```bash
dotnet test                      # desde la raíz: unitarios + integración
dotnet test --filter "FullyQualifiedName~Unit"          # solo unitarios (segundos, sin Docker ni MongoDB)
dotnet test --filter "FullyQualifiedName~Integration"   # solo integración
```

Las pruebas de integración necesitan un MongoDB 7+ (el índice parcial de citas lo exige). Se resuelve solo, en este orden:

1. Variable `TALLER_TEST_MONGO` con una cadena de conexión (`mongodb://localhost:27017`): usa ese servidor. Cada test crea
   una base `taller_test_xxxx` propia y la borra al terminar; nunca toca `taller_db`.
2. Si no está definida y hay **Docker** (Docker Desktop en Windows), levanta `mongo:7.0` con Testcontainers.
3. Si no hay ninguna de las dos, los tests de integración salen como **omitidos** (no fallan) y los unitarios corren igual.

Primera ejecución con Docker: baja la imagen `mongo:7.0` (unos 250 MB). Duración orientativa: unitarios ~3 s; suite completa ~2 min
(cada test arranca su propia copia de la API).

## Arquitectura

```
tests/TallerChat.Api.Tests/
  Unit/              reglas puras y servicios con dobles; sin MongoDB, sin Docker
  Integration/       la API real (WebApplicationFactory<Program>) + MongoDB real + Gemini/Gmail falsos
  Infrastructure/    TallerApp (la API en memoria), MongoFixture, GoogleFake (captura los emails), FakeHttpHandler
```

**Pirámide.** Muchos unitarios baratos para las reglas (roles, validación, URL base, franjas, plantillas de email, reintentos de
Gemini) y pruebas de integración por flujo de negocio (alta → confirmar → reservar → confirmar/cancelar → avisos). Los tests
de integración atraviesan la pila entera: cookie, `OnValidatePrincipal`, políticas `staff`, límites de peticiones, MongoDB,
índices únicos y envío de correo.

**Por qué MongoDB real y no un mock.** Las reglas críticas viven en la base de datos: el índice único parcial
`appointments_slot_bay_unique` (doble reserva), el índice único de correo, las transiciones atómicas con `FindOneAndUpdate`
y `$text` del chat. Un mock de `IMongoCollection` no las comprobaría.

**Dobles de las dependencias externas (sin tocar el código de producción):**

| Dependencia | Cómo se sustituye |
|---|---|
| Gemini | `AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(...)` con `FakeHttpHandler`: guiones de respuesta (200, 429, 404, vacío...) y registro de lo que se le envía |
| Email | Con `Smtp:Gmail:RefreshToken` la API envía por `GmailApiSender`; `GoogleFake` hace de Gmail + OAuth, decodifica el MIME y deja el correo (destinatario, asunto, cuerpo, adjunto `.ics`, enlaces) listo para comprobar |
| IP de origen | `TestServer` no tiene IP: un `IStartupFilter` la toma de la cabecera `X-Test-Ip`, así los límites por IP no se mezclan entre tests |
| Configuración | `UseSetting(...)` (no `ConfigureAppConfiguration`: `Program.cs` lee `Mongo:ConnectionString` y `Gemini:ApiKey` nada más empezar) |
| Entorno | `Testing` se comporta como producción (obliga a tener correo, activa la URL por petición). `Development` se usa solo donde se prueba su comportamiento |

El único cambio en código de producción es `public partial class Program;` al final de `Program.cs`, necesario para
`WebApplicationFactory<Program>`.

**Aislamiento.** Cada test crea su `TallerApp` con base de datos propia (nombre aleatorio). Las franjas de agenda se reparten con
`NextSlotAsync()` para que ningún test pise el hueco de otro. Los usuarios se insertan directamente en Mongo y se entra
por `/api/auth/login`, como haría la web.

## Qué cubre

- **Usuarios y sesión** (`AuthFlowTests`, `ProfileTests`): alta, correo de confirmación, token de un solo uso (solo se guarda su hash),
  login igual para clave mala / correo inexistente, desactivadas, contraseña olvidada (misma respuesta exista o no la cuenta,
  enfriamiento), cierre de sesiones al cambiar la clave, límite de 10 intentos/min, cookie HttpOnly/SameSite, datos y vehículos.
- **Roles** (`RoleAuthorizationTests`, `UserManagementTests`, `RolesTests`): matriz anónimo/cliente/gestor/admin sobre `/api/admin/*`; el
  gestor solo ve y toca clientes; solo el admin reparte roles; un cambio de rol, desactivación o borrado cierra la sesión al
  instante; nadie se quita su propio rol; el último administrador utilizable no se pierde (ni con bajas simultáneas); borrar
  exige correo exacto / contraseña.
- **Citas** (`AppointmentTests`, `BookingScheduleTests`): disponibilidad, franjas y zona horaria (Madrid, verano/invierno),
  antelación, máximo de días y de citas activas, doble reserva (incluso con peticiones simultáneas), cancelar y liberar hueco,
  transiciones de estado válidas e inválidas, agenda y calendario.
- **Emails con URL base** (`EmailBaseUrlTests`, `PublicUrlTests`, `AppointmentEmailsTests`, `EmailSendingTests`): `Shop:PublicBaseUrl` →
  `RENDER_EXTERNAL_URL` → origen de la primera petición (y que una cabecera `Host` falsa posterior no la cambie); enlaces de
  confirmar, restablecer, "mis citas" y agenda de admin; aviso a admins y gestores sin repetir buzón (`Shop:PlaceholderDomain`);
  cuentas `@demo.taller` nunca reciben correo; un fallo de Gmail nunca rompe la reserva; HTML codificado.
- **Chatbot** (`ChatTests`, `ChatEmailTests`, `GeminiClientTests`, `ChatContentTests`): búsqueda `$text` real en MongoDB, Gemini solo recibe
  fragmentos, sin fragmentos no se llama al modelo, reintentos y modelo de respaldo, historial acotado, límites (500 caracteres,
  10/min), correo del chat (asunto fijo, enfriamiento por destinatario, 3/h).
- **Arranque** (`StartupTests`): configuración obligatoria, índices, administrador inicial, cuentas antiguas, datos de ejemplo y
  recordatorios de 24 h.

## Hallazgos (tests marcados como omitidos)

Dos tests están con `Skip` porque describen un bug real de `GeminiClient.SendAsync`: si Gemini devuelve `"candidates": []`
(lo que hace cuando bloquea la pregunta entera), `json["candidates"][0]` lanza `ArgumentOutOfRangeException`, que `ChatEndpoints`
no captura, y el usuario recibe un 500 en vez de las fuentes. Arreglo de una línea:
`json?["candidates"]?.AsArray().FirstOrDefault()?["content"]?["parts"]?.AsArray()`. Al corregirlo, quita el `Skip` de
`GeminiClientTests.Una_respuesta_sin_candidatos_prueba_otro_modelo` y `ChatTests.Si_Gemini_bloquea_la_pregunta_sin_candidatos_se_devuelven_las_fuentes`.

## CI y despliegue en Render

El `Dockerfile` **no cambia ni ejecuta tests**: solo copia `src/` y publica `TallerChat.Api.csproj`, así que el proyecto de tests
no entra en la imagen de Render (además `tests/` está en `.dockerignore`). Los tests corren aparte:

- **GitHub Actions** (`.github/workflows/tests.yml`): en cada push y PR levanta un servicio `mongo:7.0`, define `TALLER_TEST_MONGO` y
  ejecuta `dotnet test`.
- **Que Render solo despliegue si pasan**: en el servicio de Render, *Settings → Build & Deploy → Auto-Deploy → "After CI checks pass"*.
  Así un push con tests en rojo no se despliega y el `Dockerfile` sigue siendo el de producción.
- Alternativa sin GitHub Actions: una etapa de tests en el `Dockerfile`, que se usa a mano con `docker build --target test .`.
  **Debe ir ANTES de la etapa final de ejecución** (`FROM mcr.microsoft.com/dotnet/aspnet:10.0`): Render construye la última
  etapa del archivo, así que una etapa de tests colocada al final se convertiría en la imagen desplegada.

  ```dockerfile
  FROM mcr.microsoft.com/dotnet/sdk:10.0 AS test
  WORKDIR /src
  COPY . .
  # necesita un MongoDB accesible: TALLER_TEST_MONGO=mongodb://host.docker.internal:27017
  CMD ["dotnet", "test"]
  ```

  Como `.dockerignore` excluye `tests/`, para esa etapa habría que quitar esa línea (o usar un `.dockerignore` específico).
  Por eso la recomendación es CI aparte y no tocar el `Dockerfile`.

## Entorno con Docker Compose

`docker-compose.yml` levanta un entorno completo con Docker Desktop:

| Servicio | Puerto | Qué es |
|---|---|---|
| `mongo` | 27017 | MongoDB 7.0 con volumen `mongo_data` y healthcheck |
| `mongo-setup` | – | Ejecuta `scripts/mongo-setup.js` (solo el índice de texto de `conocimiento`; omite `createUser`) y termina |
| `api` | 8080 | Imagen del `Dockerfile` (API + web compilada), entorno Development |
| `web` | 5173 | Vite en modo dev; `/api` se redirige a `http://api:8080` vía `VITE_API_PROXY` |

```powershell
Copy-Item .env.example .env        # rellena GEMINI_API_KEY y SEED_ADMIN_PASSWORD (.env no se versiona)
docker compose up -d --build
curl http://localhost:8080/api/health
$env:TALLER_TEST_MONGO='mongodb://localhost:27017'; dotnet test
```

- La API exige `Gemini__ApiKey` al arrancar; los tests usan un Gemini simulado, así que sirve cualquier valor, pero el chat real necesita una clave válida.
- Sin `TALLER_TEST_MONGO`, los tests levantan su propio `mongo:7.0` con Testcontainers.
- Si existe un `src/TallerChat.Api/wwwroot` compilado en local, el test `Las_rutas_solo_de_desarrollo_...` falla (el fallback SPA responde 200 a `/openapi/v1.json`); bórralo o ejecútalo en un checkout limpio.
- Parar: `docker compose down` (añade `-v` para borrar los datos).

## Cómo añadir tests

- Regla pura o servicio sin base de datos → `Unit/`. Para construir `BookingService` sin Mongo: `TestDb.Booking()`.
- Flujo con base de datos → `Integration/`, hereda de `IntegrationTest`, `[MongoFact]`/`[MongoTheory]`, y usa `App.UserAsync(role)`,
  `App.LoginAsync(user)`, `App.NextSlotAsync()`, `App.Google.To(email)` y `App.Gemini`.
- Otra configuración (URL base, sin correo, datos previos...) → `Start(new TallerAppOptions { ... })`.
