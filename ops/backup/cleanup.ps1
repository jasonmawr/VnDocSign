param([int]$Keep=7)

$backupRoot = "backups"
if (-not (Test-Path $backupRoot)) {
  Write-Host "No backups folder found."
  exit
}

$dirs = Get-ChildItem -Directory $backupRoot | Sort-Object LastWriteTime -Descending
$dirs | Select-Object -Skip $Keep | Remove-Item -Recurse -Force
Write-Host "[OK] Kept $Keep most recent backups"
