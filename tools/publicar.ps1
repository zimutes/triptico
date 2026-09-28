# Compila o Tríptico num único .exe e instala-o para o utilizador atual:
#   %LOCALAPPDATA%\Programs\Triptico\Triptico.exe  + atalho no menu Iniciar.
# Usa o runtime .NET 10 Desktop já instalado. Para um .exe que corre em qualquer PC
# (sem .NET instalado, ~70 MB), usar: .\publicar.ps1 -Autonomo
param([switch]$Autonomo, [switch]$SemInstalar)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot
$projeto = Join-Path $raiz 'src\Triptico\Triptico.csproj'
$saida = Join-Path $raiz 'publicar'

$dotnetArgs = @('publish', $projeto, '-c', 'Release', '-r', 'win-x64', '-o', $saida,
          '-p:PublishSingleFile=true', '-p:DebugType=none')
if ($Autonomo) { $dotnetArgs += @('--self-contained', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true') }
else { $dotnetArgs += @('--no-self-contained') }

if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { throw 'A compilação falhou.' }
Write-Host "Publicado em $saida"
if ($SemInstalar) { return }

$destino = Join-Path $env:LOCALAPPDATA 'Programs\Triptico'
$aCorrer = Get-Process Triptico -ErrorAction SilentlyContinue
$aCorrer | Stop-Process -Force
Start-Sleep -Milliseconds 500
New-Item -ItemType Directory -Force $destino | Out-Null
Copy-Item (Join-Path $saida '*') $destino -Recurse -Force

$atalho = Join-Path ([Environment]::GetFolderPath('Programs')) 'Tríptico.lnk'
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($atalho)
$lnk.TargetPath = Join-Path $destino 'Triptico.exe'
$lnk.WorkingDirectory = $destino
$lnk.Description = 'Perfis de ecrãs · by zimutek'
$lnk.Save()

Write-Host "Instalado em $destino (atalho no menu Iniciar)."
if ($aCorrer) { Start-Process (Join-Path $destino 'Triptico.exe') -ArgumentList '--bandeja' }
