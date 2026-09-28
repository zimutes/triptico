<#
    Remove o Tríptico: fecha-o, apaga a instalação, o atalho do menu Iniciar e o arranque
    com o Windows.

    Os perfis em %APPDATA%\Triptico ficam, a não ser que se use -Tudo: assim uma
    reinstalação volta a encontrá-los. Com -WhatIf mostra o que faria, sem apagar nada.
#>

[CmdletBinding(SupportsShouldProcess)]
param([Alias("All")][switch]$Tudo)

$ErrorActionPreference = "Continue"

$destino = Join-Path $env:LOCALAPPDATA "Programs\Triptico"
$atalho = Join-Path ([Environment]::GetFolderPath("Programs")) "Tríptico.lnk"
$dados = Join-Path $env:APPDATA "Triptico"
$run = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

function Feito($texto) { Write-Host "  $texto" -ForegroundColor Green }

# Apagar uma junção pelo caminho apaga o destino real: nesse caso não se toca em nada.
function Remover-Pasta($pasta, $descricao) {
    if (-not (Test-Path $pasta)) { return }
    $item = Get-Item $pasta -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        Write-Host "  $pasta é uma junção/ligação: não foi apagada" -ForegroundColor Yellow
        return
    }
    if ($PSCmdlet.ShouldProcess($pasta, "Apagar")) {
        Remove-Item $pasta -Recurse -Force
        Feito $descricao
    }
}

Write-Host "`nTríptico — remoção`n" -ForegroundColor White

$processos = Get-Process Triptico -ErrorAction SilentlyContinue
if ($processos -and $PSCmdlet.ShouldProcess("Triptico.exe", "Fechar")) {
    $processos | Stop-Process -Force
    Start-Sleep -Milliseconds 700
    Feito "app fechada"
}

if ((Test-Path $atalho) -and $PSCmdlet.ShouldProcess($atalho, "Apagar")) {
    Remove-Item $atalho -Force
    Feito "atalho do menu Iniciar removido"
}

if ((Get-ItemProperty $run -Name "Triptico" -ErrorAction SilentlyContinue) -and $PSCmdlet.ShouldProcess("$run\Triptico", "Apagar")) {
    Remove-ItemProperty $run -Name "Triptico"
    Feito "arranque com o Windows removido"
}

Remover-Pasta $destino "ficheiros removidos"

if ($Tudo) {
    Remover-Pasta $dados "perfis e definições removidos"
}
elseif (Test-Path $dados) {
    Write-Host "  perfis mantidos em $dados (usa -Tudo para os apagar)" -ForegroundColor Yellow
}

if ($WhatIfPreference) { Write-Host "`nSimulação: nada foi apagado.`n" -ForegroundColor White }
else { Write-Host "`nRemovido.`n" -ForegroundColor White }
