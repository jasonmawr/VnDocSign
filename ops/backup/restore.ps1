param([string]$BackupFolder)

if (-not (Test-Path $BackupFolder)) {
  Write-Host "Usage: restore.ps1 <backup_folder>"
  exit
}

Write-Host "[*] Restoring database..."
& mysql -h $env:DB_HOST -u $env:DB_USER --password=$env:DB_PASS $env:DB_NAME < "$BackupFolder\db.sql"

Write-Host "[*] Restoring files..."
Expand-Archive -Path "$BackupFolder\files.zip" -DestinationPath "." -Force

Write-Host "[OK] Restore done from $BackupFolder"
