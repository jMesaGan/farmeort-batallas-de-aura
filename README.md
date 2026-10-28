# FarmeORT · Batallas de Aura

Demo de despliegue para Diseño de Aplicaciones 2, ORT Uruguay. Código independiente en esta carpeta; los ejemplos previos del repositorio se conservan.

## Alcance

Registro de jugador → login → inscripción aceptada en la copa → logout → login de organizador → generación de ronda y batalla → consulta del detalle persistido. No incluye lanzamiento de skills, arbitraje, resultados, cambios de ELO, creación de otros torneos ni nuevas rondas. El jugador comienza sin skills; el torneo no exige ninguna.

La pantalla **Registrar batalla de demo** genera una ronda, nunca una batalla aislada. El servidor asigna IDs, rol Jugador, ELO 1200, estado Pendiente, participantes y primer turno. La API no recibe esos valores del cliente. En esta demo solo existe una ronda por torneo. Un reintento devuelve la misma batalla.

## Versiones y requisitos

| Componente | Versión |
| --- | --- |
| SDK .NET | 10.0.201 (`global.json`; permite parches/features de .NET 10) |
| Target / ASP.NET Core | net10.0 / 10.0 (runtime local 10.0.5) |
| EF Core y proveedor SQL Server | 10.0.7 |
| Herramienta local dotnet-ef | 10.0.7 |
| Angular | 17.3.12, conservando la versión mayor de los ejemplos existentes |
| CLI / builder Angular | 17.3.17 |
| TypeScript | 5.2.2 |
| Node de frontend | 20.11.1 (`.nvmrc`) |
| SQL local | Microsoft SQL Server **Express 2025** (17.x) |
| SQL desplegado | Azure SQL Database |

Se necesitan npm, SDK .NET 10 y acceso a npm/NuGet para restaurar dependencias. `package-lock.json` y `packages.lock.json` fijan dependencias transitivas. Los contenedores usan imágenes oficiales .NET 10 que reciben parches. No se necesita Docker para ejecutar localmente contra Express.

Angular 17 usa Node 18/20 según su [matriz oficial](https://angular.dev/reference/versions). Se encontró y usó Node 20.11.1 en `/usr/local/bin/node` para la comprobación final; el Node predeterminado del entorno es 25.2.0. En Vercel se selecciona Node 24 para instalar dependencias y el comando `build:vercel` obtiene **Node 20.11.1 como herramienta de build** mediante npx, porque [Vercel retiró Node 20 de los builds estándar](https://vercel.com/changelog/node-js-20-is-being-deprecated). El resultado es una SPA estática sin runtime Node.

## Estructura

```text
backend/src/FarmeORT.Domain          Entidades y relaciones
backend/src/FarmeORT.Application     Casos de uso, validaciones, DTOs y puertos
backend/src/FarmeORT.Infrastructure  EF, SQL Server, hashing, seed y migraciones
backend/src/FarmeORT.Api             HTTP, autenticación, autorización y configuración
backend/tests/FarmeORT.Tests         Reglas, servicios y prueba SQL opcional
frontend/                           Angular standalone y formularios reactivos
scripts/smoke.mjs                    Recorrido HTTP completo contra una demo aislada
```

La sesión usa un token aleatorio de 256 bits. Se devuelve una sola vez en el login, Angular lo conserva en `sessionStorage` y lo envía como `Authorization: Bearer <token>`. SQL guarda únicamente su SHA-256, usuario y expiración. Logout elimina la sesión del servidor; no basta con borrar el token del navegador. Las sesiones sobreviven reinicios de la API hasta su vencimiento. La contraseña se guarda con `PasswordHasher` de ASP.NET Core (PBKDF2, salt individual, 210.000 iteraciones), nunca en DTOs. El login devuelve el mismo error para email inexistente y contraseña incorrecta y está limitado por IP.

## SQL Server Express 2025 local

1. En Windows x64, instalar **SQL Server 2025 Express** desde [Microsoft](https://www.microsoft.com/en-us/sql-server/sql-server-downloads), motor de base de datos, instancia `SQLEXPRESS`. Instalar SSMS para administrar. No reemplazarlo por otro motor o edición.
2. En SQL Server Configuration Manager habilitar TCP/IP para SQLEXPRESS. En IPAll, vaciar puertos dinámicos y fijar TCP 1433; reiniciar el servicio. Abrir el firewall solo al equipo de desarrollo si se conecta desde otra máquina.
3. Crear una base exclusiva `FarmeORTDemo`. Para una ejecución Windows local se puede usar autenticación integrada. Para otro sistema operativo, habilitar modo mixto, crear un login SQL dedicado y un usuario en esa base. El usuario de migraciones requiere permisos DDL; el de runtime requiere lectura/escritura. No usar `sa` en la API.
4. Configurar la conexión. Ejemplo Windows local: `Server=localhost,1433;Database=FarmeORTDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True`. O usar el ejemplo con login de `.env.example`, reemplazando la contraseña fuera de Git.
5. El certificado autofirmado de Express solo se admite con `Demo__AllowLocalUntrustedCertificate=true`, entorno `Demo` y servidor `localhost`, `127.0.0.1` o `.`. Para conexiones remotas usar un certificado confiable y `TrustServerCertificate=False`. Azure siempre debe usar esa validación.

Express 2025 no se instala de forma nativa en este macOS ARM. Para dar la clase desde Mac, usar un host Windows x64 o VM compatible con Express 2025 y una conexión TCP con certificado confiable. Esta entrega no sustituye Express por un contenedor Developer, SQLite ni una base en memoria.

## Alternativa local solicitada: Express 2025 con Docker

`backend/compose.yaml` usa la imagen oficial SQL Server 2025 con `MSSQL_PID=Express`, puerto solo en loopback y volumen persistente. En Apple Silicon se fuerza `linux/amd64`: requiere emulación, una configuración [no soportada oficialmente por Microsoft](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver17). Para la clase, verificar que la imagen arranque en el equipo antes de depender de ella.

Con Docker Desktop iniciado, desde `farmeort/backend`:

```sh
../scripts/start-db.sh
set -a
source .env
set +a
dotnet tool restore
# Solo las migraciones usan sa; el runtime usa farmeort_app, sin permisos DDL.
ConnectionStrings__SqlServer="Server=localhost,1433;Database=FarmeORTDemo;User Id=sa;Password=$MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True" \
  dotnet ef database update --project src/FarmeORT.Infrastructure --startup-project src/FarmeORT.Api
dotnet run --project src/FarmeORT.Api --no-launch-profile
```

El script crea `.env` solo si no existe, con contraseñas aleatorias, espera a que SQL esté disponible y prepara la base y el usuario de runtime. Si ya hay `.env`, agregar `MSSQL_SA_PASSWORD` y `MSSQL_APP_PASSWORD` (esta última debe coincidir con la contraseña de la cadena de conexión). Conservar `.env` junto al volumen para mantener las credenciales entre arranques. Reiniciar con `docker compose stop db` y `docker compose start db`, sin borrar el volumen.

Intento de ejecución desde la sesión del asistente: Docker Desktop falla al iniciar con `user: unknown userid 501`; no quedó un servidor SQL ejecutándose. El contenedor y la aplicación deben verificarse desde una terminal de usuario con Docker Desktop funcionando.

## Ejecutar backend

Desde `farmeort/backend`:

```sh
dotnet tool restore
dotnet restore
dotnet build
cp .env.example .env
# Editar .env con la conexión de la demo local.
set -a
source .env
set +a
dotnet ef database update --project src/FarmeORT.Infrastructure --startup-project src/FarmeORT.Api
dotnet run --project src/FarmeORT.Api --no-launch-profile
```

`source` corresponde a bash/zsh. En PowerShell definir las mismas variables mediante `$env:ASPNETCORE_ENVIRONMENT='Demo'`, `$env:ConnectionStrings__SqlServer='...'`, etc. .NET **no carga `.env` automáticamente**. La factory de EF lee `ConnectionStrings__SqlServer` y no ejecuta el seed al generar/aplicar migraciones. Las migraciones se aplican explícitamente antes del primer arranque; la API no tiene permisos para aplicarlas automáticamente.

Comprobar `http://localhost:5080/health` (`{status: "ok"}`). `/health/ready` confirma conectividad SQL con respuesta mínima y devuelve 503 si no está lista. No devuelve la cadena de conexión ni detalles internos.

## Ejecutar frontend

En otra terminal, desde `farmeort/frontend`:

```sh
nvm install
nvm use
npm ci
export BACKEND_URL=http://localhost:5080
npm start
```

Abrir `http://localhost:4200`. `BACKEND_URL` es público y se incorpora al bundle al compilar; nunca colocar secretos allí. Se exige un origen HTTP(S) sin ruta. Si cambia la URL, recompilar. El proyecto no depende de fuentes ni imágenes externas.

```sh
npm run build
# Salida: dist/farmeort/browser
```

## Variables

| Variable backend | Uso |
| --- | --- |
| `ConnectionStrings__SqlServer` | Conexión SQL Server / Azure SQL; obligatoria |
| `ASPNETCORE_ENVIRONMENT` | `Demo` para esta demostración; `Production` fuera de ella |
| `ASPNETCORE_URLS` | Local: `http://0.0.0.0:5080` |
| `PORT` | Render la inyecta; Docker escucha `0.0.0.0:$PORT` (fallback 8080) |
| `Auth__SessionMinutes` | Duración 1–1440 minutos; default 120 |
| `Cors__Origins` | Orígenes exactos separados por coma; default `http://localhost:4200` |
| `Demo__Enabled` | `true` habilita seed, exclusivamente con entorno `Demo` |
| `Demo__OrganizerPassword` | Contraseña inicial ficticia del organizador, obligatoria con seed |
| `Demo__RivalPassword` | Contraseña inicial ficticia del rival, obligatoria con seed |
| `Demo__AllowLocalUntrustedCertificate` | Excepción de certificado exclusiva para loopback en Demo; default false |

Frontend: `BACKEND_URL`, por ejemplo `https://farmeort-api.onrender.com`. En Vercel `VERCEL` hace que el generador rechace HTTP.

No hay clave JWT: la autenticación es una sesión opaca con revocación SQL. Los secretos reales se configuran en variables, fuera de Git; `.env` está ignorado. El seed no cambia contraseñas ya existentes.

## Seed y credenciales ficticias

| Rol | Email | Contraseña de ejemplo |
| --- | --- | --- |
| Organizador | `organizador@farmeort.example` | `OrganizadorDemo123!` |
| Rival | `rival@farmeort.example` | `RivalDeDemo12345!` |

Solo se crean si `Demo__Enabled=true` y `ASPNETCORE_ENVIRONMENT=Demo`. Deben configurarse esas contraseñas ficticias en las variables correspondientes; no están embebidas en el código del servidor. El seed es transaccional e idempotente por IDs estables y tiene un lock de SQL para arranques simultáneos. Crea un organizador, un rival Jugador con ELO 1200, torneo abierto con cupo 2 y una inscripción aceptada para el rival. No crea rondas ni batallas. No recupera cupos ni cambia un torneo ya iniciado al reiniciar.

## Azure SQL Database: preparar durante la clase

1. Crear un servidor lógico y una base exclusiva para la demo. Guardar credenciales fuera del repositorio; no reutilizar una base del obligatorio real.
2. En Networking, habilitar acceso público con **Selected networks**. Autorizar la IP pública del equipo que aplicará migraciones y las [direcciones de salida de la región de Render](https://render.com/docs/outbound-ip-addresses). Consultarlas en el servicio de Render y configurar los rangos necesarios, sin abrir todo Internet. [Azure documenta estas reglas](https://learn.microsoft.com/en-us/azure/azure-sql/database/connectivity-settings?view=azuresql).
3. Permitir salida TCP hacia Azure SQL en la red del equipo. Para simplificar el firewall de la demo, configurar política de conexión **Proxy** en el servidor lógico y TCP 1433; Redirect puede requerir puertos adicionales. TLS mínimo 1.2.
4. Usar una conexión como esta, completando datos mediante variables:

```text
Server=tcp:TU-SERVIDOR.database.windows.net,1433;Initial Catalog=FarmeORTDemo;User ID=TU-USUARIO;Password=TU-SECRETO;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

5. Exportar esa conexión como `ConnectionStrings__SqlServer` en una terminal. Desde `backend`, ejecutar `dotnet tool restore` y `dotnet ef database update --project src/FarmeORT.Infrastructure --startup-project src/FarmeORT.Api` con el usuario de migraciones. Para revisar el SQL antes de aplicarlo: `dotnet ef migrations script --idempotent --project src/FarmeORT.Infrastructure --startup-project src/FarmeORT.Api --output migrations.sql`.
6. Configurar un usuario de runtime de la base con lectura/escritura (por ejemplo roles `db_datareader`/`db_datawriter`), sin permisos para crear o borrar bases ni alterar esquemas. La API usa `sys.sp_getapplock` con principal público para serializar operaciones. No habilitar retries automáticos de EF alrededor de esta transacción; el cliente puede reintentar la operación idempotente.

## Render: backend Docker

No se creó ni publicó ningún servicio. Cuando corresponda durante la clase:

1. Crear Web Service desde el repositorio. Root Directory: `backend`. Runtime: Docker. Dockerfile: `Dockerfile`, contexto `.`. El Dockerfile multi-stage usa SDK/runtime oficiales .NET 10 y usuario no root.
2. Definir conexión Azure cifrada, `ASPNETCORE_ENVIRONMENT=Demo`, `Demo__Enabled=true`, ambas contraseñas ficticias, `Demo__AllowLocalUntrustedCertificate=false`, `Auth__SessionMinutes=120` y `Cors__Origins=https://TU-FRONTEND.vercel.app,http://localhost:4200`.
3. Aplicar las migraciones antes de iniciar el servicio. No poner credenciales como build args. Configurar health check `/health/ready`. Render provee `PORT`; el entrypoint escucha en `0.0.0.0` ([contrato de puertos](https://render.com/docs/web-services)).
4. Verificar `/health` y `/health/ready` en la URL HTTPS asignada. El primer arranque realiza el seed; un redeploy conserva usuarios, inscripciones, sesiones y batalla en Azure SQL.

Prueba Docker local opcional, desde `backend`: `docker build -t farmeort-api .` y `docker run --rm -p 5080:8080 -e ConnectionStrings__SqlServer -e ASPNETCORE_ENVIRONMENT -e Demo__Enabled -e Demo__OrganizerPassword -e Demo__RivalPassword -e Cors__Origins farmeort-api` (toma las variables previamente exportadas en la terminal). Una conexión `localhost` dentro del contenedor apunta al propio contenedor; para SQL externo usar un nombre accesible y certificado confiable. `.env` no entra en la imagen.

## Vercel: frontend Angular

1. Importar el repositorio. Root Directory: `frontend`. Framework: Angular. Seleccionar Node **24.x** para la instalación estándar de Vercel. El rango `engines.node` permite `20.x || 24.x`; Node 20 se usa localmente y en el compilador invocado por `build:vercel`.
2. Configurar `BACKEND_URL=https://TU-API.onrender.com`. `vercel.json` fija instalación `npm ci --engine-strict=false`, build `npm run build:vercel` y salida `dist/farmeort/browser`. El script de build usa Node **20.11.1** vía npx para compilar Angular 17; necesita acceso al registry. La relajación de engines solo permite la fase de instalación bajo Node 24; el compilador se ejecuta con Node 20.
3. Agregar el origen HTTPS exacto de Vercel a `Cors__Origins` en Render, sin slash final. Las previews requieren su propio origen; no usar un wildcard.
4. Publicar cuando se decida en clase. El rewrite de `vercel.json` devuelve `index.html` para rutas SPA sin extensión, incluyendo `/registro`, `/organizador` y `/batalla/<id>` al recargar.

## Guion para mostrar en clase

1. Abrir Registro y mostrar una validación (por ejemplo contraseña corta). Registrar `Ana`, `Pérez`, un email nuevo, nickname alfanumérico, contraseña como `JugadorDeDemo123!` y fecha de nacimiento de mayor de 13 años.
2. Login con el jugador. Ver copa abierta y rival inscripto. Pulsar **Inscribirme en la copa**. Se confirma aceptación y 2/2 jugadores.
3. **Cerrar sesión**. Ingresar como organizador con las credenciales ficticias del seed.
4. Abrir **Registrar batalla de demo**. Ver ambos jugadores y sus ELO. Pulsar **Generar ronda y registrar batalla**.
5. En el detalle ver ID, torneo, ronda 1, ELO iniciales, estado Pendiente y quién comienza. Con ELO empatado se decide al azar una sola vez.
6. Copiar la URL y recargar. Reiniciar backend (o redeploy). Recargar la misma URL y comprobar mismo ID y primer turno; en el torneo el estado será En curso. Volver a generar devuelve esa misma batalla.
7. Opcionalmente consultar SQL:

```sql
SELECT t.Name, t.Status, r.Number, b.Id, b.Status, b.StartingPlayerId,
       b.PlayerOneElo, b.PlayerTwoElo
FROM Tournaments t
JOIN [Round] r ON r.TournamentId = t.Id
JOIN Battles b ON b.RoundId = r.Id;
```

El nombre EF de la tabla de rondas es `Round` (no hay DbSet con otro nombre). Los apellidos, email y hash no se muestran en el detalle.

## Restablecer exclusivamente la demo

Detener la API y confirmar que la conexión apunta a **la base aislada de demostración**. Desde `backend`, con las mismas variables de esa demo:

```sh
export ASPNETCORE_ENVIRONMENT=Demo
export Demo__Enabled=true
dotnet run --project src/FarmeORT.Api --no-launch-profile -- --reset-demo
# El proceso finaliza tras el reset. Luego iniciar normalmente:
dotnet run --project src/FarmeORT.Api --no-launch-profile
```

El comando se rechaza fuera de entorno Demo o sin seed habilitado. Solo elimina el torneo con el ID fijo de demo y flag `IsDemo`, sus inscripciones/rondas/batallas por cascada y las sesiones de los dos usuarios de seed. Lo recrea abierto con el rival. Conserva los jugadores que registraron los estudiantes, sus contraseñas y otros torneos. Se puede volver a inscribir el mismo jugador. No ejecutar `database drop` para reiniciar la clase. Para cambiar contraseñas de seed existentes no basta cambiar variables: el seed preserva los usuarios ya creados; usar una nueva base de demo si se necesita un inicio totalmente limpio.

## Pruebas y verificación

Desde `backend`:

```sh
dotnet build
dotnet test
# Alternativa sin sockets de VSTest; ejecuta los mismos Fact/Theory con sus aserciones:
dotnet run --project tests/FarmeORT.Tests --no-launch-profile
```

Incluye registro y límites, hashing, unicidad lógica, token guardado como hash, credenciales inválidas, expiración, revocación, inscripción, cupo, permisos del organizador y del detalle, dos aceptados distintos, ELO mayor, empate, relaciones e idempotencia.

La prueba SQL opcional crea una base temporal `FarmeORT_Test_<guid>`, aplica migraciones y verifica seed repetido, unicidad real, generación concurrente desde contextos separados, consulta desde un contexto nuevo, rollback y reset. Elimina **solo esa base temporal** en finally. No usa EF InMemory ni SQLite. Para ejecutarla contra Express 2025, configurar una conexión de prueba con permiso CREATE/DROP DATABASE:

```sh
export FARMEORT_TEST_SQL='Server=localhost,1433;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test
```

Recorrido HTTP completo contra una demo aislada recién restablecida, con API iniciada (crea un jugador y consume el cupo):

```sh
# Desde farmeort/
export API_URL=http://localhost:5080
export DEMO_ORGANIZER_PASSWORD='OrganizadorDemo123!'
node scripts/smoke.mjs
```

Comprueba también 401/403, validaciones, intentos de imponer rol/ELO, logout y tres solicitudes concurrentes de generación. Para comprobar reinicios, seguir el guion con el ID que imprime.

Verificación en este entorno (macOS ARM, 27/10/2026):

- Build backend y proyectos de pruebas: correcto, sin errores ni warnings.
- Migración inicial y SQL idempotente: generados correctamente; no requieren conectarse a SQL. Se utilizó la herramienta global EF 10.0.9 disponible con librerías EF 10.0.7; la herramienta reproducible del proyecto está fijada en 10.0.7.
- 23 casos de reglas/servicios ejecutados correctamente con el runner sin sockets. Una prueba SQL omitida al no tener `FARMEORT_TEST_SQL`.
- `dotnet test` compiló pero VSTest no pudo abrir su socket interno por las restricciones del entorno; por eso se usó el runner alternativo.
- Build frontend de producción: **correcto**, con Node 20.11.1; salida `frontend/dist/farmeort/browser`. Se restauraron los paquetes que faltaban resolviendo el DNS solo para el proceso de descarga. El despliegue remoto en Vercel sigue pendiente.
- No se pudo ejecutar SQL Server Express 2025 en este Mac ni se proporcionaron credenciales Azure. Quedan pendientes integración SQL, prueba HTTP, navegación visual y Docker/servicios de despliegue. No se publicó ni contrató ningún servicio.

Si el SDK de macOS presenta problemas con el compilador compartido dentro de un sandbox, compilar con `dotnet build -m:1 -p:UseSharedCompilation=false`. Esto no afecta el build Docker Linux.
