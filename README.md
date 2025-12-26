# Momo Memory Plugin

VS Code / Cursor 窗口自动化插件，用于窗口操作、OCR 文字识别和自动点击。采用 VS Code 插件 + .NET 后端架构，支持高精度中文 OCR（PaddleOCR）和多种后台点击模式。

## 功能特点

- **窗口选择** - 选择目标窗口进行操作
- **OCR 识别** - 支持多个 OCR 区域，可自定义别名，使用 PaddleOCR 进行高精度中英文识别
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
│   │   ├── PaddleOcrService.cs   # PaddleOCR 服务
│   │   ├── MouseController.cs    # 鼠标控制
│   │   ├── WindowManager.cs      # 窗口管理
│   │   └── ...
│   ├── ConfigForm.cs             # 配置界面
│   ├── TestForm.cs               # 测试界面
│   └── momo-config.json          # 配置文件
├── package.json                  # 插件配置
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
git clone https://github.com/your-repo/MomoMemoryPlugin.git
cd MomoMemoryPlugin

# 2. 安装插件依赖
npm install

# 3. 编译插件
npm run compile

# 4. 编译后端
cd MomoMemoryPlugin-backend
dotnet publish -c Release -o ../backend

# 5. 打包插件
cd ..
npx @vscode/vsce package
```

## 使用流程

### 1. 配置后端

首次使用需要配置 OCR 区域和点击坐标：

**方式一：通过 VS Code 命令**
1. 按 `Ctrl+Shift+P` 打开命令面板
2. 输入 `Momo: Open Backend Config Window`
3. 在弹出的配置窗口中：
   - **点击区域**：添加多个点击位置，设置别名和坐标
   - **OCR 区域**：添加多个 OCR 识别区域，设置别名、位置和大小
4. 保存后使用 `Momo: Refresh Config` 刷新配置

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
    { "alias": "数值", "x": 10, "y": 50, "width": 100, "height": 25, "language": "auto", "enabled": true }
  ],
  "ocrRefreshInterval": 3000,
  "ocrAutoRefresh": false,
  "ocrEngine": "paddle"
}
```

## 点击模式

后端支持多种点击模式：

| 模式 | 说明 |
|------|------|
| `foreground` | 前台点击（移动鼠标） |
| `fast_background` | 快速后台点击（推荐，几乎无感知） |
| `background_post` | PostMessage 后台点击 |
| `background_send` | SendMessage 后台点击 |

## 技术实现

### 插件 (TypeScript)
- VS Code Extension API
- HTTP 客户端与后端通信
- 动态状态栏 UI 管理

### 后端 (C# .NET 8)
- **OCR**: PaddleOCR Sharp - 高精度中英文识别
- **窗口操作**: Windows API (user32.dll)
- **鼠标控制**: SendInput / PostMessage / SendMessage
- **HTTP API**: HttpListener

## 系统要求

- Windows 10/11
- VS Code 1.85.0+ 或 Cursor
- .NET 8.0 Runtime（后端需要）

## 常见问题

### Q: 后端启动失败？
A: 检查 5678 端口是否被占用，或查看 `Momo: Show Backend Output` 日志。

### Q: OCR 识别不准确？
A: 确保 OCR 区域配置正确，PaddleOCR 对清晰文字识别效果最佳。可在配置界面使用"预览"功能检查区域是否正确。

### Q: 点击没有响应？
A: 尝试切换点击模式，某些应用可能需要特定模式才能响应。推荐使用 `fast_background` 模式。

### Q: 状态栏项目顺序不对？
A: 在 VS Code 设置中修改 `momo.statusBarLayout`，自定义显示顺序。

## License

MIT
