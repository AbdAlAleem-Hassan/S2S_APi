#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

if [ -z "${BACKEND_IMAGE:-}" ]; then
  : "${GHCR_OWNER:?Set GHCR_OWNER, for example: export GHCR_OWNER=hedra-nabil}"
  GHCR_OWNER="$(printf '%s' "$GHCR_OWNER" | tr '[:upper:]' '[:lower:]')"
  TAG="${TAG:-$(git -C "$ROOT_DIR" rev-parse --short HEAD 2>/dev/null || date +%Y%m%d%H%M%S)}"
  BACKEND_IMAGE="ghcr.io/${GHCR_OWNER}/s2sai-backend:${TAG}"
fi

BACKEND_IMAGE="$(printf '%s' "$BACKEND_IMAGE" | tr '[:upper:]' '[:lower:]')"

docker build \
  -f "$ROOT_DIR/Dockerfile" \
  -t "$BACKEND_IMAGE" \
  "$ROOT_DIR"

if [ "${SKIP_PUSH:-0}" != "1" ]; then
  docker push "$BACKEND_IMAGE"
fi

echo "BACKEND_IMAGE=$BACKEND_IMAGE"
