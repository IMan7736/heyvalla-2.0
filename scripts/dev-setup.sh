#!/usr/bin/env bash
# One-time local setup: creates infra/.env with a random password and stores the same password
# in dotnet user-secrets for the API. Safe to re-run; it never overwrites an existing infra/.env.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"

if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
  export PATH="$HOME/.dotnet:$PATH"
fi

env_file="$root/infra/.env"
if [ ! -f "$env_file" ]; then
  password="$(openssl rand -hex 16)"
  printf 'POSTGRES_USER=dhathuru\nPOSTGRES_PASSWORD=%s\n' "$password" > "$env_file"
  chmod 600 "$env_file"
  echo "Created infra/.env"
fi

password="$(grep '^POSTGRES_PASSWORD=' "$env_file" | cut -d= -f2-)"
dotnet user-secrets set "Database:Password" "$password" --project "$root/apps/api/src/Dhathuru.Api" >/dev/null
echo "Stored Database:Password in user-secrets for Dhathuru.Api"

# Local-only random secrets (user-secrets, never committed). Existing values are kept so tickets stay verifiable.
project="$root/apps/api/src/Dhathuru.Api"
existing="$(dotnet user-secrets list --project "$project" 2>/dev/null || true)"
set_secret() { # name, generator-command
  if ! grep -q "^$1 = " <<<"$existing"; then
    dotnet user-secrets set "$1" "$(eval "$2")" --project "$project" >/dev/null
    echo "Generated $1"
  fi
}
set_secret "Bookings:Tickets:Keys:k1" "openssl rand -base64 32"
set_secret "Bookings:Verification:OperatorKey" "openssl rand -hex 24"
if ! grep -q "^Bookings:Tickets:ActiveKeyId = " <<<"$existing"; then
  dotnet user-secrets set "Bookings:Tickets:ActiveKeyId" "k1" --project "$project" >/dev/null
fi
if ! grep -q "^Bookings:EnableSimulation = " <<<"$existing"; then
  dotnet user-secrets set "Bookings:EnableSimulation" "true" --project "$project" >/dev/null # dev only: delay/disruption simulation
fi
[ -f "$root/apps/web/.env.local" ] || { cp "$root/apps/web/.env.example" "$root/apps/web/.env.local"; echo "Created apps/web/.env.local"; }
echo "Next: pnpm infra:up"
