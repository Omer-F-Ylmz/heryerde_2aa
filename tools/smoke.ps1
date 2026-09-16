# HerYerde smoke testi: verilen adrese karsi vitrinin ayakta ve dogru yapilandirilmis oldugunu dogrular.
#   pwsh tools/smoke.ps1 -BaseUrl https://alanadi.com
#   pwsh tools/smoke.ps1 -BaseUrl https://staging.alanadi.com -Staging -Credential "kullanici:parola"
# Windows PowerShell 5.1 ve pwsh 7 ile calisir. Her denetim "OK" ya da "FAIL" satiri yazar; bir FAIL varsa cikis kodu 1.
# -Credential: staging Basic Auth kapisi ("kullanici:parola"); -Staging: noindex basligi ve kapali robots denetlenir.
param(
    [Parameter(Mandatory = $true)][string]$BaseUrl,
    [string]$Credential = '',
    [switch]$Staging
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

$base = $BaseUrl.TrimEnd('/')
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(30)
$script:failures = 0

function Get-Page([string]$path) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, [Uri]($base + $path))
    $request.Headers.Accept.ParseAdd('text/html')
    if ($Credential) {
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new(
            'Basic', [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($Credential)))
    }
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    [pscustomobject]@{ Status = [int]$response.StatusCode; Body = $body; Response = $response }
}

function Get-Header($page, [string]$name) {
    $values = $null
    if ($page.Response.Headers.TryGetValues($name, [ref]$values)) { return ($values -join ', ') }
    if ($page.Response.Content.Headers.TryGetValues($name, [ref]$values)) { return ($values -join ', ') }
    return ''
}

function Check([string]$name, [bool]$passed, [string]$detail) {
    if ($passed) {
        [Console]::Out.WriteLine("OK    $name")
    } else {
        [Console]::Out.WriteLine("FAIL  $name - $detail")
        $script:failures++
    }
}

function Check-Ok([string]$path) {
    try {
        $page = Get-Page $path
        Check "GET $path 200" ($page.Status -eq 200) "durum $($page.Status)"
        return $page
    } catch {
        Check "GET $path 200" $false $_.Exception.Message
        return $null
    }
}

$homePage = Check-Ok '/'
if ($homePage) {
    foreach ($header in 'Content-Security-Policy', 'X-Content-Type-Options', 'X-Frame-Options', 'Referrer-Policy', 'Permissions-Policy') {
        Check "guvenlik basligi $header" ((Get-Header $homePage $header) -ne '') 'baslik yok'
    }
    if ($base.StartsWith('https://')) {
        Check 'HSTS' ((Get-Header $homePage 'Strict-Transport-Security') -match 'max-age=\d+') 'Strict-Transport-Security yok'
    }
    if ($Staging) {
        Check 'staging noindex' ((Get-Header $homePage 'X-Robots-Tag') -match 'noindex') 'X-Robots-Tag noindex yok'
    }
}

[void](Check-Ok '/ev')
[void](Check-Ok '/sepet')
[void](Check-Ok '/health/ready')

$sitemap = Check-Ok '/sitemap.xml'
$productPath = $null
if ($sitemap) {
    Check 'sitemap urlset' ($sitemap.Body -match '<urlset') 'urlset yok'
    $match = [regex]::Match($sitemap.Body, '<loc>[^<]*?(/urun/[^<]+)</loc>')
    Check 'sitemap urun iceriyor' $match.Success 'sitemapte /urun/ yok'
    if ($match.Success) { $productPath = $match.Groups[1].Value }
}
if ($productPath) { [void](Check-Ok $productPath) }

$robots = Check-Ok '/robots.txt'
if ($robots) {
    if ($Staging) {
        Check 'staging robots kapali' ($robots.Body -match '(?m)^Disallow: /\s*$') 'Disallow: / yok'
    } else {
        Check 'robots Sitemap satiri' ($robots.Body -match '(?m)^Sitemap: ') 'Sitemap satiri yok'
    }
}

try {
    $admin = Get-Page '/admin'
    $location = Get-Header $admin 'Location'
    if (-not $location -and $admin.Response.Headers.Location) { $location = $admin.Response.Headers.Location.ToString() }
    Check 'GET /admin 302 giris' (($admin.Status -eq 302) -and ($location -match '/admin/auth/login')) "durum $($admin.Status), Location '$location'"
} catch {
    Check 'GET /admin 302 giris' $false $_.Exception.Message
}

try {
    $missing = Get-Page '/olmayan-sayfa-smoke'
    Check 'GET /olmayan 404 markali' (($missing.Status -eq 404) -and ($missing.Body -match 'class="wordmark"')) "durum $($missing.Status)"
} catch {
    Check 'GET /olmayan 404 markali' $false $_.Exception.Message
}

if ($script:failures -gt 0) {
    Write-Output "SMOKE FAILED: $($script:failures) denetim"
    exit 1
}

Write-Output 'SMOKE OK'
exit 0
