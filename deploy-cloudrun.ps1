# =============================================================================
# Despliegue de TMS_API en Google Cloud Run
# =============================================================================
# Requisitos previos (una sola vez):
#   1. Instalar Google Cloud CLI: https://cloud.google.com/sdk/docs/install
#   2. Autenticarse:   gcloud auth login
#   3. (Opcional) Configurar Docker: gcloud auth configure-docker
#
# Uso:
#   .\deploy-cloudrun.ps1
# =============================================================================

$ErrorActionPreference = "Stop"

# --- Configuración -----------------------------------------------------------
$ProjectId = "apitms-500409"
$Region    = "europe-southwest1"   # Madrid. Cambia si prefieres otra región.
$Service   = "tms-api"

# Secretos (en producción, considera Google Secret Manager en lugar de texto plano)
$ApiUsername   = "********************"
$ApiPassword   = "********************"
$JwtSigningKey = "********************"
$LegacyToken   = "*********************"
# -----------------------------------------------------------------------------

Write-Host "==> Proyecto activo: $ProjectId" -ForegroundColor Cyan
gcloud config set project $ProjectId

Write-Host "==> Habilitando APIs necesarias (Cloud Run, Cloud Build, Artifact Registry)..." -ForegroundColor Cyan
gcloud services enable run.googleapis.com cloudbuild.googleapis.com artifactregistry.googleapis.com

Write-Host "==> Desplegando $Service en Cloud Run (build desde código fuente)..." -ForegroundColor Cyan
# --source . hace que Cloud Build construya la imagen a partir del Dockerfile y la publique.
gcloud run deploy $Service `
	--source . `
	--project $ProjectId `
	--region $Region `
	--platform managed `
	--allow-unauthenticated `
	--port 8080 `
	--set-env-vars "ASPNETCORE_ENVIRONMENT=Production,ApiAuth__Username=$ApiUsername,ApiAuth__Password=$ApiPassword,ApiAuth__JwtSigningKey=$JwtSigningKey,LegacyTms__Token=$LegacyToken"

Write-Host "==> Despliegue completado." -ForegroundColor Green
$Url = gcloud run services describe $Service --region $Region --format "value(status.url)"
Write-Host "URL del servicio: $Url" -ForegroundColor Green
Write-Host "Prueba de readiness: $Url/health/ready" -ForegroundColor Green
