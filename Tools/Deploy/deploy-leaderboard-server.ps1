<#
  deploy-leaderboard-server.ps1 - install or update the PIMLR leaderboard server on the VM.
    1. Creates the PRIVATE bucket gs://pimlr-world-ops on first run (holds server code; not public, not the game bucket)
    2. Uploads Tools\LeaderboardServer to gs://pimlr-world-ops/leaderboard/
    3. Runs install.sh on the VM over gcloud compute ssh (keeps data + admin password on updates)

  Usage:  powershell -ExecutionPolicy Bypass -File Tools\Deploy\deploy-leaderboard-server.ps1
          add -SkipVm to only upload (then run the printed command in the VM's SSH window)
#>
param(
  [string]$Project = "perfect-altar-409523",
  [string]$Bucket  = "pimlr-world-ops",
  [string]$Vm      = "instance-20250801-113609",
  [string]$Zone    = "us-central1-c",
  [switch]$SkipVm
)
$ErrorActionPreference = "Stop"
$src = (Resolve-Path (Join-Path $PSScriptRoot "..\LeaderboardServer")).Path
function Step($m) { Write-Host "`n== $m" -ForegroundColor Cyan }

Step "Checking bucket gs://$Bucket"
gcloud storage buckets describe "gs://$Bucket" --project $Project --format="value(name)" 2>$null | Out-Null
if ($LASTEXITCODE) {
  Write-Host "Creating private bucket gs://$Bucket (us-central1, public access blocked)"
  gcloud storage buckets create "gs://$Bucket" --project $Project --location us-central1 --uniform-bucket-level-access --public-access-prevention
  if ($LASTEXITCODE) { throw "Bucket creation failed" }
}

Step "Uploading $src"
# install.sh must keep Unix line endings or bash on the VM chokes on it
$sh = Join-Path $src "install.sh"
[IO.File]::WriteAllText($sh, ([IO.File]::ReadAllText($sh) -replace "`r`n", "`n"))
gcloud storage rsync --recursive --delete-unmatched-destination-objects --exclude="^data/" "$src" "gs://$Bucket/leaderboard"
if ($LASTEXITCODE) { throw "Upload failed" }

$remote = "rm -rf /tmp/pimlr-lb && mkdir -p /tmp/pimlr-lb && gcloud storage cp -r 'gs://$Bucket/leaderboard/*' /tmp/pimlr-lb/ && sudo bash /tmp/pimlr-lb/install.sh"
if ($SkipVm) { Write-Host "`nUploaded. In the VM's SSH window run:`n$remote" -ForegroundColor Yellow; exit 0 }

Step "Installing on $Vm"
gcloud compute ssh $Vm --zone $Zone --project $Project --command $remote
if ($LASTEXITCODE) { Write-Host "`nVM step failed. Uploaded OK - run this in the VM's SSH window instead:`n$remote" -ForegroundColor Yellow; exit 1 }
Write-Host "`nDone: https://pimlrworld.com/leaderboard  |  admin: https://pimlrworld.com/admin" -ForegroundColor Green
