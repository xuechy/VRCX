# Plugin API 实现计划

## 概述
本文档描述了如何完成 Local API 和 Webhook 功能的实现。

## 当前状态

### 已完成
1. ✅ 前端 UI 组件 (`src/views/Settings/components/PluginSettings.vue`)
2. ✅ 前端 Store (`src/stores/settings/plugins.js`)
3. ✅ Webhook Store (`src/stores/webhook.js`)
4. ✅ 设置页面集成（添加了"插件"标签页）
5. ✅ 本地化支持（英文和中文）
6. ✅ 版本号更新（从 2025.09.10 更新到 2025.11.16）

### 待实现
1. ❌ C# AppApi 方法实现
2. ❌ 插件配置文件管理
3. ❌ Local API HTTP 服务器
4. ❌ Webhook 发送逻辑集成

## 实现步骤

### 步骤 1: 添加 C# AppApi 方法

需要在 `Dotnet/AppApi/Common/AppApiCommonBase.cs` 中添加以下方法：

```csharp
// 获取插件配置
public virtual async Task<string> GetPluginConfig()
{
    var configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VRCX",
        "plugin-config.json"
    );
    
    if (!File.Exists(configPath))
    {
        // 返回默认配置
        var defaultConfig = new
        {
            plugins = new
            {
                localApiPlugin = new
                {
                    enabled = true,
                    port = 15342
                }
            },
            webhook = new
            {
                enabled = false,
                targetUrl = "",
                timeout = 5000,
                events = new
                {
                    playerJoined = true,
                    playerLeft = true,
                    locationChanged = true,
                    videoPlay = false,
                    portalSpawn = false
                }
            }
        };
        return JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
    }
    
    return await File.ReadAllTextAsync(configPath);
}

// 保存插件配置
public virtual async Task<bool> SetPluginConfig(string configJson)
{
    try
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VRCX",
            "plugin-config.json"
        );
        
        var directory = Path.GetDirectoryName(configPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        await File.WriteAllTextAsync(configPath, configJson);
        return true;
    }
    catch (Exception ex)
    {
        logger.Error($"Failed to save plugin config: {ex.Message}");
        return false;
    }
}

// 测试 Local API 连接
public virtual async Task<string> TestLocalApi(int port)
{
    try
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        
        var response = await client.GetAsync($"http://127.0.0.1:{port}/api/status");
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        else
        {
            return JsonConvert.SerializeObject(new
            {
                error = $"API 测试失败：由于目标计算机积极拒绝，无法连接。(127.0.0.1:{port})"
            });
        }
    }
    catch (Exception ex)
    {
        return JsonConvert.SerializeObject(new
        {
            error = $"API 测试失败：{ex.Message}"
        });
    }
}
```

### 步骤 2: 实现 Local API HTTP 服务器

创建新文件 `Dotnet/LocalApiServer.cs`：

```csharp
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Newtonsoft.Json;

namespace VRCX
{
    public class LocalApiServer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private HttpListener listener;
        private bool isRunning;
        private int port;

        public LocalApiServer(int port = 15342)
        {
            this.port = port;
        }

        public async Task Start()
        {
            if (isRunning) return;

            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
                isRunning = true;

                logger.Info($"Local API Server started on port {port}");

                while (isRunning)
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Local API Server error: {ex.Message}");
                isRunning = false;
            }
        }

        public void Stop()
        {
            if (!isRunning) return;

            isRunning = false;
            listener?.Stop();
            listener?.Close();
            logger.Info("Local API Server stopped");
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // CORS headers
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                string responseString;

                switch (request.Url.AbsolutePath)
                {
                    case "/api/status":
                        responseString = JsonConvert.SerializeObject(new
                        {
                            status = "ok",
                            version = Program.Version
                        });
                        break;

                    default:
                        response.StatusCode = 404;
                        responseString = JsonConvert.SerializeObject(new
                        {
                            error = "Not found"
                        });
                        break;
                }

                var buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling request: {ex.Message}");
            }
        }
    }
}
```

### 步骤 3: 在 Program.cs 中集成 Local API Server

在 `Dotnet/Program.cs` 中添加：

```csharp
private static LocalApiServer localApiServer;

// 在初始化时启动服务器
public static async Task StartLocalApiServer(int port)
{
    if (localApiServer != null)
    {
        localApiServer.Stop();
    }

    localApiServer = new LocalApiServer(port);
    await Task.Run(() => localApiServer.Start());
}

public static void StopLocalApiServer()
{
    localApiServer?.Stop();
}
```

### 步骤 4: 更新前端组件

一旦后端实现完成，更新 `src/views/Settings/components/PluginSettings.vue`：

1. 移除 `disabled` 属性
2. 恢复使用 `usePluginSettingsStore`
3. 移除"功能开发中"的提示

## 测试计划

1. **配置保存测试**
   - 修改设置并保存
   - 重启应用，验证设置是否保持

2. **Local API 测试**
   - 启用 Local API
   - 点击"Test API Connection"按钮
   - 验证是否返回成功消息

3. **Webhook 测试**
   - 配置 webhook URL（可以使用 webhook.site 进行测试）
   - 启用 webhook
   - 点击"Test Webhook"按钮
   - 验证是否收到测试消息

4. **事件触发测试**
   - 在 VRChat 中触发各种事件
   - 验证 webhook 是否正确发送

## 注意事项

1. Local API 服务器应该只监听 127.0.0.1，不要暴露到外网
2. Webhook URL 应该支持 HTTPS
3. 需要添加适当的错误处理和日志记录
4. 考虑添加速率限制以防止 webhook 滥用
