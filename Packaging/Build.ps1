param(
    [ValidateSet("Release", "Canary")]
    [string]$Mode = "Canary",

    # Canary 自签证书 Publisher（必须与签证书的 CN 一致）
    [string]$CanaryPublisher = "CN=凌莞"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Resolve-Path "$PSScriptRoot\.."

# ==========================================
# 1. 版本号
# ==========================================
Write-Host "Calculating version..." -ForegroundColor Cyan
$BuildVersion = "0.1.0.0"
$baseVer = "0.1.0"

try {
    Push-Location $ProjectRoot
    $gitDescribe = git describe --tags --long 2>$null
    Pop-Location

    if ($LASTEXITCODE -eq 0 -and $gitDescribe -match "v?(\d+\.\d+(?:\.\d+)?)-(\d+)-g[0-9a-f]+") {
        $baseVer = $Matches[1]
        $commitCount = $Matches[2]
        $baseVerFull = if ($baseVer.Split('.').Length -eq 2) { "$baseVer.0" } else { $baseVer }

        $BuildVersion = if ($Mode -eq "Canary") { "$baseVerFull.$commitCount" } else { "$baseVerFull.0" }
    } else {
        # 还没有 tag — 用 commit count 做 fallback，避免每次构建版本都相同
        Push-Location $ProjectRoot
        $commitCount = git rev-list --count HEAD 2>$null
        Pop-Location
        if ($LASTEXITCODE -eq 0 -and $commitCount) {
            $BuildVersion = "0.1.0.$commitCount"
        }
        Write-Warning "No git tag found, using fallback version $BuildVersion"
    }
} catch {
    Write-Warning "Git describe failed. Fallback to $BuildVersion"
}

Write-Host "Target Version: $BuildVersion" -ForegroundColor Green

# ==========================================
# 2. 清理
# ==========================================
Write-Host "Cleaning up..." -ForegroundColor Cyan
$PackDir = "$PSScriptRoot\Pack"
if (Test-Path $PackDir) { Remove-Item $PackDir -Recurse -Force }
New-Item -ItemType Directory -Path $PackDir -Force | Out-Null
Remove-Item "$PSScriptRoot\*.appx" -ErrorAction SilentlyContinue
Remove-Item "$PSScriptRoot\*.msix" -ErrorAction SilentlyContinue

# ==========================================
# 3. 前端构建（CI 中前端由 Linux job 预构建，本地构建始终重新构建）
# ==========================================
$WwwrootPath = "$ProjectRoot\ChuChartManager\wwwroot"
if ($Mode -ne "Canary" -or -not (Test-Path "$WwwrootPath\index.html")) {
    Write-Host "Building Frontend..." -ForegroundColor Cyan
    Push-Location $ProjectRoot
    try {
        pnpm install
        if ($LASTEXITCODE -ne 0) { throw "pnpm install failed ($LASTEXITCODE)" }
        pnpm --filter ccm-frontend build
        if ($LASTEXITCODE -ne 0) { throw "Frontend build failed ($LASTEXITCODE)" }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "wwwroot already exists, skipping frontend build." -ForegroundColor Yellow
}

# ==========================================
# 4. FreeMote 工具链（ChuChartManager 必需的 tools/）
# ==========================================
Write-Host "Building FreeMote tools..." -ForegroundColor Cyan
Push-Location $ProjectRoot
try {
    dotnet build FreeMote/FreeMote.Tools.PsbDecompile -c Release
    if ($LASTEXITCODE -ne 0) { throw "PsbDecompile build failed ($LASTEXITCODE)" }
    dotnet build FreeMote/FreeMote.Tools.PsBuild -c Release
    if ($LASTEXITCODE -ne 0) { throw "PsBuild build failed ($LASTEXITCODE)" }
    dotnet build FreeMote/FreeMote.Tools.Viewer -c Release
    if ($LASTEXITCODE -ne 0) { throw "Viewer build failed ($LASTEXITCODE)" }
} finally {
    Pop-Location
}

# ==========================================
# 5. 发布主程序 + CLI
# ==========================================
Write-Host "Publishing ChuChartManager + CLI..." -ForegroundColor Cyan
Push-Location $ProjectRoot
try {
    # ErrorOnDuplicatePublishOutputFiles=false：FreeMote 三个 tools 在 csproj 中 link 到同一个 tools\，
    # 共享同名依赖 dll，dotnet publish 默认会因 NETSDK1152 报错，关掉这个检查让后者覆盖前者
    $publishArgs = @(
        "-c", "Release", "-r", "win-x64", "--self-contained",
        "-p:PublishSingleFile=false",
        "-p:PublishReadyToRun=true",
        "-p:ErrorOnDuplicatePublishOutputFiles=false",
        "-o", $PackDir
    )

    # 主程序：self-contained win-x64
    dotnet publish ChuChartManager/ChuChartManager.csproj @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "publish ChuChartManager failed ($LASTEXITCODE)" }

    # CLI：合并发布到同目录
    dotnet publish ChuChartManager.CLI/ChuChartManager.CLI.csproj @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "publish ChuChartManager.CLI failed ($LASTEXITCODE)" }
} finally {
    Pop-Location
}

# ==========================================
# 6. 复制 Manifest
# ==========================================
Copy-Item "$PSScriptRoot\AppxManifest.xml" "$PackDir\AppxManifest.xml" -Force

# ==========================================
# 7. 复制图标（仓库中预生成，见 Packaging\GenerateIcons.ps1）
# ==========================================
Write-Host "Copying Appx icons..." -ForegroundColor Cyan
Copy-Item "$PSScriptRoot\Base\*.png" $PackDir -Force

# ==========================================
# 8. 修改 Manifest 版本和 Canary 标识
# ==========================================
Write-Host "Patching Manifest..." -ForegroundColor Cyan
$ManifestPath = "$PackDir\AppxManifest.xml"
[xml]$xml = Get-Content $ManifestPath

$ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace("x", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
$ns.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10")
$ns.AddNamespace("desktop", "http://schemas.microsoft.com/appx/manifest/desktop/windows10")
$ns.AddNamespace("uap3", "http://schemas.microsoft.com/appx/manifest/uap/windows10/3")

$xml.Package.Identity.Version = $BuildVersion

if ($Mode -eq "Canary") {
    # Canary 用自签证书 + 独立 Identity，避免覆盖未来的商店版
    $xml.Package.Identity.Name = "MuNET.ChuChartManager.Canary"
    $xml.Package.Identity.Publisher = $CanaryPublisher
    $xml.Package.Properties.DisplayName = "ChuChartManager (Canary)"
    $xml.Package.Properties.PublisherDisplayName = "凌莞"

    foreach ($app in $xml.Package.Applications.Application) {
        if ($app.VisualElements) {
            $app.VisualElements.DisplayName = $app.VisualElements.DisplayName + " (Canary)"
        }
        if ($app.Id -eq "CliTool") {
            $aliasNode = $app.SelectSingleNode(".//desktop:ExecutionAlias", $ns)
            if ($aliasNode) { $aliasNode.Alias = "ccmc.exe" }
        }
    }
}
$xml.Save($ManifestPath)

# ==========================================
# 9. 生成 PRI 并打包
# ==========================================
Write-Host "Generating PRI and Packing..." -ForegroundColor Cyan
Push-Location $PackDir
try {
    Remove-Item "priconfig.xml" -ErrorAction SilentlyContinue
    Remove-Item "*.pri" -ErrorAction SilentlyContinue

    makepri.exe createconfig /cf priconfig.xml /dq zh-CN
    if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed ($LASTEXITCODE)" }
    makepri.exe new /pr . /cf .\priconfig.xml
    if ($LASTEXITCODE -ne 0) { throw "makepri new failed ($LASTEXITCODE)" }
    Remove-Item "priconfig.xml"

    $OutputName = if ($Mode -eq "Canary") {
        "ChuChartManager_Canary_$BuildVersion.appx"
    } else {
        "ChuChartManager_$BuildVersion.appx"
    }
    $OutputAppx = "$PSScriptRoot\$OutputName"

    makeappx.exe pack /d . /p $OutputAppx
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed ($LASTEXITCODE)" }
} finally {
    Pop-Location
}

# ==========================================
# 10. 签名 (仅 Canary，使用 self-hosted runner 上的签名脚本)
# ==========================================
if ($Mode -eq "Canary") {
    Write-Host "Signing Appx..." -ForegroundColor Cyan

    $SignCmd = "D:\Sign\signcode.cmd"
    if (Test-Path $SignCmd) {
        & $SignCmd $OutputAppx
        if ($LASTEXITCODE -ne 0) { throw "signcode failed ($LASTEXITCODE)" }
        Write-Host "Build & Sign Complete: $OutputAppx" -ForegroundColor Green
    } else {
        Write-Warning "Sign script not found at $SignCmd. Skipping signing."
        Write-Host "Build Complete (unsigned): $OutputAppx" -ForegroundColor Yellow
    }
} else {
    Write-Host "Build Complete: $OutputAppx" -ForegroundColor Green
}
