#!/bin/bash
set -e

REPO=/mnt/user/appdata/ruvarr
LOG_PREFIX="$(date '+%Y-%m-%d %H:%M:%S')"

cd "$REPO"
git fetch origin main

LOCAL=$(git rev-parse HEAD)
REMOTE=$(git rev-parse origin/main)

if [ "$LOCAL" != "$REMOTE" ]; then
    echo "$LOG_PREFIX: Changes detected ($LOCAL -> $REMOTE), deploying..."

    git reset --hard origin/main
    git clean -fd

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
        -v /mnt/user/downloads:/downloads \
        --restart unless-stopped \
        ruvarr:latest

    rm -rf /tmp/ruvarr
    echo "$LOG_PREFIX: Deploy complete"
fi
