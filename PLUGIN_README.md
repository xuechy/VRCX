# VRCX Local API & Webhook Plugin

这是一个为 VRCX 添加本地 API 查询和 Webhook 推送功能的插件系统。

## 快速开始

### 编译

使用 GitHub Actions 自动编译(推荐):
1. Fork 本仓库
2. 进入 Actions 标签页 → VRCX workflow → Run workflow  
3. 下载 Artifacts 中的安装包或 ZIP 文件

本地编译:
```bash
dotnet build Dotnet\VRCX-Cef.csproj -p:Configuration=Release -p:DefineConstants=ENABLE_LOCAL_API
```

### 功能说明

#### 1. 本地 API (HTTP 服务器)

**地址**: `http://127.0.0.1:15342`

**端点**:
- `GET /api/info` - 服务状态和版本
- `GET /api/users/{userId}` - 查询缓存的用户信息
- `GET /api/plugins` - 已加载的插件列表

**示例**:
```bash
curl http://localhost:15342/api/info
curl http://localhost:15342/api/users/usr_xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

#### 2. Webhook 推送

自动推送以下事件到指定 URL:
- 玩家加入/离开
- 位置变更
- 视频播放
- 传送门生成

**配置** (在浏览器控制台):
```javascript
const webhook = window.$app._context.provides[Symbol.for('pinia')].state.value.webhook;
webhook.enabled = true;
webhook.targetUrl = 'https://your-webhook-url.com/endpoint';
```

## 配置文件

插件配置自动创建在: `%AppData%\VRCX\config\plugins.json`

```json
{
  "plugins": {
    "LocalApiPlugin": {
      "enabled": true,
      "settings": {
        "Port": 15342,
        "BindAddress": "127.0.0.1"
      }
    }
  }
}
```

## 安全说明

- ✅ API 仅监听本地回环地址
- ✅ 不额外调用 VRChat API
- ⚠️ Webhook URL 需自行验证可信度

## 详细文档

查看 [walkthrough.md](C:/Users/admin/.gemini/antigravity/brain/40c7e1ae-7a42-4a0c-b0c3-42623393775c/walkthrough.md) 获取完整的实现细节和测试指南。

---

基于 [implementation_plan.md](file:///d:/AI/VRCX/implementation_plan.md) 实现
