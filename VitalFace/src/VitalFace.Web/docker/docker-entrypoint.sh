#!/bin/sh
# Blazor WASM's config (wwwroot/appsettings.Production.json) is fetched by the browser at
# runtime, not baked into the .wasm bundle at build time — so it's safe to regenerate it from
# env vars on every container start, letting one image serve any API URL without a rebuild
# (e.g. a Cloudflare quick-tunnel URL that only exists after the container is already running).
set -eu

: "${API_BASE_URL:=http://localhost:8080/}"
: "${VITALFACE_LOCATION_ID:=Postazione non configurata}"
export API_BASE_URL VITALFACE_LOCATION_ID

# shellcheck disable=SC2016 -- single quotes are intentional: envsubst takes this as a literal
# "only substitute these names" filter, not a shell-expanded string.
envsubst '${API_BASE_URL} ${VITALFACE_LOCATION_ID}' \
  < /usr/share/nginx/html/appsettings.Production.template.json \
  > /usr/share/nginx/html/appsettings.Production.json

exec "$@"
