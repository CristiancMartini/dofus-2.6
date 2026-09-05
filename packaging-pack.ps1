# Pack Dofus Local runtime + prepare release assets
$ErrorActionPreference = "Stop"
$Root = "E:\Projetos-teste\server-nikito"
$Stage = Join-Path $Root "packaging\stage"
$Out = Join-Path $Root "packaging\out"

Get-Process Stump.GUI*,mysqld,Dofus -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Stage, $Out | Out-Null

Write-Host "== Staging runtime =="
$dirs = @(
  @{ Src = "tools\mariadb-10.4.34-winx64"; Dst = "tools\mariadb-10.4.34-winx64" },
  @{ Src = "tools\mariadb-data"; Dst = "tools\mariadb-data" },
  @{ Src = "Stump\trunk\Run\Debug\AuthServer"; Dst = "Stump\trunk\Run\Debug\AuthServer" },
  @{ Src = "Stump\trunk\Run\Debug\WorldServer"; Dst = "Stump\trunk\Run\Debug\WorldServer" },
  @{ Src = "Stump\trunk\Run\static"; Dst = "Stump\trunk\Run\static" }
)

foreach ($d in $dirs) {
  $src = Join-Path $Root $d.Src
  $dst = Join-Path $Stage $d.Dst
  Write-Host "Copy $($d.Src) ..."
  New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
  robocopy $src $dst /E /XD logs /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $($d.Src) code $LASTEXITCODE" }
}

# Clean DB lock/pid/log files
Get-ChildItem (Join-Path $Stage "tools\mariadb-data") -Filter "*.pid" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem (Join-Path $Stage "tools\mariadb-data") -Filter "*.err" -ErrorAction SilentlyContinue | Remove-Item -Force
$aria = Join-Path $Stage "tools\mariadb-data\aria_log_control"
# keep aria files; just remove ib_logfile if locked issues - leave as is

# Placeholder my.ini (installer overwrites paths)
$ini = @"
[mysqld]
basedir=PLACEHOLDER/tools/mariadb-10.4.34-winx64
datadir=PLACEHOLDER/tools/mariadb-data
port=3306
bind-address=127.0.0.1
skip-name-resolve
character-set-server=utf8
collation-server=utf8_general_ci
default-storage-engine=InnoDB
sql_mode=NO_ENGINE_SUBSTITUTION
max_allowed_packet=64M
innodb_buffer_pool_size=128M

[client]
port=3306
host=127.0.0.1
plugin-dir=PLACEHOLDER/tools/mariadb-10.4.34-winx64/lib/plugin
"@
Set-Content -Path (Join-Path $Stage "tools\mariadb-data\my.ini") -Value $ini -Encoding ASCII

# Marker
Set-Content -Path (Join-Path $Stage "VERSION.txt") -Value "DofusLocal 2.6.2 / Stump`r`nv1.0.0" -Encoding ASCII

Write-Host "== Zipping runtime (this takes a few minutes) =="
$runtimeZip = Join-Path $Out "DofusLocal-Runtime.zip"
if (Test-Path $runtimeZip) { Remove-Item $runtimeZip -Force }
Push-Location $Stage
try {
  tar -a -cf $runtimeZip *
} finally {
  Pop-Location
}
Write-Host ("Runtime zip: {0:N1} MB" -f ((Get-Item $runtimeZip).Length/1MB))

Write-Host "== Preparing client zip =="
$clientZip = Join-Path $Out "Dofus-Client.zip"
$srcClient = Join-Path $Root "Dofus 2.6.ZIP"
if (Test-Path $clientZip) { Remove-Item $clientZip -Force }
Copy-Item $srcClient $clientZip
Write-Host ("Client zip: {0:N1} MB" -f ((Get-Item $clientZip).Length/1MB))

# Rebuild setup exe
cmd /c (Join-Path $Root "packaging\installer\build.cmd")
Write-Host "DONE"
Get-ChildItem $Out | Format-Table Name, @{N='MB';E={[math]::Round($_.Length/1MB,1)}} -AutoSize
