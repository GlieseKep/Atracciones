# Empaqueta el código del backend (API + dev-auth + paquetes) en out/backend.zip para desplegarlo en Azure App Service.
# El mismo zip sirve para las dos Web Apps: Azure compila en el servidor (SCM_DO_BUILD_DURING_DEPLOYMENT=true)
# y cada app usa su propio comando de inicio.
#
# Uso: powershell -ExecutionPolicy Bypass -File tools\package-backend.ps1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$out = Join-Path $root 'out'
New-Item -ItemType Directory -Force $out | Out-Null
$zip = Join-Path $out 'backend.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }

Push-Location $root
try {
    # tar.exe de Windows 10+ (bsdtar) genera rutas con '/', compatibles con Linux; Compress-Archive no.
    # Ruta explícita: en los runners de GitHub el tar de Git (GNU, sin soporte zip) puede ir antes en el PATH.
    & (Join-Path $env:SystemRoot 'System32\tar.exe') -a -c -f $zip `
        --exclude=node_modules --exclude=dist --exclude=.env --exclude=*.tsbuildinfo --exclude=test `
        package.json package-lock.json tsconfig.base.json packages apps
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el zip.' }
} finally {
    Pop-Location
}
Write-Host ("Creado {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)) -ForegroundColor Green
