// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

#if ENABLE_LOCAL_API

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CefSharp;
using NLog;

namespace VRCX.Plugins
{
    /// <summary>
    /// Configuration for the plugin system.
    /// </summary>
    public class PluginConfig
    {
        public Dictionary<string, PluginSettings> Plugins { get; set; } = new();
    }

    public class PluginSettings
    {
        public bool Enabled { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    /// <summary>
    /// Loads and manages VRCX plugins.
    /// </summary>
    public class PluginLoader
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly List<IVRCXPlugin> _loadedPlugins = new();
        private IVRCXContext _context;
        private PluginConfig _config;

        /// <summary>
        /// Loads all enabled plugins from configuration.
        /// </summary>
        /// <param name="browser">The main CEF browser instance.</param>
        public void LoadPlugins(IWebBrowser browser)
        {
            try
            {
                logger.Info("Initializing plugin system...");
                
                _context = new VRCXContext(browser);
                _config = LoadPluginConfig();

                // Manually instantiate plugins (compile-time loading)
                // This avoids reflection issues and keeps things simple
                RegisterPlugin(new LocalApiPlugin.LocalApiServer());

                logger.Info("Plugin system initialized with {0} plugin(s)", _loadedPlugins.Count);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to initialize plugin system");
            }
        }

        /// <summary>
        /// Registers a plugin and initializes/starts it if enabled.
        /// </summary>
        private void RegisterPlugin(IVRCXPlugin plugin)
        {
            try
            {
                var pluginName = plugin.Name;
                logger.Info("Registering plugin: {0} v{1}", pluginName, plugin.Version);

                // Check if plugin is enabled in config
                if (_config?.Plugins?.TryGetValue(pluginName, out var settings) == true)
                {
                    if (!settings.Enabled)
                    {
                        logger.Info("Plugin {0} is disabled in configuration", pluginName);
                        return;
                    }
                }
                else
                {
                    logger.Info("Plugin {0} not found in configuration, defaulting to enabled", pluginName);
                }

                plugin.Initialize(_context);
                plugin.Start();
                _loadedPlugins.Add(plugin);
                
                logger.Info("Plugin {0} started successfully", pluginName);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to register plugin: {0}", plugin.Name);
            }
        }

        /// <summary>
        /// Loads plugin configuration from file.
        /// </summary>
        private PluginConfig LoadPluginConfig()
        {
            var configPath = Path.Join(Program.AppDataDirectory, "config", "plugins.json");
            
            if (!File.Exists(configPath))
            {
                logger.Info("Plugin configuration not found at {0}, using defaults", configPath);
                return CreateDefaultConfig(configPath);
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<PluginConfig>(json);
                logger.Info("Loaded plugin configuration from {0}", configPath);
                return config ?? new PluginConfig();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load plugin configuration, using defaults");
                return new PluginConfig();
            }
        }

        /// <summary>
        /// Creates a default plugin configuration file.
        /// </summary>
        private PluginConfig CreateDefaultConfig(string configPath)
        {
            var config = new PluginConfig
            {
                Plugins = new Dictionary<string, PluginSettings>
                {
                    ["LocalApiPlugin"] = new PluginSettings
                    {
                        Enabled = true,
                        Settings = new Dictionary<string, object>
                        {
                            ["Port"] = 15342,
                            ["BindAddress"] = "127.0.0.1"
                        }
                    }
                }
            };

            try
            {
                var directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
                logger.Info("Created default plugin configuration at {0}", configPath);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to create default plugin configuration");
            }

            return config;
        }

        /// <summary>
        /// Unloads all plugins.
        /// </summary>
        public void UnloadPlugins()
        {
            logger.Info("Unloading {0} plugin(s)...", _loadedPlugins.Count);
            
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    logger.Info("Stopping plugin: {0}", plugin.Name);
                    plugin.Stop();
                    plugin.Dispose();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error while unloading plugin: {0}", plugin.Name);
                }
            }
            
            _loadedPlugins.Clear();
            logger.Info("All plugins unloaded");
        }

        /// <summary>
        /// Gets the list of loaded plugins.
        /// </summary>
        public IReadOnlyList<IVRCXPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

        /// <summary>
        /// Gets plugin settings by name.
        /// </summary>
        public PluginSettings GetPluginSettings(string pluginName)
        {
            if (_config?.Plugins?.TryGetValue(pluginName, out var settings) == true)
            {
                return settings;
            }
            return new PluginSettings { Enabled = false };
        }
    }
}

#endif
