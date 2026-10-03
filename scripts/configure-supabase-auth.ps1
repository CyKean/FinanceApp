param(
    [string]$ProjectRoot = ".",
    [string]$AccessToken = $env:SUPABASE_ACCESS_TOKEN,
    [string]$SiteUrl,
    [switch]$Apply
)

# Applies the Supabase Auth settings Finora needs.
#
# Why this exists: Supabase defaults SITE_URL to http://localhost:3000 and email
# confirmation to ON. The confirmation email therefore contains a localhost link,
# which no phone can open - the account is created but can never be used.
#
# Finora is offline-first: credentials live in the device's LocalUsers table and
# the Supabase user is only a sync mirror, so there is nothing for an inbox round
# trip to prove. Confirmation is turned OFF and the account is auto-confirmed,
# which removes the emailed link entirely and lets sync start on first sign-in.
#
# Requires a personal access token with auth:write (Owner or Administrator):
#   https://supabase.com/dashboard/account/tokens
# Usage:
#   powershell -File scripts\configure-supabase-auth.ps1                 # show the diff
#   powershell -File scripts\configure-supabase-auth.ps1 -Apply          # apply it
#   powershell -File scripts\configure-supabase-auth.ps1 -Apply -SiteUrl "https://app.example.com"

$ErrorActionPreference = "Stop"
$root = (Resolve-Path $ProjectRoot).Path
$refFile = Join-Path $root "supabase\.temp\project-ref"

if (-not (Test-Path -LiteralPath $refFile)) {
    Write-Host "No linked Supabase project found at $refFile. Run 'supabase link' first."
    exit 1
}

$projectRef = (Get-Content -LiteralPath $refFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($projectRef)) {
    Write-Host "$refFile is empty. Run 'supabase link' first."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    Write-Host "No access token. Set `$env:SUPABASE_ACCESS_TOKEN or pass -AccessToken."
    Write-Host "Create one at https://supabase.com/dashboard/account/tokens (needs auth:write)."
    exit 1
}

# Fall back to the project's own URL so an emailed link always resolves to a real
# host, even for the flows (password recovery) that still send mail.
if ([string]::IsNullOrWhiteSpace($SiteUrl)) {
    $SiteUrl = "https://$projectRef.supabase.co"
}

$headers = @{
    "Authorization" = "Bearer $AccessToken"
    "Content-Type"  = "application/json"
}
$endpoint = "https://api.supabase.com/v1/projects/$projectRef/config/auth"

Write-Host "Project: $projectRef"

try {
    $current = Invoke-RestMethod -Uri $endpoint -Headers $headers -Method Get
}
catch {
    Write-Host "Could not read the current auth config: $($_.Exception.Message)"
    Write-Host "The token needs auth:read, and PATCH /config/auth additionally needs Owner or Administrator."
    exit 1
}

# Only the keys this script owns. Everything else on the project is left alone.
$desired = [ordered]@{
    mailer_autoconfirm = $true
    site_url           = $SiteUrl
}

$changes = @()
foreach ($key in $desired.Keys) {
    $before = $current.$key
    $after = $desired[$key]
    if ($before -ne $after) {
        $changes += [pscustomobject]@{ Key = $key; Current = $before; Desired = $after }
    }
}

if ($changes.Count -eq 0) {
    Write-Host "OK: auth already configured for offline-first (mailer_autoconfirm=true, site_url=$SiteUrl)"
    exit 0
}

Write-Host ""
Write-Host "Pending changes:"
$changes | ForEach-Object {
    Write-Host ("  {0}: {1} -> {2}" -f $_.Key, $(if ($null -eq $_.Current) { "<unset>" } else { $_.Current }), $_.Desired)
}

if (-not $Apply) {
    Write-Host ""
    Write-Host "Dry run. Re-run with -Apply to change the project."
    exit 0
}

$body = ($desired | ConvertTo-Json -Compress)
try {
    Invoke-RestMethod -Uri $endpoint -Headers $headers -Method Patch -Body $body | Out-Null
}
catch {
    Write-Host "Update failed: $($_.Exception.Message)"
    Write-Host "PATCH /config/auth requires an Owner or Administrator token."
    exit 1
}

Write-Host ""
Write-Host "Applied. Verifying with the public auth settings endpoint..."

$settings = Invoke-RestMethod -Uri "https://$projectRef.supabase.co/auth/v1/settings" -Method Get -Headers @{ apikey = "public" }

# mailer_autoconfirm is served publicly, so this check needs no token at all.
if ($settings.mailer_autoconfirm -eq $true) {
    Write-Host "OK: mailer_autoconfirm=true - registration no longer sends a confirmation link."
}
else {
    Write-Host "WARNING: mailer_autoconfirm is still false. Confirm the change in the dashboard."
    exit 1
}

Write-Host "Note: existing unconfirmed users still need to be confirmed or deleted before they can sign in."
exit 0