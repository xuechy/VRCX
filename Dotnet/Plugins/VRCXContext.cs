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
    /// Implementation of IVRCXContext providing plugins with access to VRCX resources.
    /// </summary>
    public class VRCXContext : IVRCXContext
    {
        public IWebBrowser Browser { get; }
        public VRCXStorage Config { get; }
        public string BaseDirectory { get; }
        public string AppDataDirectory { get; }

        public VRCXContext(IWebBrowser browser)
        {
            Browser = browser;
            Config = VRCXStorage.Instance;
            BaseDirectory = Program.BaseDirectory;
            AppDataDirectory = Program.AppDataDirectory;
        }
    }
}

#endif
