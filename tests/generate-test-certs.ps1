# PowerShell script to generate test certificates for RabbitMQ mutual TLS
# Requires OpenSSL to be installed and available in PATH
# On Windows, install OpenSSL via:
#   - Chocolatey: choco install openssl
#   - Win32-OpenSSL: https://slproweb.com/products/Win32OpenSSL.html
#   - Git for Windows (includes OpenSSL in Git Bash)

$ErrorActionPreference = "Stop"

# Check if OpenSSL is available
try {
    $null = Get-Command openssl -ErrorAction Stop
}
catch {
    Write-Error @"
OpenSSL is not found in PATH. Please install OpenSSL:
  - Chocolatey: choco install openssl
  - Win32-OpenSSL: https://slproweb.com/products/Win32OpenSSL.html
  - Git for Windows (includes openssl.exe in Git\usr\bin)

After installation, restart PowerShell and try again.
"@
    exit 1
}

# Directory for certificates
$certDir = ".\certs"
if (-not (Test-Path $certDir)) {
    New-Item -ItemType Directory -Path $certDir | Out-Null
}

Write-Host "Generating test certificates for RabbitMQ mutual TLS..." -ForegroundColor Cyan
Write-Host ""

# 1. Generate CA private key and certificate
Write-Host "1. Generating CA certificate..." -ForegroundColor Yellow
& openssl genrsa -out "$certDir\ca-key.pem" 4096
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& openssl req -new -x509 -days 3650 -key "$certDir\ca-key.pem" `
    -out "$certDir\ca-cert.pem" `
    -subj "/CN=Brighter Test CA/O=Brighter/C=US"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 2. Generate server private key and CSR
Write-Host "2. Generating server certificate..." -ForegroundColor Yellow
& openssl genrsa -out "$certDir\server-key.pem" 4096
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& openssl req -new -key "$certDir\server-key.pem" `
    -out "$certDir\server-req.pem" `
    -subj "/CN=rabbitmq-mtls/O=Brighter/C=US"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Sign server certificate with CA
& openssl x509 -req -in "$certDir\server-req.pem" `
    -CA "$certDir\ca-cert.pem" -CAkey "$certDir\ca-key.pem" `
    -CAcreateserial -out "$certDir\server-cert.pem" -days 3650
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 3. Generate client private key and CSR
Write-Host "3. Generating client certificate..." -ForegroundColor Yellow
& openssl genrsa -out "$certDir\client-key.pem" 4096
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& openssl req -new -key "$certDir\client-key.pem" `
    -out "$certDir\client-req.pem" `
    -subj "/CN=brighter-test-client/O=Brighter/C=US"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Sign client certificate with CA
& openssl x509 -req -in "$certDir\client-req.pem" `
    -CA "$certDir\ca-cert.pem" -CAkey "$certDir\ca-key.pem" `
    -CAcreateserial -out "$certDir\client-cert.pem" -days 3650
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 4. Convert client certificate to PKCS#12 format (.pfx) for .NET
Write-Host "4. Converting client certificate to .pfx format..." -ForegroundColor Yellow
& openssl pkcs12 -export -out "$certDir\client-cert.pfx" `
    -inkey "$certDir\client-key.pem" `
    -in "$certDir\client-cert.pem" `
    -certfile "$certDir\ca-cert.pem" `
    -passout pass:test-password
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "✅ Certificates generated successfully in $certDir\" -ForegroundColor Green
Write-Host ""
Write-Host "Files created:" -ForegroundColor Cyan
Write-Host "  - ca-cert.pem (CA certificate)"
Write-Host "  - server-cert.pem + server-key.pem (RabbitMQ server)"
Write-Host "  - client-cert.pem + client-key.pem (Test client)"
Write-Host "  - client-cert.pfx (Test client in .pfx format, password: test-password)"
Write-Host ""
Write-Host "To start RabbitMQ with mTLS:" -ForegroundColor Cyan
Write-Host "  docker-compose -f docker-compose.rabbitmq-mtls.yml up -d"
Write-Host ""
Write-Host "To run mTLS tests:" -ForegroundColor Cyan
Write-Host "  `$env:RMQ_MTLS_ACCEPTANCE_TESTS='true'; dotnet test --filter Category=MutualTLS"
Write-Host ""
