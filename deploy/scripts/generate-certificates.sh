#!/usr/bin/env bash
# Creates the OpenIddict signing and encryption certificates (ADR-0003) in deploy/secrets/, protected with
# IDENTITY_CERTIFICATE_PASSWORD from deploy/.env. Run once per environment from deploy/; needs openssl.
# Replacing them invalidates every issued token and refresh token (users log in again).
set -euo pipefail

cd "$(dirname "$0")/.."
# .env is read as shell, so keep its values free of spaces and $ (the generated passwords are).
# shellcheck disable=SC1091
set -a; . ./.env; set +a
: "${IDENTITY_CERTIFICATE_PASSWORD:?IDENTITY_CERTIFICATE_PASSWORD must be set in deploy/.env}"

umask 077
mkdir -p secrets
trap 'rm -f secrets/*.key secrets/*.crt' EXIT
for kind in signing encryption; do
  if [ -f "secrets/$kind.pfx" ]; then
    echo "secrets/$kind.pfx exists, keeping it"
    continue
  fi
  usage=$([ "$kind" = signing ] && echo digitalSignature || echo keyEncipherment)
  openssl req -x509 -newkey rsa:2048 -sha256 -days 1825 -nodes \
    -subj "/CN=Trekky $kind" -addext "keyUsage=critical,$usage" \
    -keyout "secrets/$kind.key" -out "secrets/$kind.crt"
  openssl pkcs12 -export -inkey "secrets/$kind.key" -in "secrets/$kind.crt" \
    -out "secrets/$kind.pfx" -passout env:IDENTITY_CERTIFICATE_PASSWORD
  echo "created secrets/$kind.pfx"
done

# Readable by you and by the API container's user (UID/GID 1654, `app` in the .NET images), nobody else.
chmod 750 secrets
chmod 640 secrets/*.pfx
if ! chgrp -R 1654 secrets 2>/dev/null; then
  echo "Now run: sudo chgrp -R 1654 $(pwd)/secrets   (lets the API container read the certificates)"
fi
