# Imagen única para Render: compila la web (Vue) y la API (.NET 10); la API sirve la web desde wwwroot.
# Los secretos (Mongo__ConnectionString, Gemini__ApiKey, Smtp__*, Seed__*) se configuran en Render, nunca aquí.

# 1. Web: vite build sale a ../src/TallerChat.Api/wwwroot (ver vite.config.ts)
FROM node:22-alpine AS web
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2. API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY src/TallerChat.Api/TallerChat.Api.csproj src/TallerChat.Api/
RUN dotnet restore src/TallerChat.Api/TallerChat.Api.csproj
COPY src/ src/
COPY --from=web /src/src/TallerChat.Api/wwwroot src/TallerChat.Api/wwwroot
RUN dotnet publish src/TallerChat.Api/TallerChat.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# 3. Ejecución
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
# Render termina TLS en su proxy: sin ForwardedHeaders todos los clientes comparten IP (límites de peticiones) y la
# cookie de sesión no se marca como Secure.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true
USER $APP_UID
EXPOSE 8080
# Render inyecta el puerto en PORT; en local se usa 8080.
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet TallerChat.Api.dll"]
