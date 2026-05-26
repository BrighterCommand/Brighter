#!/bin/bash
# Setup script for the Spanner emulator used by Brighter integration tests.
# Prerequisites: docker compose -f docker-compose-spanner.yaml up -d

set -euo pipefail

PROJECT_ID="${GOOGLE_CLOUD_PROJECT:-brighter-tests}"
INSTANCE_ID="brighter-spanner"
DATABASE_ID="brightertests"
EMULATOR_HOST="${SPANNER_EMULATOR_HOST:-localhost:9010}"
# The emulator exposes gRPC on port 9010 and REST/HTTP on port 9020.
# Client libraries use the gRPC port; health checks and admin REST calls use 9020.
REST_HOST="${EMULATOR_HOST%:*}:9020"

# Export environment variables for the current shell
export SPANNER_EMULATOR_HOST="$EMULATOR_HOST"
export GOOGLE_CLOUD_PROJECT="$PROJECT_ID"

echo "SPANNER_EMULATOR_HOST=$SPANNER_EMULATOR_HOST"
echo "GOOGLE_CLOUD_PROJECT=$GOOGLE_CLOUD_PROJECT"

# Wait for the emulator to be ready (REST endpoint on port 9020)
echo "Waiting for Spanner emulator REST API at $REST_HOST..."
for i in $(seq 1 30); do
    if curl -s "http://$REST_HOST" > /dev/null 2>&1; then
        echo "Emulator is ready."
        break
    fi
    if [ "$i" -eq 30 ]; then
        echo "ERROR: Spanner emulator did not start within 30 seconds."
        exit 1
    fi
    sleep 1
done

# Create the instance
echo "Creating instance '$INSTANCE_ID'..."
curl -s -X POST "http://$REST_HOST/v1/projects/$PROJECT_ID/instances" \
    -H "Content-Type: application/json" \
    -d "{
        \"instanceId\": \"$INSTANCE_ID\",
        \"instance\": {
            \"config\": \"emulator-config\",
            \"displayName\": \"Brighter Test Instance\",
            \"nodeCount\": 1
        }
    }" | head -1
echo ""

# Create the database
echo "Creating database '$DATABASE_ID'..."
curl -s -X POST "http://$REST_HOST/v1/projects/$PROJECT_ID/instances/$INSTANCE_ID/databases" \
    -H "Content-Type: application/json" \
    -d "{
        \"createStatement\": \"CREATE DATABASE \`$DATABASE_ID\`\"
    }" | head -1
echo ""

echo "Spanner emulator setup complete."
echo ""
echo "To use in your shell, run:"
echo "  export SPANNER_EMULATOR_HOST=$EMULATOR_HOST"
echo "  export GOOGLE_CLOUD_PROJECT=$PROJECT_ID"
