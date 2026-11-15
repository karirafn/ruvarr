#!/bin/bash
set -e

REPO=/mnt/user/appdata/ruvarr-src
LOG_PREFIX="$(date '+%Y-%m-%d %H:%M:%S')"
DEPLOY_FLAG="--deploy"

cd "$REPO"

# `git reset --hard` replaces this file, but the running bash keeps executing the
# copy it already opened (and parsed), so any step after the reset would run the
# previous version. The detection phase therefore ends in `exec`, and the deploy
# phase starts from a fresh read of the updated script.
if [ "$1" != "$DEPLOY_FLAG" ]; then
    git fetch --quiet origin main

    LOCAL=$(git rev-parse HEAD)
    REMOTE=$(git rev-parse origin/main)

    if [ "$LOCAL" = "$REMOTE" ]; then
        exit 0
    fi

    echo "$LOG_PREFIX: Changes detected ($LOCAL -> $REMOTE), deploying..."

    git reset --hard origin/main
    git clean -fd

    exec /bin/bash "$REPO/deploy.sh" "$DEPLOY_FLAG"
fi

rm -rf /tmp/ruvarr
mkdir -p /tmp/ruvarr
git archive HEAD | tar -x -C /tmp/ruvarr

cd /tmp/ruvarr
docker build -t ruvarr:latest .

docker stop ruvarr && docker rm ruvarr
docker run -d --name ruvarr \
    --user 99:100 \
    -p 9484:8080 \
    -v /mnt/user/appdata/ruvarr:/app/data \
    -v /mnt/user/data/torrents:/downloads \
    --restart unless-stopped \
    ruvarr:latest

rm -rf /tmp/ruvarr
echo "$LOG_PREFIX: Deploy complete"
