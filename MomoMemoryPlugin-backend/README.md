# MomoBackend - 后台点击服务

Windows 后台点击 HTTP API 服务，供 VS Code/Cursor 插件调用。

## 快速开始

### 编译运行

```bash
cd D:\project\MomoMemoryPlugin-backend
dotnet run
```

默认监听端口：`23333`

指定端口：
```bash
dotnet run 12345
```

### 发布

```bash
dotnet publish -c Release -o ./publish
```

## API 接口

### 服务状态

```
GET /api/health
```

### 窗口管理

```
GET  /api/windows              # 获取所有窗口列表
GET  /api/windows/{hwnd}       # 获取窗口信息
GET  /api/windows/{hwnd}/valid # 检查窗口是否有效
GET  /api/windows/{hwnd}/rect  # 获取窗口位置大小
```

### 鼠标操作

```
POST /api/click                # 执行点击
GET  /api/mouse/position       # 获取鼠标位置
GET  /api/mouse/position/{hwnd}# 获取鼠标相对于窗口的位置
```

#### 点击请求参数

```json
{
    "hwnd": 12345678,
    "x": 100,
    "y": 200,
    "mode": "stealth",
    "button": "left"
}
```

**mode 可选值：**
- `stealth` - 隐身点击：透明化 + 激活 + 点击 + 恢复（推荐）
- `quick_switch` - 快速切换：激活 + 点击 + 切回
- `transparent` - 透明点击：透明化后用 PostMessage
- `post_message` - 纯 PostMessage（很多程序不响应）
- `send_message` - 纯 SendMessage（很多程序不响应）
- `foreground` - 前台点击（普通点击）

**button 可选值：**
- `left` - 左键
- `right` - 右键

### 坐标管理

```
GET    /api/coordinates        # 获取所有坐标
POST   /api/coordinates        # 添加坐标
DELETE /api/coordinates/{alias}# 删除坐标
```

## VS Code 插件调用示例

```typescript
// 获取窗口列表
const response = await fetch('http://localhost:23333/api/windows');
const windows = await response.json();

// 执行点击
const clickResponse = await fetch('http://localhost:23333/api/click', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        hwnd: 12345678,
        x: 100,
        y: 200,
        mode: 'stealth',
        button: 'left'
    })
});
const result = await clickResponse.json();
```

## 点击模式说明

| 模式 | 原理 | 适用场景 |
|------|------|----------|
| stealth | 透明化窗口→激活→点击→恢复 | 需要激活才能响应的程序 |
| quick_switch | 激活→点击→切回 | 同上，但会短暂闪烁 |
| transparent | 透明化后用 PostMessage | 支持后台消息的程序 |
| post_message | 纯 PostMessage | 标准 Win32 控件 |
| send_message | 纯 SendMessage | 标准 Win32 控件 |
| foreground | 普通前台点击 | 调试用 |
