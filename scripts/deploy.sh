#!/usr/bin/env bash
# scripts/deploy.sh
#
# Deploys a new image tag to the droplet's docker-compose stack and gates the
# deploy on a post-restart health check. Run from /opt/rideready on the
# droplet (where docker-compose.yml and its .env file live), e.g.:
#
#   IMAGE_NAME=ghcr.io/<owner>/<repo> ./scripts/deploy.sh <image-tag>
#
# Rollback procedure:
#   Each deploy re-pulls the branch's mutable tag (e.g. "main"), retagging it
#   over whatever image previously ran on this droplet — there is no local
#   "previous image" left to fall back to once a new deploy has pulled. To
#   roll back, pull a specific, immutable semantic-version tag from the
#   registry instead (release.yml pushes one alongside the branch tag on
#   every release, e.g. "1.4.2"). This script's own "Deploying ..." log line
#   only shows the branch tag deployed and when — it can't tell you which
#   version was behind that tag at any given moment. Find the actual
#   known-good version to roll back to from the repo's GitHub Releases page
#   (published automatically by release.yml's semantic-release step) or
#   `git tag` / `git log --oneline --decorate`, then SSH into the droplet
#   and run:
#
#     cd /opt/rideready
#     IMAGE_TAG=<previous-known-good-version-tag> docker compose up -d
#
set -euo pipefail

IMAGE_TAG="${1:?Usage: deploy.sh <image-tag>}"
export IMAGE_TAG

echo "Deploying ${IMAGE_NAME:-ghcr.io/OWNER/REPO}:${IMAGE_TAG}"

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

echo "Health check failed after deploy of ${IMAGE_NAME:-ghcr.io/OWNER/REPO}:${IMAGE_TAG}." >&2
echo "Roll back by pulling a known-good semantic-version tag from the registry:" >&2
echo "  IMAGE_TAG=<previous-known-good-version-tag> docker compose up -d" >&2
exit 1
