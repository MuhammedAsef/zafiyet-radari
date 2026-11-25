#!/usr/bin/env bash
# =============================================================================
# Zafiyet Radarı — sunucu ilk kurulumu. Root olarak bir kez çalıştırılır; tekrar çalıştırmak güvenlidir
# (var olan sırlara, sertifikaya ve anahtarlara dokunmaz).
#
#   bash zr-init.sh ghcr.io/<github-kullanici-adi>
#
# Yaptıkları:
#   1. /opt/zafiyet-radari: compose dosyası, .env (güçlü rastgele şifrelerle), klasörler
#   2. Data Protection sertifikası (anahtar halkasını şifreler)
#   3. /usr/local/bin/zr-deploy
#   4. "deploy" kullanıcısı: parolasız, sadece zr-deploy'u çalıştırabilen SSH anahtarı
#   5. GitHub'a girilecek secret değerlerini ekrana basar
# nginx ve sertifika adımları ayrıca, elle yapılır (KURULUM.md).
# =============================================================================
set -Eeuo pipefail
umask 077

readonly APP_DIR=/opt/zafiyet-radari
SRC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SRC_DIR
readonly APP_UID=1654   # .NET chiseled imajlarındaki "app" kullanıcısı

[[ $EUID -eq 0 ]] || { echo "Root olarak çalıştır." >&2; exit 1; }

image_prefix="${1:-}"
if [[ ! "$image_prefix" =~ ^ghcr\.io/[a-z0-9-]+$ ]]; then
  echo "Kullanım: bash zr-init.sh ghcr.io/<github-kullanici-adi>   (küçük harf)" >&2
  exit 64
fi

for f in docker-compose.prod.yml zr-deploy.sh; do
  [[ -f "$SRC_DIR/$f" ]] || { echo "$SRC_DIR/$f bulunamadı." >&2; exit 1; }
done

step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

# Büyük harf + küçük harf + rakam garanti (SQL Server CHECK_POLICY). Sadece harf/rakam:
# bağlantı cümlesini (;) ve .env değişken açılımını ($) bozacak karakter yok.
gen_password() {
  local core
  core="$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 32)"
  printf 'Zr%s9a' "$core"
}

# -----------------------------------------------------------------------------
step "Ağ çakışması kontrolü"
conflict="$(docker network ls -q \
  | xargs -r docker network inspect --format '{{.Name}} {{range .IPAM.Config}}{{.Subnet}} {{end}}' \
  | grep ' 172\.30\.0\.' | grep -v '^zafiyet-radari_edge ' || true)"
if [[ -n "$conflict" ]]; then
  echo "172.30.0.0/24 başka bir Docker ağında kullanılıyor: $conflict" >&2
  echo "docker-compose.prod.yml ve ReverseProxy__KnownProxies__0 değerini değiştir." >&2
  exit 1
fi
echo "172.30.0.0/24 uygun."

# -----------------------------------------------------------------------------
step "Klasörler ve compose dosyası"
install -d -m 750 -o root -g root "$APP_DIR"
install -d -m 750 -o root -g root "$APP_DIR/secrets"
install -d -m 755 -o root -g root "$APP_DIR/data"
install -d -m 700 -o "$APP_UID" -g "$APP_UID" "$APP_DIR/data/worker-tmp"
install -m 644 -o root -g root "$SRC_DIR/docker-compose.prod.yml" "$APP_DIR/docker-compose.prod.yml"
install -m 755 -o root -g root "$SRC_DIR/zr-deploy.sh" /usr/local/bin/zr-deploy
echo "Tamam."

# -----------------------------------------------------------------------------
step ".env (sırlar)"
if [[ -f "$APP_DIR/.env" ]]; then
  echo "Zaten var, dokunulmadı: $APP_DIR/.env"
else
  cat > "$APP_DIR/.env" <<EOF
# Zafiyet Radarı — production sırları. root:root 600. Repoya ve hiçbir yere kopyalanmaz.
PUBLIC_HOST=radar.muhammedasef.com
IMAGE_PREFIX=${image_prefix}

MSSQL_SA_PASSWORD=$(gen_password)
ZR_WEB_DB_PASSWORD=$(gen_password)
ZR_WORKER_DB_PASSWORD=$(gen_password)
DATAPROTECTION_CERT_PASSWORD=$(gen_password)

# Brevo > SMTP & API > SMTP sekmesi. Şifre: "SMTP key" (hesap şifren değil).
SMTP_HOST=smtp-relay.brevo.com
SMTP_PORT=587
SMTP_USERNAME=DEGISTIR
SMTP_PASSWORD=DEGISTIR
SMTP_FROM_ADDRESS=bildirim@muhammedasef.com
EOF
  chmod 600 "$APP_DIR/.env"
  echo "Oluşturuldu: $APP_DIR/.env  (SMTP_USERNAME ve SMTP_PASSWORD'ü sonra doldur)"
fi

# -----------------------------------------------------------------------------
step "Data Protection sertifikası"
pfx="$APP_DIR/secrets/dataprotection.pfx"
if [[ -f "$pfx" ]]; then
  echo "Zaten var, dokunulmadı: $pfx"
else
  cert_password="$(sed -n 's/^DATAPROTECTION_CERT_PASSWORD=//p' "$APP_DIR/.env")"
  work="$(mktemp -d)"
  trap 'rm -rf "$work"' EXIT
  openssl req -x509 -newkey rsa:3072 -sha256 -nodes -days 3650 \
    -subj "/CN=zafiyet-radari-dataprotection" \
    -keyout "$work/key.pem" -out "$work/cert.pem" 2>/dev/null
  CERT_PASSWORD="$cert_password" openssl pkcs12 -export \
    -inkey "$work/key.pem" -in "$work/cert.pem" -out "$pfx" -passout env:CERT_PASSWORD
  shred -u "$work/key.pem"
  # Konteyner "app" (UID 1654) olarak çalışır; dosyayı sadece o okuyabilir.
  chown "$APP_UID:$APP_UID" "$pfx"
  chmod 400 "$pfx"
  echo "Oluşturuldu: $pfx (10 yıl geçerli)"
fi

# -----------------------------------------------------------------------------
step "deploy kullanıcısı"
if ! id deploy >/dev/null 2>&1; then
  useradd --create-home --shell /bin/bash deploy
fi
passwd -l deploy >/dev/null   # parola ile giriş yok

cat > /etc/sudoers.d/zr-deploy <<'EOF'
# GitHub Actions deploy kullanıcısı sadece bu tek script'i root olarak çalıştırabilir.
deploy ALL=(root) NOPASSWD: /usr/local/bin/zr-deploy
EOF
chmod 440 /etc/sudoers.d/zr-deploy
visudo -cf /etc/sudoers.d/zr-deploy >/dev/null

install -d -m 700 -o deploy -g deploy /home/deploy/.ssh
auth=/home/deploy/.ssh/authorized_keys
touch "$auth"; chown deploy:deploy "$auth"; chmod 600 "$auth"

private_key=""
if grep -q 'github-actions@zafiyet-radari' "$auth"; then
  echo "Deploy anahtarı zaten kayıtlı. Yenisi gerekirse authorized_keys'ten satırı sil ve tekrar çalıştır."
else
  keydir="$(mktemp -d)"
  ssh-keygen -q -t ed25519 -N "" -C "github-actions@zafiyet-radari" -f "$keydir/id"
  # restrict: port/agent/X11 yönlendirme ve pty kapalı. command: başka komut çalıştırılamaz.
  # shellcheck disable=SC2016  # $SSH_ORIGINAL_COMMAND sshd tarafından açılmalı, burada değil.
  printf 'restrict,command="sudo -n /usr/local/bin/zr-deploy \\"$SSH_ORIGINAL_COMMAND\\"" %s\n' \
    "$(cat "$keydir/id.pub")" >> "$auth"
  private_key="$(cat "$keydir/id")"
  shred -u "$keydir/id"; rm -rf "$keydir"
fi

# -----------------------------------------------------------------------------
step "GitHub secret değerleri (Settings > Environments > production > Environment secrets)"
host_ip="$(curl -fsS -4 --max-time 5 https://ifconfig.me || hostname -I | awk '{print $1}')"

echo
echo "DEPLOY_HOST:"
echo "$host_ip"
echo
echo "DEPLOY_KNOWN_HOSTS:"
for f in /etc/ssh/ssh_host_ed25519_key.pub /etc/ssh/ssh_host_ecdsa_key.pub /etc/ssh/ssh_host_rsa_key.pub; do
  [[ -f "$f" ]] && printf '%s %s\n' "$host_ip" "$(cut -d' ' -f1,2 "$f")"
done
echo
if [[ -n "$private_key" ]]; then
  echo "DEPLOY_SSH_KEY (bir kez gösterilir, sunucuda saklanmadı):"
  echo "$private_key"
else
  echo "DEPLOY_SSH_KEY: daha önce oluşturuldu, tekrar gösterilemez."
fi
echo
echo "Sıradaki: $APP_DIR/.env içindeki SMTP değerlerini doldur, sonra nginx adımlarına geç (KURULUM.md)."
