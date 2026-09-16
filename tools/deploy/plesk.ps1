# Plesk / runasp.net yayını (deploy.yml "plesk" işi). pwsh ile windows-latest'te koşar.
#   -Action Configure : yayın çıktısındaki web.config'e ortam ve gizli ayarları environmentVariables olarak yazar
#   -Action Deploy    : Web Deploy (msdeploy, varsayılan) ya da FTP ile dosyaları siteye gönderir
# Ortam değişkenleri (workflow verir): ENVIRONMENT (staging|production), APP_SETTINGS_JSON ({"Ad__Alt": "değer", ...}),
# STAGING_USER/STAGING_PASS (staging), DEPLOY_METHOD (msdeploy|ftp), PLESK_SERVER, PLESK_SITE, PLESK_USER, PLESK_PASSWORD,
# FTP_SERVER, FTP_USER, FTP_PASSWORD, FTP_DIR.
param(
    [Parameter(Mandatory = $true)][ValidateSet('Configure', 'Deploy')][string]$Action,
    [Parameter(Mandatory = $true)][string]$Path
)

$ErrorActionPreference = 'Stop'
$publish = (Resolve-Path $Path).Path

function Require([string]$name) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if (-not $value) { throw "$name tanımsız (GitHub ortam değişkeni ya da gizlisi)." }
    return $value
}

if ($Action -eq 'Configure') {
    $environment = if ((Require 'ENVIRONMENT') -eq 'production') { 'Production' } else { 'Staging' }
    $settings = [ordered]@{ 'ASPNETCORE_ENVIRONMENT' = $environment }
    (Require 'APP_SETTINGS_JSON' | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $settings[$_.Name] = [string]$_.Value }
    if ($environment -eq 'Staging') {
        $settings['StagingGate__User'] = Require 'STAGING_USER'
        $settings['StagingGate__Password'] = Require 'STAGING_PASS'
    }

    # Gizliler yalnız sunucudaki web.config'te durur; IIS web.config'i hiçbir zaman sunmaz. Değer loga yazılmaz.
    $file = Join-Path $publish 'web.config'
    [xml]$config = Get-Content $file -Raw
    $aspNetCore = $config.SelectSingleNode('//aspNetCore')
    if (-not $aspNetCore) { throw 'web.config içinde aspNetCore öğesi yok.' }
    $variables = $aspNetCore.SelectSingleNode('environmentVariables')
    if (-not $variables) { $variables = $aspNetCore.AppendChild($config.CreateElement('environmentVariables')) }
    foreach ($name in $settings.Keys) {
        $existing = $variables.SelectSingleNode("environmentVariable[@name='$name']")
        if ($existing) { [void]$variables.RemoveChild($existing) }
        $element = $config.CreateElement('environmentVariable')
        $element.SetAttribute('name', $name)
        $element.SetAttribute('value', $settings[$name])
        [void]$variables.AppendChild($element)
        Write-Output "web.config: $name"
    }
    $config.Save($file)
    exit 0
}

$method = if ($env:DEPLOY_METHOD) { $env:DEPLOY_METHOD } else { 'msdeploy' }
if ($method -eq 'msdeploy') {
    $msdeploy = 'C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe'
    if (-not (Test-Path $msdeploy)) { throw 'msdeploy.exe bulunamadı (windows-latest runner).' }
    $site = Require 'PLESK_SITE'
    $server = Require 'PLESK_SERVER'
    # AppOffline: kopyalama sırasında kilitli dll'ler için site kısa süre bakım sayfasına alınır. Yüklenen görseller, gizli
    # belgeler ve loglar sunucuda kalır (skip); diğer eski dosyalar silinir.
    $arguments = @(
        '-verb:sync',
        "-source:contentPath=`"$publish`"",
        "-dest:contentPath=`"$site`",computerName=`"https://$($server):8172/msdeploy.axd?site=$site`",userName=`"$(Require 'PLESK_USER')`",password=`"$(Require 'PLESK_PASSWORD')`",authType=`"Basic`"",
        '-allowUntrusted',
        '-enableRule:AppOffline',
        '-skip:Directory=\\wwwroot\\uploads',
        '-skip:Directory=\\private',
        '-skip:Directory=\\logs',
        '-retryAttempts:3',
        '-retryInterval:3000'
    )
    $process = Start-Process -FilePath $msdeploy -ArgumentList $arguments -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "msdeploy çıkış kodu $($process.ExitCode)." }
    exit 0
}

if ($method -eq 'ftp') {
    $root = "ftp://$(Require 'FTP_SERVER')/$(if ($env:FTP_DIR) { $env:FTP_DIR.Trim('/') } else { 'wwwroot' })"
    $user = "$(Require 'FTP_USER'):$(Require 'FTP_PASSWORD')"
    $offline = Join-Path $env:RUNNER_TEMP 'app_offline.htm'
    Set-Content -Path $offline -Value '<!doctype html><title>Bakım</title><p>Kısa bir bakım yapıyoruz; birkaç dakika sonra yeniden deneyin.</p>' -Encoding utf8

    function Upload([string]$local, [string]$remote) {
        & curl.exe --silent --show-error --fail --ssl-reqd --ftp-create-dirs --retry 3 --user $user -T $local "$root/$remote"
        if ($LASTEXITCODE -ne 0) { throw "FTP yükleme hatası: $remote" }
    }

    # FTP silmez: yüklenen görseller ve gizli belgeler yerinde kalır; kaldırılan eski dosyalar sunucuda artık olarak durur.
    Upload $offline 'app_offline.htm'
    Get-ChildItem -Path $publish -Recurse -File | ForEach-Object {
        Upload $_.FullName ($_.FullName.Substring($publish.Length + 1).Replace('\', '/'))
    }
    & curl.exe --silent --show-error --fail --ssl-reqd --user $user $root/ -Q 'DELE app_offline.htm' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'app_offline.htm silinemedi.' }
    exit 0
}

throw "DEPLOY_METHOD geçersiz: $method (msdeploy | ftp)."
