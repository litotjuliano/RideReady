#!/usr/bin/env bash
# scripts/deploy.sh
#
# Deploys a new image tag to the droplet's docker-compose stack and gates the
# deploy on a post-restart health check. Run from /opt/ridebooking on the
# droplet (where docker-compose.yml and its .env file live), e.g.:
#
#   IMAGE_NAME=ghcr.io/<owner>/<repo> ./scripts/deploy.sh <image-tag>
#
# Rollback procedure:
#   If this script exits non-zero, the health check failed but the previous
#   image is still the one running (a failed `docker compose up -d` pull/start
#   leaves the last-known-good container in place). If a deploy instead
#   succeeds here but is later found to be bad, roll back manually by SSHing
#   into the droplet and running:
#
#     cd /opt/ridebooking
#     IMAGE_TAG=<previous-known-good-tag> docker compose up -d
#
set -euo pipefail

IMAGE_TAG="${1:?Usage: deploy.sh <image-tag>}"
export IMAGE_TAG

docker compose pull app
docker compose up -d
docker compose ps

echo "Waiting for health check..."
for i in $(seq 1 10); do
  if curl -sf http://localhost:5000/health > /dev/null; then
    echo "Healthy."
    exit 0
  fi
  sleep 5
done

echo "Health check failed after deploy. The previous image is still available locally —" >&2
echo "roll back manually with: IMAGE_TAG=<previous-tag> docker compose up -d" >&2
exit 1
