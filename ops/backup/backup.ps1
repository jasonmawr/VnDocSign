param([int]$Keep=7)

$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$out = "backups\$ts"
New-Item -ItemType Directory -Path $out -Force | Out-Null

Write-Host "[*] Dump MySQL..."
# Yêu cầu: bạn đã cài MySQL (và mysqldump có trong PATH)
& mysqldump -h $env:DB_HOST -u $env:DB_USER --password=$env:DB_PASS $env:DB_NAME --routines --triggers --single-transaction > "$out\db.sql"

Write-Host "[*] Archive ./data ..."
$paths = @(".\VnDocSign\VnDocSign.Api\data\*", ".\data\*")
$existing = $paths | Where-Object { Test-Path $_ }
if ($existing.Count -gt 0) {
  Compress-Archive -Path $existing -DestinationPath "$out\files.zip" -Force
} else {
  Write-Host "No data folders found to archive."
}

Write-Host "[*] Cleanup..."
$dirs = Get-ChildItem -Directory .\backups | Sort-Object LastWriteTime -Descending
$dirs | Select-Object -Skip $Keep | Remove-Item -Recurse -Force

Write-Host "[OK] Backup done: $out"
