# syntax=docker/dockerfile:1.7
#
# Zafiyet Radarı — tek Dockerfile, üç hedef:
#   docker build --target web      -t zafiyet-radari-web .
#   docker build --target worker   -t zafiyet-radari-worker .
#   docker build --target migrator -t zafiyet-radari-migrator .
#
# Çalışma imajı: Ubuntu chiseled "extra" — kabuk, paket yöneticisi ve root kullanıcı yok (saldırı yüzeyi küçük),
# ICU ve tzdata var (CSV'deki tr-TR kültürü için gerekli). Varsayılan kullanıcı: app (UID 1654).

ARG DOTNET_VERSION=10.0

# ---------------------------------------------------------------------------
# Derleme
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
ENV DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

# Önce sadece proje dosyaları: kod değişse de restore katmanı önbellekten gelir.
COPY global.json Directory.Build.props Directory.Packages.props BannedSymbols.txt .editorconfig ./
COPY src/ZafiyetRadari.Domain/ZafiyetRadari.Domain.csproj                 src/ZafiyetRadari.Domain/
COPY src/ZafiyetRadari.Application/ZafiyetRadari.Application.csproj       src/ZafiyetRadari.Application/
COPY src/ZafiyetRadari.Infrastructure/ZafiyetRadari.Infrastructure.csproj src/ZafiyetRadari.Infrastructure/
COPY src/ZafiyetRadari.Web/ZafiyetRadari.Web.csproj                       src/ZafiyetRadari.Web/
COPY src/ZafiyetRadari.Worker/ZafiyetRadari.Worker.csproj                 src/ZafiyetRadari.Worker/
COPY src/ZafiyetRadari.Migrator/ZafiyetRadari.Migrator.csproj             src/ZafiyetRadari.Migrator/

# NuGet Audit burada da çalışır: zafiyetli paket varsa imaj oluşmaz.
RUN dotnet restore src/ZafiyetRadari.Web/ZafiyetRadari.Web.csproj \
 && dotnet restore src/ZafiyetRadari.Worker/ZafiyetRadari.Worker.csproj \
 && dotnet restore src/ZafiyetRadari.Migrator/ZafiyetRadari.Migrator.csproj

COPY src/ src/
COPY db/ db/

FROM build AS publish-web
RUN dotnet publish src/ZafiyetRadari.Web/ZafiyetRadari.Web.csproj -c Release -o /out --no-restore -p:UseAppHost=false

FROM build AS publish-worker
RUN dotnet publish src/ZafiyetRadari.Worker/ZafiyetRadari.Worker.csproj -c Release -o /out --no-restore -p:UseAppHost=false

FROM build AS publish-migrator
RUN dotnet publish src/ZafiyetRadari.Migrator/ZafiyetRadari.Migrator.csproj -c Release -o /out --no-restore -p:UseAppHost=false

# ---------------------------------------------------------------------------
# Çalışma imajları
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled-extra AS runtime
WORKDIR /app
ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    TZ=Europe/Istanbul
USER $APP_UID

FROM runtime AS web
LABEL org.opencontainers.image.title="zafiyet-radari-web"
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=publish-web /out .
ENTRYPOINT ["dotnet", "ZafiyetRadari.Web.dll"]

FROM runtime AS worker
LABEL org.opencontainers.image.title="zafiyet-radari-worker"
COPY --from=publish-worker /out .
ENTRYPOINT ["dotnet", "ZafiyetRadari.Worker.dll"]

FROM runtime AS migrator
LABEL org.opencontainers.image.title="zafiyet-radari-migrator"
COPY --from=publish-migrator /out .
ENTRYPOINT ["dotnet", "ZafiyetRadari.Migrator.dll"]
