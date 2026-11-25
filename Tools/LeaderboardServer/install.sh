#!/bin/bash
# PIMLR leaderboard - install or update on the VM. Safe to re-run (keeps data and the admin password).
#   sudo bash install.sh
set -euo pipefail
SRC="$(cd "$(dirname "$0")" && pwd)"
APP=/opt/pimlr-leaderboard
DATA=/var/lib/pimlr-leaderboard
ENVF=/etc/pimlr-leaderboard.env
SITE=/etc/nginx/sites-available/default

say() { echo -e "\n== $*"; }

say "Checking Node.js"
NODE="$(command -v node || true)"
[ -n "$NODE" ] || { echo "Node.js not found. Install with: sudo apt-get install -y nodejs"; exit 1; }
MAJOR="$("$NODE" -p 'process.versions.node.split(".")[0]')"
[ "$MAJOR" -ge 18 ] || { echo "Node $MAJOR is too old (need 18+)."; exit 1; }
echo "node $("$NODE" -v) at $NODE"

say "Installing app to $APP (data stays in $DATA)"
id pimlrlb >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin pimlrlb
mkdir -p "$APP" "$DATA"
cp -r "$SRC/server.js" "$SRC/public" "$APP/"
chown -R root:root "$APP"; chmod -R a+rX "$APP"
chown -R pimlrlb:pimlrlb "$DATA"; chmod 750 "$DATA"

if [ ! -f "$ENVF" ]; then
  PW="$(head -c 18 /dev/urandom | base64 | tr -d '/+=' | head -c 20)"
  printf 'PORT=12100\nHOST=127.0.0.1\nDATA_DIR=%s\nADMIN_PASSWORD=%s\nMIN_STORY_SECONDS=120\n' "$DATA" "$PW" > "$ENVF"
  chmod 600 "$ENVF"
  NEWPW="$PW"
fi

say "systemd service pimlr-leaderboard"
cat > /etc/systemd/system/pimlr-leaderboard.service <<EOF
[Unit]
Description=PIMLR World leaderboard API (port 12100)
After=network.target

[Service]
EnvironmentFile=$ENVF
ExecStart=$NODE $APP/server.js
WorkingDirectory=$APP
User=pimlrlb
Restart=always
RestartSec=3
NoNewPrivileges=true
ProtectSystem=strict
ReadWritePaths=$DATA
PrivateTmp=true

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable pimlr-leaderboard >/dev/null
systemctl restart pimlr-leaderboard
sleep 2
systemctl is-active pimlr-leaderboard

say "nginx routes: /api/, /leaderboard, /rules, /privacy, /admin"
cat > /etc/nginx/snippets/pimlr-api.conf <<'EOF'
# PIMLR leaderboard API + pages -> Node on 127.0.0.1:12100 (service: pimlr-leaderboard)
location ^~ /api/ {
    proxy_pass http://127.0.0.1:12100;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header Authorization $http_authorization;
    client_max_body_size 128k;
}
location ~ ^/(leaderboard/?|rules|privacy|admin/?)$ {
    proxy_pass http://127.0.0.1:12100;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header Authorization $http_authorization;
}
EOF
cp "$SITE" "$SITE.bak-api-$(date +%Y%m%d%H%M)"
grep -q 'pimlr-api.conf' "$SITE" || sed -i '/include snippets\/pimlr-play.conf;/a\    include snippets/pimlr-api.conf;' "$SITE"
grep -q 'pimlr-api.conf' "$SITE" || { echo "Could not add the include line (no pimlr-play.conf include found)."; exit 1; }
nginx -t
systemctl reload nginx

say "Daily backup at 3:17 AM (keeps 30 days in /var/backups/pimlr-leaderboard)"
mkdir -p /var/backups/pimlr-leaderboard
cat > /etc/cron.d/pimlr-leaderboard-backup <<EOF
17 3 * * * root tar -czf /var/backups/pimlr-leaderboard/data-\$(date +\%Y\%m\%d).tgz -C $DATA . && find /var/backups/pimlr-leaderboard -name 'data-*.tgz' -mtime +30 -delete
EOF

say "Health check"
curl -s http://127.0.0.1:12100/api/health; echo
curl -s -o /dev/null -w "public https://pimlrworld.com/api/health -> %{http_code}\n" https://pimlrworld.com/api/health
curl -s -o /dev/null -w "public https://pimlrworld.com/leaderboard -> %{http_code}\n" https://pimlrworld.com/leaderboard

if [ -n "${NEWPW:-}" ]; then
  echo -e "\n*** ADMIN PASSWORD (save it now - shown only once): $NEWPW"
  echo "*** Admin page: https://pimlrworld.com/admin  (any username, this password)"
  echo "*** To see it again later: sudo grep ADMIN_PASSWORD $ENVF"
fi
echo -e "\nDone."
