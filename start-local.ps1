# Arranca el entorno local completo (solo desarrollo):
#   - Emisor OAuth2 de desarrollo  http://localhost:5280   (tools/dev-oauth/server.mjs)
#   - API + Swagger                http://localhost:5276/swagger
#   - Frontend (Vite)              http://localhost:5173
#
# Uso:  .\start-local.cmd            (o: powershell -ExecutionPolicy Bypass -File .\start-local.ps1)
#       .\start-local.cmd -Stop      detiene los tres servicios
#       .\start-local.cmd -NoBrowser no abre el navegador
param(
    [switch]$Stop,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$ports = @{ OAuth = 5280; Api = 5276; Web = 5173 }

function Stop-Port([int]$port) {
    Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique |
        ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
}

function Wait-Url([string]$url, [string]$name, [int]$seconds = 120) {
    for ($i = 0; $i -lt $seconds; $i++) {
        try {
            Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 | Out-Null
            return
        } catch {
            if ($_.Exception.Response) { return }   # responde (aunque sea 4xx): esta arriba
            Start-Sleep -Seconds 1
        }
    }
    throw "$name no respondio en $url tras $seconds s. Revisa su ventana."
}

function Start-LocalService([string]$title, [string]$workdir, [string]$command) {
    $script = "`$Host.UI.RawUI.WindowTitle = '$title'; Set-Location '$workdir'; $command"
    Start-Process powershell -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-Command', $script | Out-Null
}

if ($Stop) {
    $ports.Values | ForEach-Object { Stop-Port $_ }
    Write-Host 'Servicios locales detenidos.' -ForegroundColor Yellow
    return
}

foreach ($tool in 'node', 'npm', 'dotnet') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Falta '$tool' en el PATH." }
}

# Libera los puertos si quedo algo de una ejecucion anterior.
$ports.Values | ForEach-Object { Stop-Port $_ }

$web = Join-Path $root 'AtraccionesService-web'
if (-not (Test-Path (Join-Path $web 'node_modules'))) {
    Write-Host 'Instalando dependencias del frontend...' -ForegroundColor Cyan
    Push-Location $web; npm install --no-audit --no-fund; Pop-Location
}

Write-Host 'Compilando el API...' -ForegroundColor Cyan
dotnet build (Join-Path $root 'AtraccionesService.API\AtraccionesService.API.csproj') -v q -nologo
if ($LASTEXITCODE -ne 0) { throw 'La compilacion del API fallo.' }

Write-Host 'Iniciando servicios...' -ForegroundColor Cyan
Start-LocalService 'Dev OAuth :5280' $root 'node tools/dev-oauth/server.mjs'
Wait-Url "http://localhost:$($ports.OAuth)/.well-known/openid-configuration" 'El emisor OAuth2'

Start-LocalService 'API :5276' $root 'dotnet run --project AtraccionesService.API --launch-profile http --no-build'
Start-LocalService 'Web :5173' $web 'npm run dev'

Wait-Url "http://localhost:$($ports.Api)/health" 'El API'
node (Join-Path $root 'tools/dev-oauth/grant-admin.mjs')
Wait-Url "http://localhost:$($ports.Web)/" 'El frontend'

Write-Host ''
Write-Host 'Entorno local listo:' -ForegroundColor Green
Write-Host "  Frontend         http://localhost:$($ports.Web)"
Write-Host "  Swagger (API)    http://localhost:$($ports.Api)/swagger"
Write-Host "  OpenAPI JSON     http://localhost:$($ports.Api)/openapi/v1.json"
Write-Host "  Emisor OAuth2    http://localhost:$($ports.OAuth)"
Write-Host ''
Write-Host 'Usuarios de desarrollo (se eligen en la pantalla de login):'
Write-Host '  Cliente de desarrollo  -> compras, reservas y pagos (datos del seed)'
Write-Host '  Admin de desarrollo    -> ademas, area /admin y edicion del catalogo'
Write-Host ''
Write-Host 'Para detener todo:  .\start-local.cmd -Stop' -ForegroundColor Yellow

if (-not $NoBrowser) {
    Start-Process "http://localhost:$($ports.Web)"
    Start-Process "http://localhost:$($ports.Api)/swagger"
}
