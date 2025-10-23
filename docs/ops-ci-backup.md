# Ops/CI/Backup – VnDocSign

## 1️⃣ Health
- `GET /health` → trả `{ "status": "Healthy|Degraded" }` (ping DB).
- Logs: xem console hoặc file Serilog (tùy config).

## 2️⃣ Backup/Restore
- Cấu hình ENV: `DB_HOST`, `DB_USER`, `DB_PASS`, `DB_NAME`.
- Chạy backup (Windows):
  ```powershell
  $env:DB_HOST="127.0.0.1"
  $env:DB_USER="root"
  $env:DB_PASS="123456"
  $env:DB_NAME="VnDoc"
  .\ops\backup\backup.ps1 -Keep 7
