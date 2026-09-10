#!/usr/bin/env bash
# 打包 Thunderstore 发布包：Author-PackageName-Version.zip
# 用法：./build-package.sh [TeamName]
#   TeamName 省略时从环境变量 THUNDERSTORE_TEAM 读取，默认 YourName。
set -euo pipefail

cd "$(dirname "$0")"

TEAM="${1:-${THUNDERSTORE_TEAM:-YourName}}"

# 从 manifest.json 读取包名与版本，保证与元数据一致
PKG_NAME=$(node -e "console.log(require('./manifest.json').name)")
VERSION=$(node -e "console.log(require('./manifest.json').version_number)")
OUT="${TEAM}-${PKG_NAME}-${VERSION}.zip"

echo ">> 构建 Release..."
dotnet build -c Release -v quiet

DLL="bin/Release/netstandard2.1/${PKG_NAME}.dll"
[ -f "$DLL" ] || { echo "找不到构建产物：$DLL" >&2; exit 1; }

STAGE="dist/pkg"
rm -rf "$STAGE"
mkdir -p "$STAGE/plugins"

# 必需文件必须在 zip 根目录
cp manifest.json "$STAGE/manifest.json"
cp README.md     "$STAGE/README.md"
cp icon.png      "$STAGE/icon.png"
[ -f CHANGELOG.md ] && cp CHANGELOG.md "$STAGE/CHANGELOG.md"
[ -f LICENSE ]     && cp LICENSE     "$STAGE/LICENSE"

# BepInEx 插件 DLL 走 plugins/ 路由，mod 管理器会映射到 BepInEx/plugins/
cp "$DLL" "$STAGE/plugins/${PKG_NAME}.dll"

rm -f "dist/${OUT}"
# 用自带 node 打包器，保证 zip 内路径为正斜杠（Thunderstore 路由要求）
node tools/zip.js "$STAGE" "dist/${OUT}"

echo ">> 已生成 dist/${OUT}"

