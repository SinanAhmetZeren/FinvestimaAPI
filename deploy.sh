#!/bin/bash
set -e

ACTIVE_FILE="/root/finvestima_active_color"
UPSTREAM_FILE="/etc/nginx/conf.d/finvestima_api_upstream.conf"
BLUE_PORT=6000
GREEN_PORT=6001
HEALTH_RETRIES=30
HEALTH_INTERVAL=2

if [ -f "$ACTIVE_FILE" ]; then
    ACTIVE=$(cat "$ACTIVE_FILE")
else
    ACTIVE="blue"
fi

if [ "$ACTIVE" = "blue" ]; then
    NEW="green"
    NEW_PORT=$GREEN_PORT
    OLD="blue"
else
    NEW="blue"
    NEW_PORT=$BLUE_PORT
    OLD="green"
fi

echo "=== Finvestima Blue-Green Deploy ==="
echo "Active: $ACTIVE → deploying: $NEW (port $NEW_PORT)"

cd /root/FinvestimaAPI

echo "Building api-$NEW..."
docker compose up --build -d api-$NEW

echo "Waiting for api-$NEW to be healthy..."
for i in $(seq 1 $HEALTH_RETRIES); do
    if curl -sf http://localhost:$NEW_PORT/api/health > /dev/null 2>&1; then
        echo "api-$NEW is healthy"
        break
    fi
    if [ $i -eq $HEALTH_RETRIES ]; then
        echo "ERROR: api-$NEW failed to become healthy. Aborting."
        docker compose stop api-$NEW
        exit 1
    fi
    echo "  attempt $i/$HEALTH_RETRIES..."
    sleep $HEALTH_INTERVAL
done

echo "Switching nginx to port $NEW_PORT..."
echo "upstream finvestima_api_backend { server localhost:$NEW_PORT; }" > $UPSTREAM_FILE
systemctl reload nginx

echo "$NEW" > "$ACTIVE_FILE"

echo "Waiting 15 seconds for in-flight requests to complete on api-$OLD..."
sleep 15

echo "Stopping api-$OLD..."
docker compose stop api-$OLD

echo "=== Deploy complete. Active: $NEW on port $NEW_PORT ==="
