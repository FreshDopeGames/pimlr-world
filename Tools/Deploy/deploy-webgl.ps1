<#
  deploy-webgl.ps1 - PIMLR World WebGL release
    1. Guards the build (refuses failed or partial builds)
    2. Uploads to gs://pimlr-world-playtest and sets the Brotli headers
    3. Syncs the VM folder that nginx serves at https://pimlrworld.com/play/
    4. Checks the live site sends Content-Encoding: br

  "Name Files As Hashes" is OFF (it caused Unity's "buildprogram run 6 times" failure),
  so files keep the same names every build and are served with no-cache (revalidate),
  never long-term caching - otherwise playtesters would be stuck on an old build.

  Usage:
    powershell -ExecutionPolicy Bypass -File Tools\Deploy\deploy-webgl.ps1
    powershell -ExecutionPolicy Bypass -File Tools\Deploy\deploy-webgl.ps1 -DryRun     # checks only
    powershell -ExecutionPolicy Bypass -File Tools\Deploy\deploy-webgl.ps1 -SkipVm     # bucket only
#>
param(
  [string]$BuildDir = (Join-Path $PSScriptRoot "..\..\Build"),
  [string]$Bucket   = "pimlr-world-playtest",
  [string]$Project  = "perfect-altar-409523",
  [string]$Vm       = "instance-20250801-113609",
  [string]$Zone     = "us-central1-c",
  [string]$VmDir    = "/var/www/pimlrworld/play",
  [string]$SiteUrl  = "https://pimlrworld.com/",
  [switch]$SkipVm,
  [switch]$DryRun
)
$ErrorActionPreference = "Stop"
function Step($m) { Write-Host "`n== $m" -ForegroundColor Cyan }
function Fail($m) { Write-Host "STOP: $m" -ForegroundColor Red; exit 1 }

# ---------- 1. Guard ----------
if (-not (Test-Path $BuildDir)) { Fail "Build folder not found: $BuildDir" }
$BuildDir  = (Resolve-Path $BuildDir).Path
$inner     = Join-Path $BuildDir "Build"
$indexPath = Join-Path $BuildDir "index.html"
Step "Checking build in $BuildDir"

if (-not (Test-Path $indexPath)) { Fail "No index.html - the build did not finish." }
$html = Get-Content $indexPath -Raw
if ($html -match "%UNITY_|\{\{\{") {
  Fail "index.html still has template placeholders - the build failed, or the WebGL Template is not set to PIMLR."
}

$files = @{}
foreach ($ext in "loader.js", "data.br", "framework.js.br", "wasm.br") {
  $found = @(Get-ChildItem $inner -Filter "*.$ext" -File -ErrorAction SilentlyContinue)
  if ($found.Count -eq 0) { Fail "Missing Build\*.$ext - the build did not finish." }
  if ($found.Count -gt 1) { Fail "More than one Build\*.$ext - old files are mixed in: $($found.Name -join ', '). Move the old ones out of Build\Build and rerun." }
  if ($html -notmatch [regex]::Escape($found[0].Name)) { Fail "index.html does not reference $($found[0].Name) - it belongs to a different build." }
  $files[$ext] = $found[0]
}
$stale = $files.Values | Where-Object { ($files["wasm.br"].LastWriteTime - $_.LastWriteTime).TotalHours -gt 2 }
if ($stale) { Write-Host "WARNING: these files are hours older than the wasm - check they're from this build: $($stale.Name -join ', ')" -ForegroundColor Yellow }
if (([regex]::Matches($html, "microphone\.js")).Count -gt 1) {
  Write-Host "WARNING: microphone.js is included twice in index.html." -ForegroundColor Yellow
}

$built = $files["wasm.br"].LastWriteTime
$mb    = [math]::Round(((Get-ChildItem $BuildDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host ("OK: built {0:g} ({1:N1} h ago), {2} MB total" -f $built, ((Get-Date) - $built).TotalHours, $mb) -ForegroundColor Green
foreach ($k in $files.Keys) { Write-Host "   $($files[$k].Name)" }

if ($DryRun) { Write-Host "`nDry run - nothing uploaded." -ForegroundColor Yellow; exit 0 }

# ---------- 2. Bucket ----------
$dest = "gs://$Bucket"
Step "Uploading to $dest"
gcloud storage rsync --recursive --delete-unmatched-destination-objects "$BuildDir" "$dest"
if ($LASTEXITCODE) { Fail "Upload to the bucket failed." }

Step "Setting Brotli headers"
$cache = "no-cache"   # same filenames every build -> browsers must revalidate
$updates = @(
  @("$dest/Build/*.wasm.br",         "application/wasm",         "br"),
  @("$dest/Build/*.framework.js.br", "application/javascript",   "br"),
  @("$dest/Build/*.data.br",         "application/octet-stream", "br"),
  @("$dest/Build/*.loader.js",       "application/javascript",   "")
)
foreach ($u in $updates) {
  if ($u[2]) { gcloud storage objects update $u[0] --content-type=$($u[1]) --content-encoding=$($u[2]) --cache-control=$cache }
  else       { gcloud storage objects update $u[0] --content-type=$($u[1]) --cache-control=$cache }
  if ($LASTEXITCODE) { Fail "Header update failed for $($u[0])" }
}
gcloud storage objects update "$dest/index.html" --content-type="text/html" --cache-control="no-cache"
if ($LASTEXITCODE) { Fail "Header update failed for index.html" }

if ($SkipVm) { Write-Host "`nBucket updated (VM skipped): https://storage.googleapis.com/$Bucket/index.html" -ForegroundColor Green; exit 0 }

# ---------- 3. VM ----------
Step "Syncing $Vm -> $VmDir"
$remote = "sudo mkdir -p $VmDir && sudo gcloud storage rsync --recursive --delete-unmatched-destination-objects gs://$Bucket $VmDir && sudo chown -R www-data:www-data $VmDir && echo VM-SYNC-OK"
gcloud compute ssh $Vm --zone $Zone --project $Project --command $remote
if ($LASTEXITCODE) { Fail "VM sync failed. The bucket IS updated - you can rerun with the SSH button: sudo gcloud storage rsync --recursive --delete-unmatched-destination-objects gs://$Bucket $VmDir" }

# ---------- 4. Live check ----------
Step "Checking $SiteUrl"
try {
  $page = Invoke-WebRequest -Uri $SiteUrl -Method Head -UseBasicParsing
  $wasm = Invoke-WebRequest -Uri ($SiteUrl + "Build/" + $files["wasm.br"].Name) -Method Head -UseBasicParsing
} catch { Fail "Live check failed: $($_.Exception.Message)" }
$enc = ($wasm.Headers["Content-Encoding"] -join "")
if ($enc -ne "br") { Fail "wasm is served without Content-Encoding: br (got '$enc') - check /etc/nginx/snippets/pimlr-play.conf" }

Write-Host "`nLIVE: $SiteUrl  (page $($page.StatusCode), wasm Content-Encoding: br)" -ForegroundColor Green
