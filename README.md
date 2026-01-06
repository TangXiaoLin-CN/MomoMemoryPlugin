# Momo Memory Plugin

VS Code / Cursor 窗口自动化插件，用于窗口操作、OCR 文字识别和自动点击。采用 VS Code 插件 + .NET 后端架构，支持高精度中文 OCR（PaddleOCR）和英文 OCR（Windows OCR），以及多种后台点击模式。

## 功能特点

- **窗口选择** - 选择目标窗口进行操作
- **OCR 识别** - 支持多个 OCR 区域，可自定义别名
  - **中文/混合内容**：使用 PaddleOCR，识别准确度高
  - **英文内容**：使用 Windows OCR，正确处理单词空格（如 "bank on" 而非 "bankon"）
- **自动点击** - 支持多个点击位置，可设置别名，支持多种点击模式（包括后台点击）
- **状态栏集成** - 在 VS Code 状态栏显示 OCR 结果和快捷点击按钮，布局可自定义
- **配置同步** - OCR 区域和点击坐标在后端配置，插件自动读取

## 项目结构

```
MomoMemoryPlugin/
├── src/                          # VS Code 插件源码 (TypeScript)
│   ├── extension.ts              # 插件入口
│   ├── statusBarManager.ts       # 状态栏管理
│   ├── backendClient.ts          # 后端 API 客户端
│   ├── backendManager.ts         # 后端进程管理
│   └── ...
├── MomoMemoryPlugin-backend/     # .NET 后端源码 (C#)
│   ├── Core/
│   │   ├── HttpApiService.cs     # HTTP API 服务
│   │   ├── PaddleOcrService.cs   # PaddleOCR 服务（中文）
│   │   ├── PaddleOcrPool.cs      # PaddleOCR 实例池（并行处理）
│   │   ├── OcrService.cs         # Windows OCR 服务（英文）
│   │   ├── MouseController.cs    # 鼠标控制
│   │   ├── WindowManager.cs      # 窗口管理
│   │   └── ...
│   ├── Views/
│   │   ├── MainConfigWindow.xaml # 主配置窗口 (WPF)
│   │   ├── TestWindow.xaml       # 测试窗口 (WPF)
│   │   └── Dialogs/              # 对话框
│   └── momo-config.json          # 配置文件
├── package.json                  # 插件配置
├── icon.png                      # 插件图标
└── README.md
```

## 安装使用

### 方式一：安装打包好的插件

1. 下载 `.vsix` 文件
2. 在 VS Code 中按 `Ctrl+Shift+P`，输入 `Install from VSIX`
3. 选择下载的 `.vsix` 文件安装
4. 重启 VS Code

### 方式二：从源码构建

#### 前置要求

- Node.js 18+
- .NET 8.0 SDK
- Windows 10/11

#### 构建步骤

```bash
# 1. 克隆仓库
git clone https://github.com/TangXiaoLin-CN/MomoMemoryPlugin.git
cd MomoMemoryPlugin

# 2. 一键构建（推荐）
build.bat

# 或手动构建：
# 安装插件依赖
npm install

# 编译插件
npm run compile

# 编译后端
dotnet publish MomoMemoryPlugin-backend/MomoBackend.csproj -c Release -r win-x64 --self-contained true -o backend

# 打包插件
npx @vscode/vsce package
```

## 使用流程

### 1. 配置后端

首次使用需要配置 OCR 区域和点击坐标：

**方式一：通过 VS Code 命令（推荐）**
1. 按 `Ctrl+Shift+P` 打开命令面板
2. 输入 `Momo: Open Backend Config Window`
3. 在弹出的配置窗口中：
   - **目标窗口**：选择要操作的窗口
   - **点击区域**：添加多个点击位置，设置别名和坐标
   - **OCR 区域**：添加多个 OCR 识别区域，设置别名、位置、大小和**语言**
4. 保存后使用 `Momo: Refresh Config` 刷新配置

> 注意：配置窗口为单实例模式，重复打开会激活已有窗口

**方式二：直接运行后端**
1. 直接运行 `MomoBackend.exe`（非 headless 模式）打开配置界面
2. 配置后保存

### 2. 使用插件

1. 打开 VS Code/Cursor
2. 插件会自动启动后端（headless 模式）
3. 按 `Ctrl+Alt+W` 选择目标窗口
4. 状态栏显示：`[窗口名] [按钮1] [按钮2] ... [OCR区域1: 内容] [OCR区域2: 内容] ... [刷新]`
5. 点击状态栏按钮执行对应操作

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+Alt+W` | 选择目标窗口 |
| `Ctrl+Alt+O` | 手动刷新 OCR |

## 命令

在命令面板 (`Ctrl+Shift+P`) 中输入 `Momo`：

| 命令 | 说明 |
|------|------|
| `Momo: Select Target Window` | 选择目标窗口 |
| `Momo: Capture OCR` | 手动触发 OCR 识别 |
| `Momo: Open Backend Config Window` | 打开后端配置窗口 |
| `Momo: Refresh Config from Backend` | 从后端重新加载配置 |
| `Momo: Show Backend Output` | 显示后端日志 |
| `Momo: Open Settings` | 打开插件设置 |

## 配置项

### 插件配置 (VS Code Settings)

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `momo.targetWindow` | 目标窗口信息 | - |
| `momo.useBackend` | 使用后端服务 | `true` |
| `momo.autoStartBackend` | 自动启动后端 | `true` |
| `momo.backendPort` | 后端 API 端口 | `5678` |
| `momo.statusBarLayout` | 状态栏布局顺序 | `window,buttons,ocr,refresh` |
| `momo.statusBarAlignment` | 状态栏对齐方式 | `left` |

### 状态栏布局自定义

通过 `momo.statusBarLayout` 可自定义状态栏项目顺序，用逗号分隔：

- `window` - 窗口选择按钮
- `buttons` - 点击按钮组
- `ocr` - OCR 结果显示
- `refresh` - 刷新按钮

示例：`ocr,buttons,window,refresh` 会将 OCR 显示在最左边

### 后端配置 (momo-config.json)

```json
{
  "version": 1,
  "targetWindowTitle": "",
  "targetProcessName": "",
  "clickPoints": [
    { "alias": "开始", "x": 100, "y": 200, "clickMode": "fast_background", "button": "left" },
    { "alias": "确认", "x": 300, "y": 400, "clickMode": "fast_background", "button": "left" }
  ],
  "ocrRegions": [
    { "alias": "状态", "x": 10, "y": 10, "width": 200, "height": 30, "language": "auto", "enabled": true },
    { "alias": "英文", "x": 10, "y": 50, "width": 100, "height": 25, "language": "en", "enabled": true }
  ],
  "ocrRefreshInterval": 3000,
  "ocrAutoRefresh": false,
  "ocrEngine": "paddle",
  "fastBackground": {
    "windowAlpha": 3,
    "delayAfterRestore": 30,
    "delayBeforeClick": 20,
    "delayAfterMove": 10,
    "delayAfterClick": 30,
    "delayBeforeRestore": 20,
    "minimizeAfterClick": true,
    "hideCursor": true
  }
}
```

## OCR 语言设置

每个 OCR 区域可以单独设置语言，系统会根据语言自动选择最合适的引擎：

| 语言设置 | 使用引擎 | 适用场景 |
|----------|----------|----------|
| `auto` (自动) | PaddleOCR | 中文或中英混合内容 |
| `zh` (中文) | PaddleOCR | 纯中文内容 |
| `en` (英文) | Windows OCR | 纯英文内容，正确处理单词空格 |

> 提示：如果英文识别结果缺少空格（如 "bankon" 而非 "bank on"），请将该区域的语言设置为 "英文"

## 点击模式

后端支持多种点击模式：

| 模式 | 说明 |
|------|------|
| `foreground` | 前台点击（移动鼠标） |
| `fast_background` | 快速后台点击（推荐，几乎无感知） |
| `background_post` | PostMessage 后台点击 |
| `background_send` | SendMessage 后台点击 |
| `hidden_cursor` | 隐藏光标点击 |

### fast_background 模式参数

`fast_background` 模式可在配置中调整参数：

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `windowAlpha` | 窗口透明度 (0-255) | 3 |
| `delayAfterRestore` | 窗口恢复后延迟 (ms) | 30 |
| `delayBeforeClick` | 点击前延迟 (ms) | 20 |
| `delayAfterMove` | 鼠标移动后延迟 (ms) | 10 |
| `delayAfterClick` | 点击后延迟 (ms) | 30 |
| `delayBeforeRestore` | 恢复前延迟 (ms) | 20 |
| `minimizeAfterClick` | 点击后最小化窗口 | true |
| `hideCursor` | 隐藏鼠标光标 | true |

## 技术实现

### 插件 (TypeScript)
- VS Code Extension API
- HTTP 客户端与后端通信
- 动态状态栏 UI 管理

### 后端 (C# .NET 8)
- **OCR**:
  - PaddleOCR (Sdcb.PaddleOCR) - 高精度中文识别
  - Windows OCR (Windows.Media.Ocr) - 英文识别，正确处理空格
- **窗口操作**: Windows API (user32.dll)
- **鼠标控制**: SendInput / PostMessage / SendMessage
- **HTTP API**: HttpListener
- **UI**: WPF (Windows Presentation Foundation)

## 系统要求

- Windows 10/11
- VS Code 1.85.0+ 或 Cursor
- 插件自带 .NET Runtime（self-contained 部署）

## 常见问题

### Q: 后端启动失败？
A: 检查 5678 端口是否被占用，或查看 `Momo: Show Backend Output` 日志。

### Q: OCR 识别不准确？
A: 确保 OCR 区域配置正确，可在配置界面使用"预览"功能检查区域是否正确。对于清晰的文字，PaddleOCR 识别效果最佳。

### Q: 英文单词之间没有空格？
A: 将该 OCR 区域的语言设置为 "英文"，系统会使用 Windows OCR 引擎，能正确处理英文单词空格。

### Q: 点击没有响应？
A: 尝试切换点击模式，某些应用可能需要特定模式才能响应。推荐使用 `fast_background` 模式。

### Q: 状态栏项目顺序不对？
A: 在 VS Code 设置中修改 `momo.statusBarLayout`，自定义显示顺序。

### Q: 配置窗口打开多个？
A: 配置窗口已实现单实例检测，重复打开会激活已有窗口而非创建新窗口。

## License

MIT
