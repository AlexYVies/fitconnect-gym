#!/usr/bin/env bash
# Espera a que SQL Server este listo y crea la BD (solo la primera vez).
set -u
SQLCMD=/opt/mssql-tools/bin/sqlcmd

echo "Esperando a SQL Server..."
for i in $(seq 1 40); do
  if $SQLCMD -S db -U sa -P "$SA_PASSWORD" -Q "SELECT 1" >/dev/null 2>&1; then
    echo "SQL Server listo."
    break
  fi
  echo "  intento $i/40..."
  sleep 3
done

# Idempotente: si la BD ya existe, no vuelve a correr el script.
EXISTS=$($SQLCMD -S db -U sa -P "$SA_PASSWORD" -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='FitConnectDB'" 2>/dev/null | tr -d '[:space:]')

if [ "$EXISTS" = "1" ]; then
  echo "FitConnectDB ya existe. Omito la creacion."
else
  echo "Creando FitConnectDB y datos iniciales..."
  $SQLCMD -S db -U sa -P "$SA_PASSWORD" -i /script.sql
  echo "Base de datos inicializada."
fi
