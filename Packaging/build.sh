#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-Canary}"
CANARY_PUBLISHER="${2:-CN=凌莞}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# ==========================================
# 1. 版本号
# ==========================================
echo -e "\033[36mCalculating version...\033[0m"
BUILD_VERSION="0.1.0.0"
BASE_VER="0.1.0"

if git_describe=$(git -C "$PROJECT_ROOT" describe --tags --long 2>/dev/null); then
    if [[ "$git_describe" =~ v?([0-9]+\.[0-9]+(\.[0-9]+)?)-([0-9]+)-g[0-9a-f]+ ]]; then
        BASE_VER="${BASH_REMATCH[1]}"
        COMMIT_COUNT="${BASH_REMATCH[3]}"
        IFS='.' read -ra parts <<< "$BASE_VER"
        if [[ ${#parts[@]} -eq 2 ]]; then
            BASE_VER_FULL="${BASE_VER}.0"
        else
            BASE_VER_FULL="$BASE_VER"
        fi

        if [[ "$MODE" == "Canary" ]]; then
            BUILD_VERSION="${BASE_VER_FULL}.${COMMIT_COUNT}"
        else
            BUILD_VERSION="${BASE_VER_FULL}.0"
        fi
    fi
else
    COMMIT_COUNT=$(git -C "$PROJECT_ROOT" rev-list --count HEAD 2>/dev/null || echo "0")
    BUILD_VERSION="0.1.0.${COMMIT_COUNT}"
    echo -e "\033[33mWarning: No git tag found, using fallback version $BUILD_VERSION\033[0m"
fi

echo -e "\033[32mTarget Version: $BUILD_VERSION\033[0m"

# ==========================================
# 2. 清理
# ==========================================
echo -e "\033[36mCleaning up...\033[0m"
PACK_DIR="$SCRIPT_DIR/Pack"
rm -rf "$PACK_DIR"
mkdir -p "$PACK_DIR"
rm -f "$SCRIPT_DIR"/*.appx "$SCRIPT_DIR"/*.msix

# ==========================================
# 3. 前端构建
# ==========================================
WWWROOT_PATH="$PROJECT_ROOT/ChuChartManager/wwwroot"
if [[ "$MODE" != "Canary" ]] || [[ ! -f "$WWWROOT_PATH/index.html" ]]; then
    echo -e "\033[36mBuilding Frontend...\033[0m"
    pushd "$PROJECT_ROOT" > /dev/null
    pnpm install
    pnpm --filter ccm-frontend build
    popd > /dev/null
else
    echo -e "\033[33mwwwroot already exists, skipping frontend build.\033[0m"
fi

# ==========================================
# 4. FreeMote 工具链
# ==========================================
echo -e "\033[36mBuilding FreeMote tools...\033[0m"
pushd "$PROJECT_ROOT" > /dev/null
dotnet build FreeMote/FreeMote.Tools.PsbDecompile -c Release
dotnet build FreeMote/FreeMote.Tools.PsBuild -c Release
dotnet build FreeMote/FreeMote.Tools.Viewer -c Release
popd > /dev/null

# ==========================================
# 5. 发布主程序 + CLI
# ==========================================
echo -e "\033[36mPublishing ChuChartManager + CLI...\033[0m"
pushd "$PROJECT_ROOT" > /dev/null

PUBLISH_ARGS=(
    -c Release -r win-x64 --self-contained
    -p:PublishSingleFile=false
    -p:PublishReadyToRun=true
    -p:ErrorOnDuplicatePublishOutputFiles=false
    -o "$PACK_DIR"
)

dotnet publish ChuChartManager/ChuChartManager.csproj "${PUBLISH_ARGS[@]}"
dotnet publish ChuChartManager.CLI/ChuChartManager.CLI.csproj "${PUBLISH_ARGS[@]}"
popd > /dev/null

# ==========================================
# 6. 复制 Manifest
# ==========================================
cp "$SCRIPT_DIR/AppxManifest.xml" "$PACK_DIR/AppxManifest.xml"

# ==========================================
# 7. 复制图标
# ==========================================
echo -e "\033[36mCopying Appx icons...\033[0m"
cp "$SCRIPT_DIR"/Base/*.png "$PACK_DIR/"

# ==========================================
# 8. 修改 Manifest 版本和 Canary 标识
# ==========================================
echo -e "\033[36mPatching Manifest...\033[0m"
MANIFEST_PATH="$PACK_DIR/AppxManifest.xml"

# 替换版本号
sed -i "s/Version=\"[^\"]*\"/Version=\"$BUILD_VERSION\"/" "$MANIFEST_PATH"

if [[ "$MODE" == "Canary" ]]; then
    sed -i "s/Name=\"MuNET.ChuChartManager\"/Name=\"MuNET.ChuChartManager.Canary\"/" "$MANIFEST_PATH"
    sed -i "s/Publisher=\"[^\"]*\"/Publisher=\"$CANARY_PUBLISHER\"/" "$MANIFEST_PATH"
    sed -i "s/<DisplayName>ChuChartManager</<DisplayName>ChuChartManager (Canary)</" "$MANIFEST_PATH"
    sed -i "s/<PublisherDisplayName>MuNET</<PublisherDisplayName>凌莞</" "$MANIFEST_PATH"
    sed -i 's/DisplayName="ChuChartManager"/DisplayName="ChuChartManager (Canary)"/' "$MANIFEST_PATH"
    sed -i 's/Alias="ccm.exe"/Alias="ccmc.exe"/' "$MANIFEST_PATH"
fi

# ==========================================
# 9. 生成 PRI 并打包
# ==========================================
echo -e "\033[36mGenerating PRI and Packing...\033[0m"
pushd "$PACK_DIR" > /dev/null

makepri.exe createconfig /cf priconfig.xml /dq zh-CN
makepri.exe new /pr . /cf ./priconfig.xml
rm -f priconfig.xml

if [[ "$MODE" == "Canary" ]]; then
    OUTPUT_NAME="ChuChartManager_Canary_${BUILD_VERSION}.appx"
else
    OUTPUT_NAME="ChuChartManager_${BUILD_VERSION}.appx"
fi
OUTPUT_APPX="$SCRIPT_DIR/$OUTPUT_NAME"

makeappx.exe pack /d . /p "$OUTPUT_APPX"
popd > /dev/null

# ==========================================
# 10. 签名 (仅 Canary)
# ==========================================
if [[ "$MODE" == "Canary" ]]; then
    echo -e "\033[36mSigning Appx...\033[0m"
    SIGN_CMD="D:/Sign/signcode.cmd"
    if [[ -f "$SIGN_CMD" ]]; then
        "$SIGN_CMD" "$OUTPUT_APPX"
        echo -e "\033[32mBuild & Sign Complete: $OUTPUT_APPX\033[0m"
    else
        echo -e "\033[33mWarning: Sign script not found at $SIGN_CMD. Skipping signing.\033[0m"
        echo -e "\033[33mBuild Complete (unsigned): $OUTPUT_APPX\033[0m"
    fi
else
    echo -e "\033[32mBuild Complete: $OUTPUT_APPX\033[0m"
fi
