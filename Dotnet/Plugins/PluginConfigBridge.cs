// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

#if ENABLE_LOCAL_API

using System;
using System.IO;
using System.Text.Json;
using NLog;

namespace VRCX.Plugins
{
    /// <summary>
    /// Configuration model for plugins.json
    /// </summary>
    public class PluginConfiguration
    {
        public PluginMap Plugins { get; set; } = new();
        public WebhookConfig Webhook { get; set; } = new();
    }

    public class PluginMap
    {
        public LocalApiPluginConfig LocalApiPlugin { get; set; } = new();
    }

    public class LocalApiPluginConfig
    {
        public bool Enabled { get; set; } = true;
        public int Port { get; set; } = 15342;
        public string BindAddress { get; set; } = "127.0.0.1";
    }

    public class WebhookConfig
    {
        public bool Enabled { get; set; } = false;
        public string TargetUrl { get; set; } = "";
        public int RetryCount { get; set; } = 3;
        public int Timeout { get; set; } = 5000;
        public WebhookEvents Events { get; set; } = new();
    }

    public class WebhookEvents
    {
        public bool PlayerJoined { get; set; } = true;
        public bool PlayerLeft { get; set; } = true;
        public bool LocationChanged { get; set; } = true;
        public bool VideoPlay { get; set; } = false;
        public bool PortalSpawn { get; set; } = false;
    }

    /// <summary>
    /// Bridge for reading/writing plugin configuration from JavaScript
    /// </summary>
    public class PluginConfigBridge
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly string ConfigPath = Path.Join(Program.AppDataDirectory, "config", "plugins.json");
        private static readonly object _lock = new object();

        /// <summary>
        /// Reads the entire plugin configuration as JSON string
        /// </summary>
        public static string GetPluginConfig()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(ConfigPath))
                    {
                        logger.Info("Plugin config not found, creating default");
                        var defaultConfig = CreateDefaultConfig();
                        SaveConfig(defaultConfig);
                        return JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                    }

                    var json = File.ReadAllText(ConfigPath);
                    logger.Debug("Read plugin config: {0} bytes", json.Length);
                    return json;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to read plugin config");
                    return JsonSerializer.Serialize(CreateDefaultConfig());
                }
            }
        }

        /// <summary>
        /// Updates the plugin configuration
        /// </summary>
        /// <param name="configJson">JSON string of PluginConfiguration</param>
        public static bool SetPluginConfig(string configJson)
        {
            lock (_lock)
            {
                try
                {
                    // Validate JSON by deserializing
                    var config = JsonSerializer.Deserialize<PluginConfiguration>(configJson);
                    if (config == null)
                    {
                        logger.Error("Failed to deserialize plugin config");
                        return false;
                    }

                    SaveConfig(config);
                    logger.Info("Plugin configuration updated successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to set plugin config");
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets a specific config value by path (e.g., "webhook.enabled")
        /// </summary>
        public static string GetConfigValue(string path)
        {
            try
            {
                var config = LoadConfig();
                var parts = path.Split('.');
                
                // Simple path navigation
                if (parts.Length == 2)
                {
                    if (parts[0] == "localapi" && parts[1] == "enabled")
                        return config.Plugins.LocalApiPlugin.Enabled.ToString().ToLower();
                    if (parts[0] == "localapi" && parts[1] == "port")
                        return config.Plugins.LocalApiPlugin.Port.ToString();
                    if (parts[0] == "webhook" && parts[1] == "enabled")
                        return config.Webhook.Enabled.ToString().ToLower();
                    if (parts[0] == "webhook" && parts[1] == "targetUrl")
                        return config.Webhook.TargetUrl ?? "";
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to get config value for path: {0}", path);
                return "";
            }
        }

        private static PluginConfiguration CreateDefaultConfig()
        {
            return new PluginConfiguration
            {
                Plugins = new PluginMap
                {
                    LocalApiPlugin = new LocalApiPluginConfig
                    {
                        Enabled = true,
                        Port = 15342,
                        BindAddress = "127.0.0.1"
                    }
                },
                Webhook = new WebhookConfig
                {
                    Enabled = false,
                    TargetUrl = "",
                    RetryCount = 3,
                    Timeout = 5000,
                    Events = new WebhookEvents
                    {
                        PlayerJoined = true,
                        PlayerLeft = true,
                        LocationChanged = true,
                        VideoPlay = false,
                        PortalSpawn = false
                    }
                }
            };
        }

        private static PluginConfiguration LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                return CreateDefaultConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<PluginConfiguration>(json) ?? CreateDefaultConfig();
        }

        private static void SaveConfig(PluginConfiguration config)
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(ConfigPath, json);
            logger.Debug("Saved plugin config to {0}", ConfigPath);
        }
    }
}

#endif
