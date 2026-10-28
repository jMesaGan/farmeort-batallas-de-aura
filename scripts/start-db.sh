#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../backend"
if [[ ! -f .env ]]; then
  python3 - <<'PY'
import os, secrets
from pathlib import Path
sa = secrets.token_hex(24) + 'aA1!'
app = secrets.token_hex(24) + 'aA1!'
content = Path('.env.example').read_text().replace('Password=REEMPLAZAR;', 'Password=' + app + ';')
content += "\nMSSQL_SA_PASSWORD='" + sa + "'\nMSSQL_APP_PASSWORD='" + app + "'\n"
fd = os.open('.env', os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
with os.fdopen(fd, 'w') as f:
    f.write(content)
PY
fi
set -a
source .env
set +a
: "${MSSQL_SA_PASSWORD:?Agregar MSSQL_SA_PASSWORD a backend/.env}"
: "${MSSQL_APP_PASSWORD:?Agregar MSSQL_APP_PASSWORD a backend/.env}"
docker info >/dev/null
docker compose up -d --wait db
# Las contraseñas no se imprimen. SQL llega por stdin, no como argumento del comando.
python3 - <<'PY' | docker compose exec -T db sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b'
import os
password = os.environ['MSSQL_APP_PASSWORD'].replace("'", "''")
print("IF DB_ID(N'FarmeORTDemo') IS NULL CREATE DATABASE [FarmeORTDemo];\nGO")
print("IF SUSER_ID(N'farmeort_app') IS NULL CREATE LOGIN [farmeort_app] WITH PASSWORD=N'" + password + "';\nGO")
print("USE [FarmeORTDemo];\nGO")
print("IF DATABASE_PRINCIPAL_ID(N'farmeort_app') IS NULL CREATE USER [farmeort_app] FOR LOGIN [farmeort_app];\nGO")
print("IF IS_ROLEMEMBER(N'db_datareader', N'farmeort_app') <> 1 ALTER ROLE [db_datareader] ADD MEMBER [farmeort_app];")
print("IF IS_ROLEMEMBER(N'db_datawriter', N'farmeort_app') <> 1 ALTER ROLE [db_datawriter] ADD MEMBER [farmeort_app];\nGO")
print("SELECT SERVERPROPERTY('Edition') AS Edition, SERVERPROPERTY('ProductVersion') AS Version;\nGO")
PY
printf 'SQL Server Express listo en localhost:1433. Configuración en backend/.env (ignorada por Git).\n'
