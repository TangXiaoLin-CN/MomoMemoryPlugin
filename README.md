# Momo Memory Plugin

VS Code / Cursor 窗口自动化插件，支持窗口操作、坐标记录和 OCR 文字识别。

## 功能

1. **窗口选择** - 选择目标窗口进行操作
2. **坐标拾取** - 记录窗口中的相对坐标并设置别名
3. **OCR 识别** - 对窗口指定区域进行截图和文字识别（支持中英文）
4. **状态栏显示** - 显示 OCR 结果和坐标快捷按钮
5. **鼠标点击** - 点击状态栏按钮触发对应位置的鼠标点击

## 安装

```bash
cd MomoMemoryPlugin
npm install
npm run compile
```

## 在 VS Code 中调试

1. 用 VS Code 打开此项目
2. 按 `F5` 启动调试
3. 在新打开的扩展开发宿主窗口中测试插件

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+Alt+W` | 选择目标窗口 |
| `Ctrl+Alt+P` | 进入坐标拾取模式 |
| `Ctrl+Alt+O` | 手动触发 OCR 识别 |

## 命令

在命令面板 (`Ctrl+Shift+P`) 中输入 `Momo` 可以看到所有可用命令：

- **Momo: Select Target Window** - 选择目标窗口
- **Momo: Pick Coordinate** - 拾取坐标
- **Momo: Capture OCR** - 手动 OCR 截图
- **Momo: Click Point** - 点击已保存的坐标
- **Momo: Start OCR Monitor** - 启动 OCR 自动监控
- **Momo: Stop OCR Monitor** - 停止 OCR 监控
- **Momo: Manage Coordinates** - 管理已保存的坐标
- **Momo: Open Settings** - 打开配置

## 配置项

在 VS Code 设置中搜索 `momo` 可以修改以下配置：

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `momo.targetWindow` | 目标窗口配置 | - |
| `momo.coordinates` | 已保存的坐标列表 | [] |
| `momo.ocrRegion` | OCR 识别区域（相对窗口） | {x:0, y:0, width:300, height:50} |
| `momo.ocrRefreshInterval` | OCR 自动刷新间隔（毫秒） | 5000 |
| `momo.ocrLanguages` | OCR 语言 | ["eng", "chi_sim"] |
| `momo.statusBarButtons` | 状态栏显示的坐标按钮 | [] |

## 使用流程

1. **选择窗口**: 按 `Ctrl+Alt+W` 打开窗口列表，选择要操作的目标窗口
2. **配置 OCR 区域**: 在设置中修改 `momo.ocrRegion` 指定要识别的区域
3. **OCR 识别**: 按 `Ctrl+Alt+O` 进行单次 OCR，或使用 `Start OCR Monitor` 自动刷新
4. **拾取坐标**: 按 `Ctrl+Alt+P` 进入拾取模式，点击目标窗口中的位置并设置别名
5. **快捷点击**: 将坐标添加到状态栏，点击状态栏按钮即可触发鼠标点击

## 技术实现

- **窗口操作**: PowerShell + Windows API
- **鼠标点击**: PowerShell + Windows API (robotjs 可选)
- **截图**: PowerShell + System.Drawing
- **OCR**: tesseract.js (本地离线)

## 系统要求

- Windows 操作系统
- VS Code 1.85.0 或更高版本
- Node.js 18+ (用于开发)

## 打包发布

```bash
npm install -g @vscode/vsce
vsce package
```

生成的 `.vsix` 文件可以直接安装到 VS Code/Cursor 中。
