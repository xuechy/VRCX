# VRCX 本地 API 与 Webhook 实现方案大纲

> **版本**: 2.0 (模块化架构)
> **目标**: 为 VRCX 添加本地 HTTP API 查询功能，以及基于事件的 Webhook 推送功能。同时配置 GitHub Actions 以支持在 Fork 仓库中进行云端编译。
> **核心原则**: 
> - 复用 VRCX 现有的内存数据（Vuex/Pinia），避免额外的 VRChat API 调用
> - **模块化设计**: 所有功能以插件形式实现，最小化对源代码的修改，便于上游更新合并
> - **松耦合架构**: 通过事件总线和接口抽象，避免与 VRCX 核心代码深度耦合

---

## 1. 架构设计 (Architecture)

### 1.0 模块化设计原则

**插件式集成策略:**
*   所有新功能封装为独立模块，通过最小化接口与 VRCX 主程序交互
*   使用事件订阅模式而非直接修改现有代码
*   通过配置文件控制模块的启用/禁用
*   模块代码集中在独立目录，便于维护和移除

**最小侵入性修改:**
*   **C# 后端**: 仅在 `Program.cs` 添加插件加载器（约 10-15 行代码）
*   **JavaScript 前端**: 通过 Pinia 插件机制注入，无需修改现有 Store
*   **条件编译**: 使用 `#if ENABLE_LOCAL_API` 包裹所有集成点，可通过编译开关完全移除

**目录结构:**
```
VRCX/
├── Dotnet/
│   ├── Plugins/                    # 新增：插件目录
│   │   ├── IVRCXPlugin.cs         # 插件接口定义
│   │   ├── PluginLoader.cs        # 插件加载器
│   │   └── LocalApiPlugin/        # 本地 API 插件
│   │       ├── LocalApiServer.cs
│   │       ├── ApiRoutes.cs
│   │       └── CefBridge.cs
│   └── Program.cs                 # 最小修改：添加插件加载
├── src/
│   ├── plugins/                   # 新增：前端插件目录
│   │   ├── webhookPlugin.js       # Webhook 插件
│   │   └── eventBus.js            # 事件总线（如果不存在）
│   └── main.js                    # 最小修改：注册插件
└── config/
    └── plugins.json               # 插件配置文件
```

### 1.1 本地 API 服务 (C# Backend - 独立插件)

*   **角色**: HTTP 服务器插件
*   **位置**: `Dotnet/Plugins/LocalApiPlugin/` (独立目录)
*   **职责**:
    *   监听本地端口（默认 `15342`）
    *   接收外部程序的 HTTP GET 请求
    *   **关键机制**: 通过 CEF (Chromium Embedded Framework) 的 `EvaluateScriptAsync` 方法，直接在主浏览器进程中执行 JavaScript，从而获取前端内存中的数据
    *   **数据流**: `外部请求` -> `C# HttpListener` -> `CEF JS 执行` -> `读取 Pinia Store` -> `返回 JSON`
*   **集成方式**: 
    *   实现 `IVRCXPlugin` 接口（`Initialize()`, `Start()`, `Stop()`, `Dispose()`）
    *   通过 `PluginLoader` 动态加载，无需硬编码依赖
    *   接收 CEF Browser 实例作为依赖注入

**插件接口示例:**
```csharp
public interface IVRCXPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize(IVRCXContext context);
    void Start();
    void Stop();
    void Dispose();
}
```

### 1.2 Webhook 系统 (JavaScript Frontend - Pinia 插件)

*   **角色**: 事件监听与推送者
*   **位置**: `src/plugins/webhookPlugin.js` (独立插件文件)
*   **职责**:
    *   监听 VRCX 内部事件（如日志解析出的"玩家加入"、"位置变更"）
    *   组装包含详细用户信息的 JSON 数据
    *   **关键机制**: 利用 VRCX 现有的 C# `WebApi` 桥接功能发送 HTTP POST 请求。这样做的好处是可以利用 VRCX 的代理设置，并避免浏览器的 CORS（跨域）限制
    *   **数据流**: `日志事件` -> `事件总线` -> `Webhook 插件` -> `调用 window.WebApi` -> `C# 发送 POST` -> `外部服务器`
*   **集成方式**: 
    *   作为 Pinia 插件注册（`app.use(pinia); pinia.use(webhookPlugin);`）
    *   通过事件总线订阅 gameLog 事件，而非直接修改 gameLog.js
    *   配置存储在独立的 Pinia Store (`useWebhookStore`)

**事件订阅示例:**
```javascript
// 在 webhookPlugin.js 中
export function webhookPlugin({ store }) {
    if (store.$id === 'gameLog') {
        store.$onAction(({ name, args, after }) => {
            if (name === 'addLogEntry') {
                after(() => {
                    // 处理日志事件，触发 webhook
                });
            }
        });
    }
}
```

---

## 2. 模块划分与功能大纲

### 2.1 后端模块 (C#)

#### `IVRCXPlugin` 接口 (新增)
*   **文件**: `Dotnet/Plugins/IVRCXPlugin.cs`
*   **职责**: 定义插件标准接口
*   **方法**:
    *   `Initialize(IVRCXContext context)`: 接收 VRCX 上下文（CEF Browser, 配置等）
    *   `Start()`: 启动插件服务
    *   `Stop()`: 停止插件服务
    *   `Dispose()`: 清理资源

#### `PluginLoader` (新增)
*   **文件**: `Dotnet/Plugins/PluginLoader.cs`
*   **职责**: 
    *   从配置文件读取启用的插件列表
    *   动态加载插件程序集
    *   管理插件生命周期
*   **集成点**: 在 `Program.cs` 中调用 `PluginLoader.LoadPlugins()`

#### `LocalApiPlugin` (新模块 - 独立目录)
*   **目录**: `Dotnet/Plugins/LocalApiPlugin/`
*   **文件结构**:
    *   `LocalApiServer.cs`: 实现 `IVRCXPlugin`，管理 HttpListener
    *   `ApiRoutes.cs`: 路由处理逻辑
    *   `CefBridge.cs`: CEF JavaScript 执行封装
*   **路由处理**:
    *   `/api/info`: 返回 VRCX 版本及服务状态
    *   `/api/users/{userId}`: 查询指定用户的详细缓存信息
    *   `/api/query`: (可选) 执行受限的 SQL 查询（针对本地 SQLite 日志数据库）

#### `Program.cs` (最小修改)
*   **修改内容**:
```csharp
#if ENABLE_LOCAL_API
    // 在 CEF 初始化后添加
    var pluginLoader = new PluginLoader();
    pluginLoader.LoadPlugins(cefBrowser, config);
    
    // 在应用退出时添加
    pluginLoader.UnloadPlugins();
#endif
```
*   **修改行数**: 约 10-15 行
*   **优势**: 可通过编译开关完全移除，不影响主分支

### 2.2 前端模块 (JavaScript/Vue)

#### `eventBus.js` (新增或复用现有)
*   **文件**: `src/plugins/eventBus.js`
*   **职责**: 提供全局事件总线，用于模块间通信
*   **API**:
    *   `eventBus.on(event, handler)`: 订阅事件
    *   `eventBus.emit(event, data)`: 发布事件
    *   `eventBus.off(event, handler)`: 取消订阅

#### `webhookPlugin.js` (新 Pinia 插件)
*   **文件**: `src/plugins/webhookPlugin.js`
*   **职责**: 
    *   作为 Pinia 插件，拦截 Store 的 Action
    *   监听 gameLog Store 的日志添加事件
    *   触发 Webhook 发送
*   **配置管理**: 使用独立的 `useWebhookStore`
*   **发送逻辑**: 封装 `sendWebhook(event, data)` 方法

#### `useWebhookStore` (新 Pinia Store)
*   **文件**: `src/stores/webhook.js`
*   **职责**: 
    *   保存 Webhook 配置（enabled, targetUrl, retryCount）
    *   持久化到 localStorage
    *   提供配置管理 API
*   **状态**:
```javascript
{
    enabled: false,
    targetUrl: '',
    retryCount: 3,
    events: {
        playerJoined: true,
        locationChanged: true
    }
}
```

#### `main.js` (最小修改)
*   **修改内容**:
```javascript
// 在 Pinia 初始化后添加
import { webhookPlugin } from './plugins/webhookPlugin';
pinia.use(webhookPlugin);
```
*   **修改行数**: 2-3 行
*   **优势**: 插件可通过配置禁用，无需修改代码

#### `gameLog.js` (无需修改)
*   **原理**: Webhook 插件通过 Pinia 的 `$onAction` 钩子监听 gameLog Store 的 Action
*   **优势**: 完全解耦，gameLog.js 无需知道 Webhook 的存在

---

## 3. 接口定义 (API Definition)

### 3.1 HTTP API (供外部调用)

*   **GET /api/info**
    *   **描述**: 检查服务是否存活
    *   **返回**: `{ "version": "...", "status": "running", "plugins": ["LocalApiPlugin"] }`

*   **GET /api/users/{userId}**
    *   **描述**: 获取 VRCX 内存中缓存的玩家信息
    *   **注意**: 仅返回 VRCX "见过"的玩家数据
    *   **返回示例**:
        ```json
        {
          "id": "usr_xxx",
          "displayName": "昵称",
          "currentAvatarImageUrl": "...",
          "$trustLevel": "Veteran User"
        }
        ```

*   **GET /api/plugins**
    *   **描述**: 列出已加载的插件
    *   **返回**: `{ "plugins": [{ "name": "LocalApiPlugin", "version": "1.0", "status": "running" }] }`

### 3.2 Webhook Payload (推送给外部)

*   **事件: player-joined**
    *   **触发时机**: 玩家加入当前实例，或自己加入实例时看到的玩家
    *   **数据内容**:
        ```json
        {
          "event": "player-joined",
          "timestamp": "ISO8601时间",
          "data": {
            "id": "usr_xxx",
            "displayName": "...",
            "status": "active",
            "location": "wrld_xxx"
          }
        }
        ```

*   **事件: location-changed**
    *   **触发时机**: 用户切换世界或实例
    *   **数据内容**:
        ```json
        {
          "event": "location-changed",
          "timestamp": "ISO8601时间",
          "data": {
            "worldId": "wrld_xxx",
            "instanceId": "...",
            "worldName": "..."
          }
        }
        ```

### 3.3 插件配置接口

*   **配置文件**: `config/plugins.json`
*   **格式**:
```json
{
    "plugins": {
        "LocalApiPlugin": {
            "enabled": true,
            "port": 15342,
            "bindAddress": "127.0.0.1"
        }
    },
    "webhook": {
        "enabled": false,
        "targetUrl": "",
        "events": {
            "playerJoined": true,
            "locationChanged": true
        }
    }
}
```

---

## 4. 开发步骤规划

### 第一阶段：插件基础设施

1.  **创建插件接口和加载器**
    *   定义 `IVRCXPlugin` 接口
    *   实现 `PluginLoader` 类
    *   在 `Program.cs` 添加插件加载逻辑（条件编译）
    *   创建配置文件 `config/plugins.json`

2.  **创建前端事件总线**
    *   实现 `eventBus.js`（如果不存在）
    *   测试事件发布/订阅机制

### 第二阶段：后端 API 插件

3.  **实现 LocalApiPlugin 骨架**
    *   创建 `LocalApiPlugin` 目录和文件
    *   实现 `IVRCXPlugin` 接口
    *   实现基础的 HTTP 监听和路由分发

4.  **实现 CEF 桥接**
    *   封装 `CefBridge.cs`
    *   实现 C# 调用前端 JS 获取简单数据的连通性测试
    *   处理超时和错误

5.  **实现 API 路由**
    *   实现 `/api/info` 端点
    *   实现 `/api/users/{userId}` 端点
    *   实现 `/api/plugins` 端点

### 第三阶段：前端 Webhook 插件

6.  **创建 Webhook Store**
    *   实现 `useWebhookStore`
    *   实现配置持久化（localStorage）
    *   实现 `sendWebhook` 方法

7.  **实现 Webhook 插件**
    *   创建 `webhookPlugin.js`
    *   实现 Pinia Action 拦截
    *   监听 gameLog 事件
    *   触发 Webhook 发送

8.  **集成到主应用**
    *   在 `main.js` 注册 Webhook 插件
    *   测试事件触发和 Webhook 发送

### 第四阶段：数据对接与完善

9.  **完善数据查询**
    *   完善 `/api/users/{userId}` 的 JS 查询脚本
    *   确保能读取到 Pinia 中的复杂对象
    *   处理数据不存在的情况

10. **完善 Webhook 数据**
    *   完善 Webhook 的数据组装
    *   确保字段尽可能丰富（包含 VRCX 特有的 Trust Level 等）
    *   实现重试逻辑

11. **添加统一配置界面**
    *   在 VRCX 设置页面添加"插件"配置面板
    *   实现 Local API 配置（启用/禁用、端口、绑定地址）
    *   实现 Webhook 配置（启用/禁用、目标 URL、事件类型）
    *   添加配置验证和测试功能
    *   实现运行时配置更新（无需重启 VRCX）

### 第五阶段：测试与文档

12. **单元测试**
    *   测试插件加载器
    *   测试 API 端点
    *   测试 Webhook 发送

13. **集成测试**
    *   测试完整的 API 查询流程
    *   测试完整的 Webhook 推送流程
    *   测试插件启用/禁用

14. **文档编写**
    *   编写插件开发指南
    *   编写 API 使用文档
    *   编写 Webhook 配置文档

---

## 5. 安全与性能注意事项

*   **安全性**: 
    *   API 服务仅绑定 `127.0.0.1`，禁止外部网络访问
    *   插件加载器仅加载白名单中的插件
    *   CEF JavaScript 执行使用沙箱隔离
*   **性能**: 
    *   API 查询依赖于主 UI 线程的响应，应避免高频轮询（建议间隔 > 1秒）
    *   插件加载使用延迟初始化，不影响 VRCX 启动速度
    *   Webhook 发送使用异步队列，不阻塞主线程
*   **数据时效性**: 
    *   返回的数据是 VRCX 的"快照"，可能与 VRChat 服务器存在延迟
    *   对于本地覆盖层（Overlay）应用已足够

---

## 6. GitHub Actions 构建配置 (Build Configuration)

为了在 Fork 的仓库中成功运行云端编译，需要修改 `.github/workflows/github_actions.yml` 文件，移除依赖私有密钥（Secrets）的步骤。

### 6.1 触发方式
*   **保留**: `workflow_dispatch` 事件
    *   这允许在 GitHub "Actions" 页面手动点击 "Run workflow" 按钮来触发构建

### 6.2 需要移除/注释的步骤
由于 Fork 仓库没有官方的签名证书和 Sentry Token，以下步骤会导致构建失败，必须移除：

1.  **移除代码签名 (Sign Dotnet executables)**
    *   定位: `jobs: build_dotnet_windows: steps` 下
    *   操作: 删除 `name: Sign Dotnet executables` 及其后续的 `uses: azure/trusted-signing-action@v0` 块

2.  **移除安装包签名 (Sign Cef setup)**
    *   定位: `jobs: build_dotnet_windows: steps` 下
    *   操作: 删除 `name: Sign Cef setup` 及其后续的 `uses: azure/trusted-signing-action@v0` 块

3.  **移除 Sentry 上报**
    *   定位: `jobs: build_node: steps` 下
    *   操作: 删除所有 `SENTRY_AUTH_TOKEN: ${{ secrets.SentryAuthToken }}` 环境变量配置
    *   涉及任务: `Build Cef-html` 和 `Build Electron-html`

### 6.3 添加条件编译支持
*   在构建步骤中添加 MSBuild 参数: `/p:DefineConstants=ENABLE_LOCAL_API`
*   这样可以通过编译开关控制插件功能的包含

### 6.4 修改后的效果
*   **结果**: 构建出的 `.exe` 和 `.zip` 文件将是未签名的（Windows 可能会提示安全警告，但功能正常）
*   **获取方式**: 构建完成后，在 GitHub Actions 运行详情页面的 "Artifacts" 区域下载

---

## 7. 模块化优势总结

### 7.1 易于维护
*   **独立目录**: 所有新代码集中在 `Dotnet/Plugins/` 和 `src/plugins/`
*   **最小修改**: 主程序仅修改 10-15 行代码
*   **条件编译**: 可通过编译开关完全移除功能

### 7.2 易于更新
*   **松耦合**: 通过接口和事件总线通信，不依赖内部实现
*   **无冲突**: 不修改现有文件，合并上游更新时无冲突
*   **可移除**: 删除插件目录即可完全移除功能

### 7.3 易于扩展
*   **插件系统**: 可轻松添加新插件（如数据库查询、自动化脚本等）
*   **配置驱动**: 通过配置文件控制功能，无需重新编译
*   **事件驱动**: 新插件可订阅现有事件，无需修改现有代码

### 7.4 易于测试
*   **独立测试**: 插件可独立测试，不影响主程序
*   **模拟环境**: 可通过模拟 `IVRCXContext` 测试插件
*   **集成测试**: 可通过配置禁用插件，测试主程序稳定性
