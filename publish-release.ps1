# ============================================================
# DynamicIslandWin 一键发布脚本（VS Code 集成终端运行）
# 用法：
#   1) 普通“整文件夹自包含”发布（推荐，对方无需安装 .NET）：
#        .\publish-release.ps1
#   2) 尝试“单个 exe”发布（更小更整洁；WPF 需联网下载运行时包）：
#        .\publish-release.ps1 -SingleFile
#   输出：
#       dist\DynamicIslandWin\   -> 可分发目录（整个文件夹拷给朋友）
#       dist\DynamicIslandWin-win-x64.zip -> 打包好的压缩包
# 要求：本机已安装 .NET 8 SDK，且能访问 https://api.nuget.org
# ============================================================
param(
    [switch]$SingleFile
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csproj = Join-Path $root 'DynamicIslandWin.csproj'
$outDir = Join-Path $root 'dist\DynamicIslandWin'
$zip = Join-Path $root "dist\DynamicIslandWin-win-x64.zip"

if (-not (Test-Path $csproj)) { throw "找不到项目文件: $csproj" }

Write-Host '==> 开始 Release 自包含发布（此过程需要下载 .NET 运行时包，请保持联网）' -ForegroundColor Cyan
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

$args = @(
    $csproj,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',      # 自带 .NET 运行时：对方电脑无需安装任何环境
    '-p:DebugType=None',
    '-o', $outDir
)

if ($SingleFile) {
    # 单 exe 模式：.NET 会把程序集打进 exe；WPF 的原生库首次运行会自解压（稍慢一点）
    # 若此处报错（个别环境不支持），去掉 -SingleFile 用整文件夹方案即可
    $args += @(
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true'
    )
    Write-Host '==> 单文件模式已开启' -ForegroundColor Yellow
}

dotnet publish @args
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败，请检查上方错误（多为网络问题）' }

Write-Host '==> 正在打包 zip ...' -ForegroundColor Cyan
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip -CompressionLevel Optimal

Write-Host ''
Write-Host '发布完成！' -ForegroundColor Green
Write-Host "  分发目录 : $outDir"
Write-Host "  压缩包   : $zip"
Write-Host ''
Write-Host '给朋友的方式：把 zip 解压到任意可写目录（如桌面），双击 DynamicIslandWin.exe 即可。' -ForegroundColor Yellow
Write-Host '注意：plugins 文件夹必须与 exe 放在一起（压缩包已包含）。' -ForegroundColor Yellow
