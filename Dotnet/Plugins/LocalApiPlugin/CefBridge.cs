// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

#if ENABLE_LOCAL_API

using System;
using System.Threading.Tasks;
using CefSharp;
using NLog;

namespace VRCX.Plugins.LocalApiPlugin
{
    /// <summary>
    /// Bridge for executing JavaScript in CEF and retrieving data.
    /// </summary>
    public class CefBridge
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly IWebBrowser _browser;
        private const int DefaultTimeoutMs = 5000;

        public CefBridge(IWebBrowser browser)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        }

        /// <summary>
        /// Executes JavaScript and returns the result.
        /// </summary>
        /// <param name="script">The JavaScript code to execute.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <returns>The result as a string, or null if failed.</returns>
        public async Task<string> ExecuteScriptAsync(string script, int timeoutMs = DefaultTimeoutMs)
        {
            try
            {
                logger.Debug("Executing script: {0}", script.Length > 100 ? script.Substring(0, 100) + "..." : script);

                var task = _browser.EvaluateScriptAsync(script);
                var timeoutTask = Task.Delay(timeoutMs);

                var completedTask = await Task.WhenAny(task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    logger.Warn("Script execution timed out after {0}ms", timeoutMs);
                    return null;
                }

                var result = await task;

                if (!result.Success)
                {
                    logger.Error("Script execution failed: {0}", result.Message);
                    return null;
                }

                if (result.Result == null)
                {
                    return "null";
                }

                // Convert result to JSON string
                var resultJson = System.Text.Json.JsonSerializer.Serialize(result.Result);
                logger.Debug("Script result: {0}", resultJson.Length > 100 ? resultJson.Substring(0, 100) + "..." : resultJson);
                
                return resultJson;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error executing script");
                return null;
            }
        }

        /// <summary>
        /// Queries user data from Pinia store.
        /// </summary>
        /// <param name="userId">The VRChat user ID.</param>
        /// <returns>User data as JSON string, or null if not found.</returns>
        public async Task<string> GetUserDataAsync(string userId)
        {
            var script = $@"
(function() {{
    try {{
        // Access Pinia stores via Vue app instance
        const app = window.__VUE_APP_INSTANCE__;
        if (!app) return null;
        
        const pinia = app.config.globalProperties.$pinia;
        if (!pinia) return null;

        // Try to get user from API cache store
        const apiStore = pinia._s.get('api');
        if (!apiStore || !apiStore.cachedUsers) return null;

        const user = apiStore.cachedUsers['{userId}'];
        if (!user) return null;

        // Return relevant user data
        return {{
            id: user.id,
            displayName: user.displayName,
            currentAvatarImageUrl: user.currentAvatarImageUrl,
            currentAvatarThumbnailImageUrl: user.currentAvatarThumbnailImageUrl,
            bio: user.bio,
            tags: user.tags,
            statusDescription: user.statusDescription,
            status: user.status,
            last_platform: user.last_platform,
            $trustLevel: user.$trustLevel,
            location: user.location
        }};
    }} catch(e) {{
        console.error('Error getting user data:', e);
        return null;
    }}
}})();
";
            return await ExecuteScriptAsync(script);
        }

        /// <summary>
        /// Gets VRCX version and status info.
        /// </summary>
        public async Task<string> GetVRCXInfoAsync()
        {
            var script = @"
(function() {
    try {
        return {
            version: window.VRCX_VERSION || 'Unknown',
            apiEnabled: true,
            timestamp: new Date().toISOString()
        };
    } catch(e) {
        return { version: 'Unknown', apiEnabled: true, timestamp: new Date().toISOString() };
    }
})();
";
            return await ExecuteScriptAsync(script);
        }

        /// <summary>
        /// Checks if the browser is ready.
        /// </summary>
        public bool IsBrowserReady()
        {
            return _browser != null && !_browser.IsLoading;
        }
    }
}

#endif
