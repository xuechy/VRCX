// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

#if ENABLE_LOCAL_API

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;

namespace VRCX.Plugins.LocalApiPlugin
{
    /// <summary>
    /// Handles HTTP API routes.
    /// </summary>
    public class ApiRoutes
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly CefBridge _cefBridge;
        private readonly PluginLoader _pluginLoader;

        public ApiRoutes(CefBridge cefBridge, PluginLoader pluginLoader)
        {
            _cefBridge = cefBridge ?? throw new ArgumentNullException(nameof(cefBridge));
            _pluginLoader = pluginLoader;
        }

        /// <summary>
        /// Routes an HTTP request to the appropriate handler.
        /// </summary>
        public async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var path = request.Url.AbsolutePath.ToLowerInvariant();
                logger.Debug("Handling request: {0} {1}", request.HttpMethod, path);

                // CORS headers for browser access (optional)
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                string jsonResponse;

                switch (path)
                {
                    case "/api/info":
                        jsonResponse = await HandleInfoAsync();
                        break;

                    case var p when p.StartsWith("/api/users/"):
                        var userId = path.Substring("/api/users/".Length);
                        jsonResponse = await HandleUserAsync(userId);
                        break;

                    case "/api/plugins":
                        jsonResponse = HandlePlugins();
                        break;

                    default:
                        jsonResponse = JsonSerializer.Serialize(new
                        {
                            error = "Not Found",
                            message = $"Unknown endpoint: {path}"
                        });
                        response.StatusCode = 404;
                        break;
                }

                SendJsonResponse(response, jsonResponse);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error handling request");
                response.StatusCode = 500;
                var errorJson = JsonSerializer.Serialize(new
                {
                    error = "Internal Server Error",
                    message = ex.Message
                });
                SendJsonResponse(response, errorJson);
            }
        }

        /// <summary>
        /// Handles /api/info endpoint.
        /// </summary>
        private async Task<string> HandleInfoAsync()
        {
            var info = await _cefBridge.GetVRCXInfoAsync();
            
            if (info == null)
            {
                return JsonSerializer.Serialize(new
                {
                    version = Program.Version,
                    status = "running",
                    apiEnabled = true,
                    timestamp = DateTime.UtcNow.ToString("O")
                });
            }

            // Parse and enhance the info
            try
            {
                var infoObj = JsonSerializer.Deserialize<JsonElement>(info);
                var enhancedInfo = new
                {
                    version = Program.Version,
                    status = "running",
                    apiEnabled = true,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    vrcxInfo = infoObj
                };
                return JsonSerializer.Serialize(enhancedInfo);
            }
            catch
            {
                return info;
            }
        }

        /// <summary>
        /// Handles /api/users/{userId} endpoint.
        /// </summary>
        private async Task<string> HandleUserAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Bad Request",
                    message = "User ID is required"
                });
            }

            var userData = await _cefBridge.GetUserDataAsync(userId);

            if (userData == null || userData == "null")
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Not Found",
                    message = $"User {userId} not found in cache"
                });
            }

            return userData;
        }

        /// <summary>
        /// Handles /api/plugins endpoint.
        /// </summary>
        private string HandlePlugins()
        {
            var plugins = new System.Collections.Generic.List<object>();

            if (_pluginLoader != null)
            {
                foreach (var plugin in _pluginLoader.LoadedPlugins)
                {
                    plugins.Add(new
                    {
                        name = plugin.Name,
                        version = plugin.Version,
                        status = "running"
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                plugins = plugins,
                count = plugins.Count
            });
        }

        /// <summary>
        /// Sends a JSON response.
        /// </summary>
        private void SendJsonResponse(HttpListenerResponse response, string json)
        {
            response.ContentType = "application/json; charset=utf-8";
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}

#endif
