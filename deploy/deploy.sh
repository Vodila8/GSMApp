#!/usr/bin/env bash
set -Eeuo pipefail

export HOME=/root
export DOTNET_CLI_HOME=/root

REPO_DIR="/opt/gsm"
PUBLISH_DIR="$REPO_DIR/publish"
AGENT_DIR="$REPO_DIR/gsm-agent"
AGENT_ZIP="$PUBLISH_DIR/wwwroot/downloads/gsm-agent.zip"

cd "$REPO_DIR"
exec 9>/var/lock/gsm-deploy.lock
flock -n 9 || exit 0

git -c safe.directory="$REPO_DIR" fetch origin main
git -c safe.directory="$REPO_DIR" merge --ff-only origin/main

dotnet restore "$REPO_DIR/gsm/gsm.csproj"

connection="$(sed -n 's/^ConnectionStrings__DefaultConnection=//p' /etc/gsm.env)"
if [[ -z "$connection" ]]; then
    echo "ConnectionStrings__DefaultConnection is missing from /etc/gsm.env" >&2
    exit 1
fi

if command -v dotnet-ef >/dev/null 2>&1; then
    dotnet-ef database update --project "$REPO_DIR/gsm/gsm.csproj" --startup-project "$REPO_DIR/gsm/gsm.csproj" --connection "$connection"
elif [[ -x /root/.dotnet/tools/dotnet-ef ]]; then
    /root/.dotnet/tools/dotnet-ef database update --project "$REPO_DIR/gsm/gsm.csproj" --startup-project "$REPO_DIR/gsm/gsm.csproj" --connection "$connection"
else
    echo "dotnet-ef is not installed" >&2
    exit 1
fi

dotnet publish "$REPO_DIR/gsm/gsm.csproj" --configuration Release --output "$PUBLISH_DIR" --no-restore

mkdir -p "$(dirname "$AGENT_ZIP")"
rm -f "$AGENT_ZIP"
(
    cd "$AGENT_DIR"
    zip -qr "$AGENT_ZIP" . -x 'bin/*' 'obj/*' 'publish/*'
)

mkdir -p "$PUBLISH_DIR/DataProtectionKeys"
chown -R gsm:gsm "$PUBLISH_DIR"
chmod 700 "$PUBLISH_DIR/DataProtectionKeys"

systemctl restart gsm
systemctl is-active --quiet gsm
printf 'GSM deployment completed: %s\n' "$(git rev-parse --short HEAD)"
