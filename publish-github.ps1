# ============================================================
# DynamicIslandWin —— 一键发布 GitHub Releases（配合应用内“检查更新”）
# 用法（在仓库根目录的 PowerShell/VS Code 终端执行）：
#   1) 仅生成本地 dist\version.json（不上传，供自托管）：
#        $env:GH_REPO="你的用户名/DynamicIslandWin"; .\publish-github.ps1 -LocalOnly
#   2) 完整发布：建 Release + 上传 zip 与 version.json：
#        $env:GH_TOKEN="github_pat_…(PAT, 需 repo 权限)"
#        $env:GH_REPO="你的用户名/DynamicIslandWin"
#        .\publish-github.ps1        [-Note "本次更新说明"]
# 输出：
#   dist\DynamicIslandWin-win-x64.zip     发布用 zip（若不存在会自动先本地打包）
#   dist\version.json                      更新清单（同版 zip 的下载地址）
#   应用内“更新清单地址”填：
#       https://github.com/<你的用户名>/DynamicIslandWin/releases/latest/download/version.json
# 注：令牌只从 -Token 参数或 $env:GH_TOKEN 读取，绝不写盘/入库。
# ============================================================
param(
    [string]$Owner,
    [string]$Repo,
    [string]$Token,
    [string]$Note = "",
    [switch]$LocalOnly,
    [switch]$Prerelease
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $Owner) {
    if ($env:GH_REPO -and $env:GH_REPO -match '^([^/]+)/([^/]+)$') { $Owner = $Matches[1]; $Repo = $Matches[2] }
}
if (-not $Token) { $Token = $env:GH_TOKEN }

# 读取当前版本（csproj 单一来源）
$csproj = Join-Path $root 'DynamicIslandWin.csproj'
$verLine = Select-String -Path $csproj -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
if (-not $verLine -or -not $verLine.Matches[0].Groups[1].Value) { throw '无法从 csproj 读取 <Version>' }
$version = $verLine.Matches[0].Groups[1].Value.Trim()
$tag = "v$version"

$zipName = 'DynamicIslandWin-win-x64.zip'
$zipPath = Join-Path $root "dist\$zipName"
# 本地 zip 缺失则先按“framework-dependent”打一版（与现有自动打包一致）
if (-not (Test-Path $zipPath)) {
    Write-Host '==> 未找到 zip，先本地打包 ...' -ForegroundColor Cyan
    $outDir = Join-Path $root 'dist\DynamicIslandWin'
    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
    & dotnet publish (Join-Path $root 'DynamicIslandWin.csproj') -c Release -r win-x64 --self-contained false -p:DebugType=None -o $outDir
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败' }
    Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
}

# 生成 version.json：zip 下载地址用 GitHub Releases 的固定直链
$assetUrl = "https://github.com/$Owner/$Repo/releases/download/$tag/$zipName"
$manifest = @{
    version = $version
    note    = $Note
    url     = $assetUrl
}
$manifestPath = Join-Path $root 'dist\version.json'
[System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "==> 已生成 $manifestPath" -ForegroundColor Green
Write-Host "    清单地址(填进应用设置)： https://github.com/$Owner/$Repo/releases/latest/download/version.json"

if ($LocalOnly) {
    Write-Host 'LocalOnly：仅生成本地清单，未上传。' -ForegroundColor Yellow
    exit 0
}

if (-not $Owner -or -not $Repo -or -not $Token) {
    throw '缺少仓库或令牌：请传 -Owner/-Repo/-Token，或设置环境变量 $env:GH_REPO="用户/仓库" 与 $env:GH_TOKEN'
}

# GitHub REST（PowerShell 5.1 兼容）
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch { }
$headers = @{
    Authorization        = "token $Token"
    Accept               = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
$base = "https://api.github.com/repos/$Owner/$Repo"

# 1) 创建 Release（已存在则跳过创建、复用）
$releaseBody = @{ tag_name = $tag; name = "DynamicIslandWin v$version"; body = $Note; draft = $false; prerelease = $Prerelease } | ConvertTo-Json
try {
    $release = Invoke-RestMethod -Method Post -Uri "$base/releases" -Headers $headers -Body $releaseBody -ContentType 'application/json'
    Write-Host "==> 已创建 Release $tag" -ForegroundColor Green
}
catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 422) {
        $release = Invoke-RestMethod -Method Get -Uri "$base/releases/tags/$tag" -Headers $headers
        Write-Host "==> Release $tag 已存在，复用" -ForegroundColor Yellow
    } else { throw }
}

# 2) 上传资产
function Upload-Asset($release, $path, $name, $contentType) {
    $uploadBase = $release.upload_url -replace '\{.*\}', ''
    $uri = $uploadBase + '?name=' + [uri]::EscapeDataString($name)
    $resp = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType $contentType -InFile $path
    Write-Host ('    已上传 {0}' -f $name) -ForegroundColor Green
}
Upload-Asset $release $zipPath $zipName 'application/zip'
Upload-Asset $release $manifestPath 'version.json' 'application/json'

Write-Host ''
Write-Host '发布完成！' -ForegroundColor Green
Write-Host "  仓库   : $Owner/$Repo"
Write-Host "  Release: https://github.com/$Owner/$Repo/releases/tag/$tag"
Write-Host '  应用内「更新清单地址」填：' -ForegroundColor Yellow
Write-Host "    https://github.com/$Owner/$Repo/releases/latest/download/version.json" -ForegroundColor Yellow
