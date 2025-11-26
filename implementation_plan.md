# VRCX 本地 API 与 Webhook 实现方案大纲

> **版本**: 1.1
> **目标**: 为 VRCX 添加本地 HTTP API 查询功能，以及基于事件的 Webhook 推送功能。同时配置 GitHub Actions 以支持在 Fork 仓库中进行云端编译。
> **核心原则**: 复用 VRCX 现有的内存数据（Vuex/Pinia），避免额外的 VRChat API 调用。

---

## 1. 架构设计 (Architecture)

为了实现“使用 VRCX 缓存数据”的目标，我们将采用 **C# 后端 + JavaScript 前端** 的混合架构：

### 1.1 本地 API 服务 (C# Backend)
*   **角色**: HTTP 服务器。
*   **职责**:
    *   监听本地端口（默认 `15342`）。
    *   接收外部程序的 HTTP GET 请求。
    *   **关键机制**: 通过 CEF (Chromium Embedded Framework) 的 `EvaluateScriptAsync` 方法，直接在主浏览器进程中执行 JavaScript，从而获取前端内存中的数据。
    *   **数据流**: `外部请求` -> `C# HttpListener` -> `CEF JS 执行` -> `读取 Pinia Store` -> `返回 JSON`。

### 1.2 Webhook 系统 (JavaScript Frontend)
*   **角色**: 事件监听与推送者。
*   **职责**:
    *   监听 VRCX 内部事件（如日志解析出的“玩家加入”、“位置变更”）。
    *   组装包含详细用户信息的 JSON 数据。
    *   **关键机制**: 利用 VRCX 现有的 C# `WebApi` 桥接功能发送 HTTP POST 请求。这样做的好处是可以利用 VRCX 的代理设置，并避免浏览器的 CORS（跨域）限制。
    *   **数据流**: `日志事件` -> `JS 逻辑处理` -> `调用 window.WebApi` -> `C# 发送 POST` -> `外部服务器`。

---

## 2. 模块划分与功能大纲

### 2.1 后端模块 (C#)

#### `LocalApiServer` (新模块)
*   **初始化**: 读取配置文件中的端口设置，启动 `HttpListener`。
*   **路由处理**:
    *   `/api/info`: 返回 VRCX 版本及服务状态。
    *   `/api/users/{userId}`: 查询指定用户的详细缓存信息。
    *   `/api/query`: (可选) 执行受限的 SQL 查询（针对本地 SQLite 日志数据库）。
*   **与前端通讯**: 封装 `GetUserFromFrontend(userId)` 方法，负责构造 JS 脚本并解析返回结果。

#### `Program.cs` (修改)
*   **生命周期管理**: 在程序启动时初始化 API 服务，在退出时关闭服务。

### 2.2 前端模块 (JavaScript/Vue)

#### `webhook.js` (新 Pinia Store)
*   **配置管理**: 保存 Webhook 开关、目标 URL 等设置（持久化到 localStorage）。
*   **发送逻辑**: 封装 `sendWebhook(event, data)` 方法，处理数据格式化和重试逻辑（如果需要）。

#### `gameLog.js` (修改)
*   **事件集成**:
    *   在解析到 `player-joined` (玩家加入) 事件时，触发 Webhook。
    *   在解析到 `location` (位置变更) 事件时，触发 Webhook。
*   **数据补全**: 在触发 Webhook 前，确保从 `userStore` 中获取到了该玩家的完整缓存数据（如头像、Trust Rank 等）。

---

## 3. 接口定义 (API Definition)

### 3.1 HTTP API (供外部调用)

*   **GET /api/info**
    *   **描述**: 检查服务是否存活。
    *   **返回**: `{ "version": "...", "status": "running" }`

*   **GET /api/users/{userId}**
    *   **描述**: 获取 VRCX 内存中缓存的玩家信息。
    *   **注意**: 仅返回 VRCX “见过”的玩家数据。
    *   **返回示例**:
        ```json
        {
          "id": "usr_xxx",
          "displayName": "昵称",
          "currentAvatarImageUrl": "...",
          "$trustLevel": "Veteran User"
        }
        ```

### 3.2 Webhook Payload (推送给外部)

*   **事件: player-joined**
    *   **触发时机**: 玩家加入当前实例，或自己加入实例时看到的玩家。
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

---

## 4. 开发步骤规划

1.  **第一阶段：后端基础**
    *   创建 `LocalApiServer.cs` 类。
    *   实现基础的 HTTP 监听和路由分发。
    *   实现 C# 调用前端 JS 获取简单数据的连通性测试。

2.  **第二阶段：前端 Webhook**
    *   创建 `webhook.js` Store。
    *   实现通过 `window.WebApi` 发送 POST 请求的功能。
    *   在 `gameLog.js` 中添加钩子，测试事件触发。

3.  **第三阶段：数据对接与完善**
    *   完善 `/api/users/{userId}` 的 JS 查询脚本，确保能读取到 Pinia 中的复杂对象。
    *   完善 Webhook 的数据组装，确保字段尽可能丰富（包含 VRCX 特有的 Trust Level 等）。
    *   添加配置界面（可选，或先通过配置文件/控制台配置）。

## 5. 安全与性能注意事项

*   **安全性**: API 服务仅绑定 `127.0.0.1`，禁止外部网络访问。
*   **性能**: API 查询依赖于主 UI 线程的响应，应避免高频轮询（建议间隔 > 1秒）。
*   **数据时效性**: 返回的数据是 VRCX 的“快照”，可能与 VRChat 服务器存在延迟，但对于本地覆盖层（Overlay）应用已足够。

---

## 6. GitHub Actions 构建配置 (Build Configuration)

为了在 Fork 的仓库中成功运行云端编译，需要修改 `.github/workflows/github_actions.yml` 文件，移除依赖私有密钥（Secrets）的步骤。

### 6.1 触发方式
*   **保留**: `workflow_dispatch` 事件。
    *   这允许在 GitHub "Actions" 页面手动点击 "Run workflow" 按钮来触发构建。

### 6.2 需要移除/注释的步骤
由于 Fork 仓库没有官方的签名证书和 Sentry Token，以下步骤会导致构建失败，必须移除：

1.  **移除代码签名 (Sign Dotnet executables)**
    *   定位: `jobs: build_dotnet_windows: steps` 下。
    *   操作: 删除 `name: Sign Dotnet executables` 及其后续的 `uses: azure/trusted-signing-action@v0` 块。

2.  **移除安装包签名 (Sign Cef setup)**
    *   定位: `jobs: build_dotnet_windows: steps` 下。
    *   操作: 删除 `name: Sign Cef setup` 及其后续的 `uses: azure/trusted-signing-action@v0` 块。

3.  **移除 Sentry 上报**
    *   定位: `jobs: build_node: steps` 下。
    *   操作: 删除所有 `SENTRY_AUTH_TOKEN: ${{ secrets.SentryAuthToken }}` 环境变量配置。
    *   涉及任务: `Build Cef-html` 和 `Build Electron-html`。

### 6.3 修改后的效果
*   **结果**: 构建出的 `.exe` 和 `.zip` 文件将是未签名的（Windows 可能会提示安全警告，但功能正常）。
*   **获取方式**: 构建完成后，在 GitHub Actions 运行详情页面的 "Artifacts" 区域下载。
