#!/bin/bash
set -e

# Directory for certificates
CERT_DIR="./certs"
mkdir -p "$CERT_DIR"

echo "Generating test certificates for RabbitMQ mutual TLS..."

# 1. Generate CA private key and certificate
echo "1. Generating CA certificate..."
openssl genrsa -out "$CERT_DIR/ca-key.pem" 4096
openssl req -new -x509 -days 3650 -key "$CERT_DIR/ca-key.pem" \
    -out "$CERT_DIR/ca-cert.pem" \
    -subj "/CN=Brighter Test CA/O=Brighter/C=US"

# 2. Generate server private key and CSR
echo "2. Generating server certificate..."
openssl genrsa -out "$CERT_DIR/server-key.pem" 4096
openssl req -new -key "$CERT_DIR/server-key.pem" \
    -out "$CERT_DIR/server-req.pem" \
    -subj "/CN=rabbitmq-mtls/O=Brighter/C=US"

# Sign server certificate with CA
openssl x509 -req -in "$CERT_DIR/server-req.pem" \
    -CA "$CERT_DIR/ca-cert.pem" -CAkey "$CERT_DIR/ca-key.pem" \
    -CAcreateserial -out "$CERT_DIR/server-cert.pem" -days 3650

# 3. Generate client private key and CSR
echo "3. Generating client certificate..."
openssl genrsa -out "$CERT_DIR/client-key.pem" 4096
openssl req -new -key "$CERT_DIR/client-key.pem" \
    -out "$CERT_DIR/client-req.pem" \
    -subj "/CN=brighter-test-client/O=Brighter/C=US"

# Sign client certificate with CA
openssl x509 -req -in "$CERT_DIR/client-req.pem" \
    -CA "$CERT_DIR/ca-cert.pem" -CAkey "$CERT_DIR/ca-key.pem" \
    -CAcreateserial -out "$CERT_DIR/client-cert.pem" -days 3650

# 4. Convert client certificate to PKCS#12 format (.pfx) for .NET
echo "4. Converting client certificate to .pfx format..."
openssl pkcs12 -export -out "$CERT_DIR/client-cert.pfx" \
    -inkey "$CERT_DIR/client-key.pem" \
    -in "$CERT_DIR/client-cert.pem" \
    -certfile "$CERT_DIR/ca-cert.pem" \
    -passout pass:test-password

echo "✅ Certificates generated successfully in $CERT_DIR/"
echo ""
echo "Files created:"
echo "  - ca-cert.pem (CA certificate)"
echo "  - server-cert.pem + server-key.pem (RabbitMQ server)"
echo "  - client-cert.pem + client-key.pem (Test client)"
echo "  - client-cert.pfx (Test client in .pfx format, password: test-password)"
echo ""
echo "To start RabbitMQ with mTLS:"
echo "  docker-compose -f docker-compose.rabbitmq-mtls.yml up -d"
echo ""
echo "To run mTLS tests:"
echo "  RMQ_MTLS_ACCEPTANCE_TESTS=true dotnet test --filter Category=MutualTLS"
