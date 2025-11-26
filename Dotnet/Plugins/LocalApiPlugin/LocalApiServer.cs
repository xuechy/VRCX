// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

#if ENABLE_LOCAL_API

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace VRCX.Plugins.LocalApiPlugin
{
    /// <summary>
    /// Local HTTP API server plugin for VRCX.
    /// Provides external programs with access to VRCX cached data.
    /// </summary>
    public class LocalApiServer : IVRCXPlugin
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        public string Name => "LocalApiPlugin";
        public string Version => "1.0.0";

        private HttpListener _httpListener;
        private CefBridge _cefBridge;
        private ApiRoutes _apiRoutes;
        private CancellationTokenSource _cts;
        private Task _listenerTask;
        private IVRCXContext _context;
        private PluginLoader _pluginLoader;

        private string _bindAddress = "127.0.0.1";
        private int _port = 15342;

        public void Initialize(IVRCXContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            logger.Info("Initializing {0} v{1}", Name, Version);

            // Load settings from config if available
            // For now, use defaults
            _bindAddress = "127.0.0.1";
            _port = 15342;

            _cefBridge = new CefBridge(context.Browser);
            logger.Info("{0} initialized", Name);
        }

        public void Start()
        {
            try
            {
                logger.Info("Starting {0}...", Name);

                _cts = new CancellationTokenSource();
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://{_bindAddress}:{_port}/");

                _httpListener.Start();
                logger.Info("HTTP API server listening on http://{0}:{1}/", _bindAddress, _port);

                // Start listening task
                _listenerTask = Task.Run(() => ListenAsync(_cts.Token), _cts.Token);

                logger.Info("{0} started successfully", Name);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to start {0}", Name);
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                logger.Info("Stopping {0}...", Name);

                _cts?.Cancel();
                _httpListener?.Stop();
                _listenerTask?.Wait(TimeSpan.FromSeconds(5));

                logger.Info("{0} stopped", Name);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error stopping {0}", Name);
            }
        }

        public void Dispose()
        {
            _httpListener?.Close();
            _cts?.Dispose();
            logger.Info("{0} disposed", Name);
        }

        /// <summary>
        /// Main listener loop.
        /// </summary>
        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            // Initialize ApiRoutes here to avoid circular dependency
            // We need to pass the PluginLoader reference, but we'll handle that separately
            _apiRoutes = new ApiRoutes(_cefBridge, null);

            logger.Info("API listener task started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var contextTask = _httpListener.GetContextAsync();
                    var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
                    
                    var completedTask = await Task.WhenAny(contextTask, cancelTask);

                    if (completedTask == cancelTask)
                    {
                        break;
                    }

                    var context = await contextTask;
                    
                    // Handle request in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _apiRoutes.HandleRequestAsync(context);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "Error handling request");
                        }
                    }, cancellationToken);
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        logger.Error(ex, "Error in listener loop");
                    }
                }
            }

            logger.Info("API listener task ended");
        }

        /// <summary>
        /// Sets the plugin loader reference (for plugin listing).
        /// </summary>
        public void SetPluginLoader(PluginLoader loader)
        {
            _pluginLoader = loader;
            if (_apiRoutes != null)
            {
                _apiRoutes = new ApiRoutes(_cefBridge, loader);
            }
        }
    }
}

#endif
