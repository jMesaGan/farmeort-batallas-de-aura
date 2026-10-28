FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/FarmeORT.Domain/FarmeORT.Domain.csproj src/FarmeORT.Domain/
COPY src/FarmeORT.Application/FarmeORT.Application.csproj src/FarmeORT.Application/
COPY src/FarmeORT.Infrastructure/FarmeORT.Infrastructure.csproj src/FarmeORT.Infrastructure/
COPY src/FarmeORT.Api/FarmeORT.Api.csproj src/FarmeORT.Api/
RUN dotnet restore src/FarmeORT.Api/FarmeORT.Api.csproj
COPY src/ src/
RUN dotnet publish src/FarmeORT.Api/FarmeORT.Api.csproj --no-restore -c Release -o /app /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
# Render inyecta PORT; Kestrel escucha en todas las interfaces.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet FarmeORT.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
