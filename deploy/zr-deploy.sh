#!/usr/bin/env bash
# =============================================================================
# Zafiyet Radarı — sunucu tarafı deploy (/usr/local/bin/zr-deploy, root:root 755)
#
# GitHub Actions bu script'i "deploy" kullanıcısıyla SSH üzerinden çağırır. O kullanıcının
# anahtarı authorized_keys'te forced command ile kısıtlıdır: başka hiçbir komut çalıştıramaz.
#   restrict,command="sudo -n /usr/local/bin/zr-deploy \"$SSH_ORIGINAL_COMMAND\"" ssh-ed25519 ...
#
# Argüman : sha-<40 haneli commit>   (başka her şey reddedilir)
# stdin   : "<ghcr-kullanıcı> <token>" — iş bitince geçersizleşen GITHUB_TOKEN.
#           Sunucuda saklanmaz; geçici DOCKER_CONFIG ile kullanılıp silinir.
#
# Akış: imajları çek → migrator → web/worker'ı yeni sürümle başlat → /health → başarısızsa önceki sürüme dön.
# Not: Şema script'leri ileriye dönüktür; geri dönüşte eski kodun yeni şemayla çalışabilmesi için
#      migration'lar geriye uyumlu yazılır (önce ekle, sonraki sürümde kaldır).
# =============================================================================
set -Eeuo pipefail
umask 077

readonly APP_DIR=/opt/zafiyet-radari
readonly LOCK_FILE=/run/lock/zr-deploy.lock
readonly COMPOSE=(docker compose
  --project-directory "$APP_DIR"
  -f "$APP_DIR/docker-compose.prod.yml"
  --env-file "$APP_DIR/.env"
  --env-file "$APP_DIR/release.env")

log() { printf '[zr-deploy] %s\n' "$*"; }
fail() { printf '[zr-deploy] HATA: %s\n' "$*" >&2; exit "${2:-1}"; }

# --- Girdi doğrulama --------------------------------------------------------------
tag="${1:-}"
[[ "$tag" =~ ^sha-[0-9a-f]{40}$ ]] || fail "Geçersiz sürüm etiketi." 64

exec 9>"$LOCK_FILE"
flock -n 9 || fail "Başka bir deploy sürüyor." 75

read -r -t 15 registry_user registry_token || fail "Kayıt defteri kimliği okunamadı." 65
[[ "$registry_user" =~ ^[A-Za-z0-9-]{1,39}$ ]] || fail "Geçersiz kullanıcı adı." 65
[[ -n "$registry_token" ]] || fail "Token boş." 65

public_host="$(sed -n 's/^PUBLIC_HOST=//p' "$APP_DIR/.env" | head -n1)"
[[ -n "$public_host" ]] || fail ".env içinde PUBLIC_HOST yok."
if grep -q '=DEGISTIR$' "$APP_DIR/.env"; then
  fail ".env içinde doldurulmamış değer var (DEGISTIR)."
fi

# --- Sürüm dosyası -----------------------------------------------------------------
previous=""
if [[ -f "$APP_DIR/release.env" ]]; then
  previous="$(sed -n 's/^RELEASE_TAG=//p' "$APP_DIR/release.env" | head -n1)"
fi

write_release() {
  printf 'RELEASE_TAG=%s\n' "$1" > "$APP_DIR/release.env.tmp"
  mv -f "$APP_DIR/release.env.tmp" "$APP_DIR/release.env"
}

rollback() {
  if [[ -n "$previous" && "$previous" != "$tag" ]]; then
    log "Önceki sürüme dönülüyor: $previous"
    write_release "$previous"
    "${COMPOSE[@]}" up -d --no-deps web worker || true
  else
    write_release "${previous:-$tag}"
  fi
}

# --- İmajları çek (geçici kimlikle) -----------------------------------------------
docker_config="$(mktemp -d)"
trap 'rm -rf "$docker_config"' EXIT
export DOCKER_CONFIG="$docker_config"

printf '%s' "$registry_token" | docker login ghcr.io -u "$registry_user" --password-stdin >/dev/null \
  || fail "ghcr.io girişi başarısız."
unset registry_token

write_release "$tag"
log "İmajlar çekiliyor: $tag"
if ! "${COMPOSE[@]}" --profile tools pull --quiet migrator web worker; then
  docker logout ghcr.io >/dev/null 2>&1 || true
  rollback
  fail "İmajlar çekilemedi."
fi
docker logout ghcr.io >/dev/null 2>&1 || true

# --- Veritabanı ------------------------------------------------------------------
"${COMPOSE[@]}" up -d mssql
log "Migrator çalışıyor."
if ! "${COMPOSE[@]}" run --rm migrator; then
  rollback
  fail "Migrator başarısız."
fi

# --- Uygulama ------------------------------------------------------------------
log "Web ve Worker başlatılıyor."
"${COMPOSE[@]}" up -d --remove-orphans web worker

healthy=false
for _ in $(seq 1 45); do
  if curl -fsS --max-time 3 -H "Host: $public_host" http://127.0.0.1:8080/health >/dev/null 2>&1; then
    healthy=true
    break
  fi
  sleep 2
done

if [[ "$healthy" != true ]]; then
  "${COMPOSE[@]}" logs --tail 60 web >&2 || true
  rollback
  fail "Sağlık kontrolü 90 saniyede geçmedi."
fi

# --- Temizlik: sadece çalışan ve bir önceki sürüm kalır -----------------------------
docker images --format '{{.Repository}}:{{.Tag}}' \
  | grep -E '/zafiyet-radari-(web|worker|migrator):sha-' \
  | grep -v -e ":${tag}\$" -e ":${previous:-none}\$" \
  | xargs -r docker rmi >/dev/null 2>&1 || true
docker image prune -f >/dev/null 2>&1 || true

log "Yayında: $tag"
