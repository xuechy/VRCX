// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

#if ENABLE_LOCAL_API

using CefSharp;

namespace VRCX.Plugins
{
    /// <summary>
    /// Interface for VRCX plugins. Plugins implementing this interface
    /// can be dynamically loaded by the PluginLoader.
    /// </summary>
    public interface IVRCXPlugin
    {
        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the version of the plugin.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initializes the plugin with the VRCX context.
        /// </summary>
        /// <param name="context">The VRCX context containing browser and configuration.</param>
        void Initialize(IVRCXContext context);

        /// <summary>
        /// Starts the plugin services.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the plugin services.
        /// </summary>
        void Stop();

        /// <summary>
        /// Disposes of plugin resources.
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// Context interface providing access to VRCX resources.
    /// </summary>
    public interface IVRCXContext
    {
        /// <summary>
        /// Gets the main CEF browser instance.
        /// </summary>
        IWebBrowser Browser { get; }

        /// <summary>
        /// Gets the VRCX configuration.
        /// </summary>
        VRCXStorage Config { get; }

        /// <summary>
        /// Gets the base directory.
        /// </summary>
        string BaseDirectory { get; }

        /// <summary>
        /// Gets the app data directory.
        /// </summary>
        string AppDataDirectory { get; }
    }
}

#endif
