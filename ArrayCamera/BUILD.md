# 项目编译与打包指南

## 📦 发布独立可执行文件

### Windows x64 单文件版（推荐）

```powershell
# 在项目根目录执行
dotnet publish ArrayCamera/ArrayCamera.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -o publish/win-x64-standalone
```

**输出位置**: `publish/win-x64-standalone/ArrayCamera.exe`

**特点**:

- ✅ 单个 `.exe` 文件
- ✅ 包含 .NET 运行时，无需安装任何依赖
- ✅ 解压即用，双击运行
- ⚠️ 文件大小 ~80MB（包含完整运行时）

---

### Windows x64 独立版（多文件）

```powershell
dotnet publish ArrayCamera/ArrayCamera.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o publish/win-x64
```

**输出位置**: `publish/win-x64/` 目录

**特点**:

- ✅ 文件较小（运行时分散在多个 DLL）
- ✅ 无需安装 .NET 运行时
- ⚠️ 需要分发整个文件夹

---

### 依赖框架版（最小化）

```powershell
dotnet publish ArrayCamera/ArrayCamera.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o publish/win-x64-fxdependent
```

**特点**:

- ✅ 体积最小（~500KB）
- ⚠️ 需要用户预装 .NET 6.0 Desktop Runtime
- 👉 [下载运行时](https://dotnet.microsoft.com/download/dotnet/6.0)

---

## 🛠️ 开发调试

### 直接运行（热重载）

```powershell
cd ArrayCamera
dotnet run
```

### 编译检查

```powershell
dotnet build ArrayCamera/ArrayCamera.csproj -c Release
```

---

## 📋 发布前检查清单

- [ ] 修改 `ArrayCamera.csproj` 中的版本号 `<Version>1.0.0</Version>`
- [ ] 添加应用图标 `<ApplicationIcon>icon.ico</ApplicationIcon>`
- [ ] 确认 HandyControl 主题正常加载
- [ ] 测试演示模式（双高斯光斑旋转）
- [ ] 验证所有按钮事件响应
- [ ] 检查颜色映射切换功能

---

## 🎯 快速打包命令（推荐）

```powershell
# 一键打包并压缩（真正的单文件版）
dotnet publish ArrayCamera/ArrayCamera.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish/ArrayCamera-v1.0-Standalone

Compress-Archive -Path "publish/ArrayCamera-v1.0-Standalone/ArrayCamera.exe" `
  -DestinationPath "ArrayCamera-v1.0-Standalone-win64.zip" -Force
```

**输出**: `ArrayCamera-v1.0-Standalone-win64.zip` （~60MB，包含完整运行时的单文件）

---

## 📌 注意事项

1. **首次运行**: Windows Defender 可能提示"未识别的应用"，点击"更多信息" → "仍要运行"
2. **防火墙**: 如需使用 UDP 功能，允许应用通过防火墙
3. **性能**: 单文件版首次启动略慢（需解压内嵌 DLL），后续启动正常
4. **分发**: 建议压缩后分发，减少下载时间

---

## 🔧 高级配置

### 启用 Trimming（减小体积）

⚠️ **警告**: 可能导致反射失效，需充分测试

```xml
<!-- ArrayCamera.csproj -->
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>link</TrimMode>
</PropertyGroup>
```

### 添加应用清单

```xml
<PropertyGroup>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>
```

---

## 📞 技术支持

- 项目文档: `README.md`
- UI/UX 设计: `docs/UI-UX-DESIGN.md`
- OpenSpec 规范: `openspec/project.md`
