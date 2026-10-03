param([string]$ProjectRoot = ".")

# Every StaticResource key referenced by a page must resolve from somewhere the
# runtime can see: the page's own resources, App.xaml, or a merged dictionary
# (Colors.xaml / Styles.xaml). Debug builds use XamlCompilation=Skip, so a missing
# key is a runtime XamlParseException rather than a build error - this catches it.

$ErrorActionPreference = "Stop"
$root = (Resolve-Path $ProjectRoot).Path
$src = Join-Path $root "src\FinanceApp.Mobile"

function Get-Keys([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return @() }
    $text = Get-Content -LiteralPath $path -Raw
    return [regex]::Matches($text, 'x:Key="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
}

$appKeys = @(Get-Keys (Join-Path $src "App.xaml"))
foreach ($merged in @("Resources\Styles\Colors.xaml", "Resources\Styles\Styles.xaml")) {
    $appKeys += @(Get-Keys (Join-Path $src $merged))
}
$appKeys = @($appKeys | Sort-Object -Unique)

$problems = @()

Get-ChildItem -Recurse -Path $src -Filter *.xaml |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' -and $_.Name -ne 'App.xaml' -and $_.DirectoryName -notmatch 'Resources\\Styles' } |
    ForEach-Object {
        $page = $_
        $local = @(Get-Keys $page.FullName)

        # Dictionary entries declared inside this page count as local too.
        $text = Get-Content -LiteralPath $page.FullName -Raw
        [regex]::Matches($text, 'x:Key="([^"]+)"') | ForEach-Object { $local += $_.Groups[1].Value }

        $used = [regex]::Matches($text, 'StaticResource\s+([A-Za-z_][A-Za-z0-9_]*)') |
            ForEach-Object { $_.Groups[1].Value } |
            Sort-Object -Unique

        foreach ($key in $used) {
            if ($local -notcontains $key -and $appKeys -notcontains $key) {
                $problems += [pscustomobject]@{ Page = $page.Name; Key = $key }
            }
        }
    }

if ($problems.Count -eq 0) {
    Write-Host "OK: every StaticResource key in every page resolves (app-level keys: $($appKeys.Count))"
    exit 0
}

Write-Host "MISSING StaticResource keys:"
$problems | Sort-Object Page, Key -Unique | ForEach-Object { Write-Host ("  {0} -> {1}" -f $_.Page, $_.Key) }
exit 1