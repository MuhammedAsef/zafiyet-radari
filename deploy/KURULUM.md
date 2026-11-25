# Canlıya alma (radar.muhammedasef.com)

Bu klasör, Zafiyet Radarı'nın VPS'e kurulması için gereken her şeyi içerir. Sunucu: Ubuntu 24.04,
Docker, sunucunun kendi nginx'i (TLS) ve certbot.

## Mimari

```
İnternet ──443──► nginx (TLS, rate limit) ──► 127.0.0.1:8080 ─► web ─┐
                                                   worker ─────────┤── db ağı (internal) ─► mssql
                                       migrator (her deploy'da bir kez) ┘
GitHub Actions ─ build/test/gitleaks ─► imaj ─ Trivy ─► GHCR ─► SSH (forced command) ─► zr-deploy
```

| Dosya | Sunucudaki yeri | Ne işe yarar |
|---|---|---|
| `docker-compose.prod.yml` | `/opt/zafiyet-radari/` | Servisler, ağlar, sertleştirme |
| `zr-deploy.sh` | `/usr/local/bin/zr-deploy` | Çek → migrate → başlat → sağlık kontrolü → gerekirse geri dön |
| `zr-init.sh` | (bir kez çalıştırılır) | Klasörler, sırlar, sertifika, deploy kullanıcısı |
| `nginx/*.conf` | `/etc/nginx/...` | Site ve ortak proxy ayarları |

## Güvenlik kararları

- **Sırlar** sadece `/opt/zafiyet-radari/.env` (root, 600) içinde. Repoda, imajda ve CI'da yok.
- **Veritabanı** dışarıya port açmaz; `db` ağı `internal` (internete çıkışı da yok). Web `zr_web`,
  Worker `zr_worker` login'iyle bağlanır; `sa` sadece migrator konteynerinde.
- **Portlar** sadece `127.0.0.1`'e yayınlanır. Docker'ın yayınladığı portlar ufw'yu atladığı için bu zorunlu.
- **Konteynerler** root olmayan kullanıcıyla (UID 1654), salt okunur dosya sisteminde, tüm Linux
  yetkileri düşürülmüş (`cap_drop: ALL`) ve `no-new-privileges` ile çalışır. İmajda kabuk yok.
- **Data Protection anahtarları** veritabanında, sunucudaki bir sertifikayla şifreli durur.
- **Deploy kullanıcısı** parolasız. SSH anahtarı forced command ile sadece `zr-deploy` çalıştırabilir;
  `zr-deploy` argümanı katı biçimde doğrular. Registry token'ı kalıcı değil: işin `GITHUB_TOKEN`'ı
  stdin'den gelir, geçici bir Docker config'iyle kullanılır ve silinir.
- **CI**: action'lar SHA ile sabitli; imaj, yamalı HIGH/CRITICAL açık varsa yayınlanmaz; her imaj
  için CycloneDX SBOM üretilir (Zafiyet Radarı'na envanter olarak yüklenebilir).
- **Proxy**: nginx istemcinin gönderdiği `X-Forwarded-For`'u atar; uygulama bu başlığı sadece
  Docker ağ geçidinden (172.30.0.1) kabul eder.

## Kurulum

### 1. Dosyaları sunucuya kopyala (Windows PowerShell)

```powershell
cd E:\zafiyet-radari
scp -r deploy root@158.220.103.146:/root/zr-files
```

### 2. İlk kurulum (sunucu)

```bash
bash /root/zr-files/zr-init.sh ghcr.io/<github-kullanici-adi>
```

Çıktıdaki `DEPLOY_HOST`, `DEPLOY_KNOWN_HOSTS` ve `DEPLOY_SSH_KEY` değerlerini bir sonraki adım için sakla.
Özel anahtar sunucuda tutulmaz, sadece bir kez gösterilir.

Sonra SMTP değerlerini doldur:

```bash
nano /opt/zafiyet-radari/.env
```

### 3. GitHub

Repo → Settings → Environments → **New environment**: `production` → Environment secrets:

| Secret | Değer |
|---|---|
| `DEPLOY_HOST` | zr-init çıktısı |
| `DEPLOY_KNOWN_HOSTS` | zr-init çıktısı (tüm satırlar) |
| `DEPLOY_SSH_KEY` | zr-init çıktısı (BEGIN/END satırları dahil) |

İsteğe bağlı: aynı ekranda **Required reviewers** ile her deploy'u elle onaylı yap.

### 4. nginx ve sertifika (sunucu)

```bash
install -m 644 /root/zr-files/nginx/zr-proxy.conf /etc/nginx/snippets/zr-proxy.conf
install -d -m 755 /var/www/certbot

# Önce sadece HTTP: Let's Encrypt doğrulaması için
install -m 644 /root/zr-files/nginx/bootstrap-http.conf /etc/nginx/sites-available/radar.muhammedasef.com
ln -sf /etc/nginx/sites-available/radar.muhammedasef.com /etc/nginx/sites-enabled/radar.muhammedasef.com
nginx -t && systemctl reload nginx

certbot certonly --webroot -w /var/www/certbot -d radar.muhammedasef.com \
  --non-interactive --agree-tos --deploy-hook "systemctl reload nginx"

# Sertifika geldi: asıl site dosyası
install -m 644 /root/zr-files/nginx/radar.muhammedasef.com.conf /etc/nginx/sites-available/radar.muhammedasef.com
nginx -t && systemctl reload nginx
certbot renew --dry-run
```

### 5. İlk deploy

`main` dalına push et. Actions → **CI/CD** iş akışında sırayla `Build ve test`, `gitleaks`,
üç imaj ve `Deploy` çalışır. Elle tetiklemek için: Actions → CI/CD → **Run workflow**.

### 6. Kontrol ve ilk admin

```bash
cd /opt/zafiyet-radari
alias zr='docker compose --project-directory /opt/zafiyet-radari -f docker-compose.prod.yml --env-file .env --env-file release.env'

zr ps
zr logs --tail 50 web
curl -sI https://radar.muhammedasef.com | head -n 5
```

Siteden kayıt ol, sonra kendine Admin rolü ver:

```bash
zr run --rm migrator --grant-admin senin@adresin.com
```

Zafiyet verisi Worker'ın zamanlanmış işleriyle gelir. Beklemek istemezsen: `/ops/jobs` →
Recurring Jobs → `vuln-sync-*` → **Trigger now**.

## Günlük işler

| İş | Komut (sunucuda, `zr` alias'ı ile) |
|---|---|
| Durum | `zr ps` |
| Loglar | `zr logs -f --tail 100 web worker` |
| Yeniden başlat | `zr restart web worker` |
| Çalışan sürüm | `cat /opt/zafiyet-radari/release.env` |
| Veritabanı yedeği | `zr exec mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(sed -n 's/^MSSQL_SA_PASSWORD=//p' .env)" -C -Q "BACKUP DATABASE ZafiyetRadari TO DISK='/var/opt/mssql/data/zr.bak' WITH INIT"` |
