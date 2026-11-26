$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$directorioLogs = Join-Path $env:LOCALAPPDATA 'GIRA\ApiAnalitica'
New-Item -ItemType Directory -Path $directorioLogs -Force | Out-Null
Start-Transcript -Path (Join-Path $directorioLogs 'supervisor.log') -Append | Out-Null

try {
    Set-Location $raiz
    [Environment]::SetEnvironmentVariable(
        'Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command', 'Warning', 'Process')

    while ($true) {
        try {
            $puertoOcupado = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().
                GetActiveTcpListeners() | Where-Object { $_.Port -eq 5253 }

            if ($puertoOcupado) {
                Start-Sleep -Seconds 15
                continue
            }

            Write-Output "$(Get-Date -Format o) Iniciando API en el puerto 5253."
            & dotnet run --project GIRA.Api\GIRA.Api.csproj -c Release --no-build --no-restore --launch-profile http
            Write-Warning "$(Get-Date -Format o) La API terminó con código $LASTEXITCODE; se reintentará."
        }
        catch {
            Write-Warning "$(Get-Date -Format o) No se pudo supervisar o iniciar la API: $($_.Exception.Message)"
        }

        Start-Sleep -Seconds 15
    }
}
finally {
    Stop-Transcript | Out-Null
}
