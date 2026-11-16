# Build context = raiz del repo (fitadmin-iis). Ver docker-compose.yml
# ---- Etapa de compilacion ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/FitConnectAPI.csproj backend/
RUN dotnet restore backend/FitConnectAPI.csproj
COPY backend/ backend/
RUN dotnet publish backend/FitConnectAPI.csproj -c Release -o /app /p:UseAppHost=false

# ---- Etapa de ejecucion ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Kestrel escucha en 8080 dentro del contenedor (se mapea a 80 en el host)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FitConnectAPI.dll"]
