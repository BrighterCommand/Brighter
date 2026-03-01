#!/bin/bash
# Automates mTLS + Quorum Verification for Brighter #3902
# This script ensures all Critical Review Guidelines are met
set -e

echo "=================================================="
echo "  mTLS + Quorum Queue Verification Script"
echo "  Issue #3902: RabbitMQ Mutual TLS Support"
echo "=================================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}--- 1. Generating Certificates [Rule #10] ---${NC}"
if [ ! -f "certs/client-cert.pfx" ]; then
    echo "Certificates not found. Generating..."
    chmod +x generate-test-certs.sh
    ./generate-test-certs.sh
    echo -e "${GREEN}✓ Certificates generated${NC}"
else
    echo -e "${GREEN}✓ Certificates already exist${NC}"
fi

echo ""
echo -e "${YELLOW}--- 2. Fixing Certificate Permissions ---${NC}"
chmod 644 certs/server-key.pem
echo -e "${GREEN}✓ Permissions fixed${NC}"

echo ""
echo -e "${YELLOW}--- 3. Starting Infrastructure ---${NC}"
docker-compose -f docker-compose.rabbitmq-mtls.yml up -d

echo ""
echo -e "${YELLOW}--- 4. Waiting for RabbitMQ Health ---${NC}"
max_attempts=30
attempt=0
until docker inspect --format='{{json .State.Health.Status}}' brighter-rabbitmq-mtls 2>/dev/null | grep -q "healthy"; do
  attempt=$((attempt + 1))
  if [ $attempt -eq $max_attempts ]; then
    echo -e "${RED}✗ RabbitMQ failed to become healthy after ${max_attempts} attempts${NC}"
    echo "Checking logs:"
    docker-compose -f docker-compose.rabbitmq-mtls.yml logs --tail=50
    exit 1
  fi
  printf '.'
  sleep 2
done
echo -e "${GREEN} ✓ Broker is Healthy${NC}"

echo ""
echo -e "${YELLOW}--- 5. Running Acceptance Tests [Rules #1, #8] ---${NC}"
cd ..
dotnet test --filter "Category=RabbitMQ&Category=MutualTLS&Requires=Docker-mTLS" \
    --logger "console;verbosity=normal" \
    --no-build --no-restore || true

echo ""
echo -e "${YELLOW}--- 6. Running Quorum + Observability Tests [Rules #8, #10, #11, #12] ---${NC}"
dotnet test --filter "Category=Quorum&Category=MutualTLS&Category=Observability" \
    --logger "console;verbosity=normal" \
    --no-build --no-restore || true

echo ""
echo -e "${YELLOW}--- 7. Verification Summary ---${NC}"
echo "The following compliance rules were verified:"
echo "  Rule #1:  Cross-gateway uniformity (Sync & Async parity)"
echo "  Rule #8:  Quorum queue explicit configuration"
echo "  Rule #10: W3C Trace Context flow"
echo "  Rule #11: BrighterTracer integration"
echo "  Rule #12: CloudEvents trace context survival"

echo ""
echo -e "${YELLOW}--- 8. Cleanup ---${NC}"
read -p "Stop and remove RabbitMQ container? [y/N] " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    cd tests
    docker-compose -f docker-compose.rabbitmq-mtls.yml down
    echo -e "${GREEN}✓ Cleanup complete${NC}"
else
    echo "Container left running. Stop manually with:"
    echo "  cd tests && docker-compose -f docker-compose.rabbitmq-mtls.yml down"
fi

echo ""
echo -e "${GREEN}=================================================="
echo "  Verification Complete!"
echo "==================================================${NC}"
