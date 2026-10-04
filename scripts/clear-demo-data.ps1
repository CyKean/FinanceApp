param(
    [string]$Package = "com.financeapp.mobile",
    [string]$AdbPath = "",
    [switch]$Purge,
    [switch]$WhatIf
)

# Removes demo data from a device's local SQLite database.
#
# The app no longer seeds demo data (see DevDataSeeder), but a database written
# by an earlier build still holds it, and afterwards the app cannot tell a seeded
# row from a real one. This deletes the rows the old seeder inserted and leaves
# everything a real user would have created.
#
# Deletes: Transactions, Accounts, Budgets, RecurringTransactions and
#          FinancialGoals matching the names the seeder used, plus the
#          SyncOperations rows that pointed at them.
# Keeps:   LocalUsers (your sign-in), Categories (app defaults - without them no
#          transaction can be recorded), preferences, settings, history files.
#
# Usage:
#   powershell -File scripts\clear-demo-data.ps1 -WhatIf   # count only, change nothing
#   powershell -File scripts\clear-demo-data.ps1          # delete the demo rows
#   powershell -File scripts\clear-demo-data.ps1 -Purge   # wipe the whole app
#
# -Purge runs "pm clear", which deletes EVERYTHING including your local account,
# so you will have to register again.

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($AdbPath)) {
    $found = Get-Command adb.exe -ErrorAction SilentlyContinue
    if ($found) {
        $AdbPath = $found.Source
    }
    else {
        # The standard SDK location, then winget's per-user package folder, which
        # is where `winget install Google.PlatformTools` puts it.
        $AdbPath = @(
            "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
            "$env:ProgramFiles\Android\platform-tools\adb.exe"
        ) + @(Get-ChildItem -Path "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Filter adb.exe -Recurse -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty FullName) |
               Where-Object { Test-Path -LiteralPath $_ } |
               Select-Object -First 1
    }
}

if (-not $AdbPath) {
    Write-Host "adb not found. Pass -AdbPath, or add it to PATH."
    exit 1
}

function Invoke-Adb([string[]]$arguments) {
    $output = & $AdbPath @arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "adb $($arguments -join ' ') failed: $output" }
    return $output
}

$devices = (Invoke-Adb @("devices")) | Select-String -Pattern "\sdevice$"
if (-not $devices) {
    Write-Host "No device connected. Plug the phone in with USB debugging on."
    exit 1
}

# adb separates columns with a tab, so split on any whitespace.
$device = ($devices | Select-Object -First 1).ToString() -split '\s+' | Select-Object -First 1
Write-Host "Device: $device"

$installed = Invoke-Adb @("shell", "pm", "list", "packages")
if ($installed -notmatch [regex]::Escape($Package)) {
    Write-Host "$Package is not installed on $device."
    $matches = $installed | Select-String "finance|finora"
    if ($matches) {
        Write-Host "Installed packages that look related:"
        $matches | ForEach-Object { Write-Host "  $_" }
    }
    exit 1
}

if ($Purge) {
    Write-Host ""
    Write-Host "PURGE: deleting ALL app data, including your local account."
    Write-Host "You will need to register again."
    Invoke-Adb @("shell", "pm", "clear", $Package) | ForEach-Object { Write-Host $_ }
    exit 0
}

# run-as only works on a debuggable build, which is what `dotnet build` produces.
$probe = & $AdbPath -s $device shell "run-as $Package sh -c 'echo ok'" 2>&1
if ($LASTEXITCODE -ne 0 -or ($probe -join '') -notmatch "ok") {
    Write-Host "Cannot open the app's private data."
    Write-Host "run-as needs a debuggable build - install a Debug APK, or use -Purge."
    exit 1
}

$db = (& $AdbPath -s $device shell "run-as $Package sh -c 'ls files/*.db'" 2>&1 | Select-Object -First 1).ToString().Trim()
if ([string]::IsNullOrWhiteSpace($db)) {
    Write-Host "No .db found in the app's files directory."
    exit 1
}

Write-Host "Database: $db"
Write-Host ""

# Built up separately: an inline join inside the SQL here-string needs nested
# quoting that PowerShell will not parse.
$seededNames = @(
    "Cash Wallet", "BDO Savings", "GCash Wallet",
    "Food & Groceries", "Transportation", "Shopping", "Entertainment",
    "Emergency Fund", "New Laptop",
    "Netflix & Spotify", "Gym Membership", "Family Allowance"
)
$nameList = ($seededNames | ForEach-Object { "'" + $_ + "'" }) -join ", "

$seededNotes = @(
    "Monthly salary", "Freelance project", "Apartment rent",
    "Electricity & water bill", "Internet & phone bill", "Streaming subscriptions",
    "Groceries", "Lunch, coffee & food delivery", "Jeepney & Grab fare",
    "Clothes & gadgets", "Movies & games", "Pharmacy & clinic visit",
    "Concert tickets", "Laptop replacement"
)
$noteList = ($seededNotes | ForEach-Object { "'" + $_ + "'" }) -join ", "

$statements = @(
    "SELECT 'accounts      ' || COUNT(*) FROM Accounts WHERE Name IN ($nameList);"
    "SELECT 'transactions ' || COUNT(*) FROM Transactions WHERE Notes IN ($noteList);"
    "SELECT 'budgets       ' || COUNT(*) FROM Budgets WHERE Name IN ($nameList);"
    "SELECT 'goals         ' || COUNT(*) FROM FinancialGoals WHERE Name IN ($nameList);"
    "SELECT 'recurring     ' || COUNT(*) FROM RecurringTransactions WHERE Name IN ($nameList);"
)

if (-not $WhatIf) {
    # Outbox rows pointing at rows that are about to stop existing. Done before
    # the deletes would leave orphans either way.
    $statements += "DELETE FROM SyncOperations WHERE (EntityType = 'Transaction' AND EntityId NOT IN (SELECT Id FROM Transactions WHERE Notes IN ($noteList))) OR (EntityType = 'Account' AND EntityId NOT IN (SELECT Id FROM Accounts WHERE Name IN ($nameList))) OR (EntityType = 'Budget' AND EntityId NOT IN (SELECT Id FROM Budgets WHERE Name IN ($nameList))) OR (EntityType = 'FinancialGoal' AND EntityId NOT IN (SELECT Id FROM FinancialGoals WHERE Name IN ($nameList))) OR (EntityType = 'RecurringTransaction' AND EntityId NOT IN (SELECT Id FROM RecurringTransactions WHERE Name IN ($nameList)));"

    # Children before parents, so nothing is left pointing at a missing account.
    $statements += "DELETE FROM Transactions WHERE Notes IN ($noteList);"
    $statements += "DELETE FROM RecurringTransactions WHERE Name IN ($nameList);"
    $statements += "DELETE FROM Budgets WHERE Name IN ($nameList);"
    $statements += "DELETE FROM FinancialGoals WHERE Name IN ($nameList);"
    $statements += "DELETE FROM Accounts WHERE Name IN ($nameList);"
}

$sql = ($statements -join "`n") + "`n"
$local = Join-Path $env:TEMP "clear-demo.sql"
Set-Content -LiteralPath $local -Value $sql -Encoding utf8

Invoke-Adb @("push", $local, "/data/local/tmp/clear-demo.sql") | Out-Null
Invoke-Adb @("shell", "run-as $Package sh -c 'sqlite3 $db < /data/local/tmp/clear-demo.sql'") | ForEach-Object { Write-Host $_ }

if ($WhatIf) {
    Write-Host ""
    Write-Host "Preview only. Re-run without -WhatIf to delete the rows listed above."
} else {
    Write-Host ""
    Write-Host "Deleted. Re-run with -WhatIf to confirm the counts are zero."
}

exit 0