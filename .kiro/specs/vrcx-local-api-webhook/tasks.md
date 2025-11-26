# Implementation Plan

- [ ] 1. Set up plugin infrastructure
  - Create plugin interface and loader system
  - Establish foundation for modular architecture
  - _Requirements: 8.1, 8.2_

- [ ] 1.1 Create IVRCXPlugin interface
  - Define `Dotnet/Plugins/IVRCXPlugin.cs` with standard plugin contract
  - Define `IVRCXContext` interface for dependency injection
  - Include methods: Initialize(), Start(), Stop(), Dispose()
  - _Requirements: 8.1_

- [ ] 1.2 Implement PluginLoader class
  - Create `Dotnet/Plugins/PluginLoader.cs`
  - Implement plugin discovery and instantiation
  - Implement lifecycle management (load, start, stop, unload)
  - Add error handling for plugin failures
  - _Requirements: 8.1, 8.4_

- [ ] 1.3 Create plugin configuration system
  - Create `config/plugins.json` configuration file
  - Implement configuration loading in PluginLoader
  - Support enable/disable flags for each plugin
  - _Requirements: 8.3_

- [ ] 1.4 Integrate PluginLoader into Program.cs
  - Add conditional compilation directive `#if ENABLE_LOCAL_API`
  - Add PluginLoader initialization after CEF setup (~15 lines)
  - Add PluginLoader cleanup in application exit handler
  - Create VRCXContext with CEF browser reference
  - Register PluginLoader with WebApi for UI control
  - _Requirements: 8.2, 8.3_

- [ ] 1.6 Add WebApi plugin control methods
  - Add `RegisterPluginControl()` method to WebApi.cs
  - Add `RestartLocalApi()` method (called from JavaScript)
  - Add `GetLocalApiStatus()` method (called from JavaScript)
  - Wrap all additions in `#if ENABLE_LOCAL_API`
  - Add JSON serialization for configuration objects
  - _Requirements: 4.2, 4.4_

- [ ] 1.5 Write property test for plugin lifecycle
  - **Property 12: Plugin lifecycle consistency**
  - **Validates: Requirements 8.4**

- [ ] 2. Implement Local API Server plugin
  - Create HTTP server as independent plugin module
  - Implement CEF bridge for frontend data access
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.4, 5.1, 5.2, 5.3, 7.1_

- [ ] 2.1 Create LocalApiServer plugin structure
  - Create directory `Dotnet/Plugins/LocalApiPlugin/`
  - Create `LocalApiServer.cs` implementing IVRCXPlugin
  - Create `ApiConfiguration` class for configuration data
  - Implement Initialize() to receive IVRCXContext
  - Implement Start() to begin HTTP listening
  - Implement Stop() to gracefully shutdown
  - Implement UpdateConfiguration() to change settings at runtime
  - Implement Restart() to stop and start with new configuration
  - Implement GetCurrentConfiguration() to query current settings
  - Add IsRunning property to track server state
  - _Requirements: 1.1, 2.1, 4.2, 4.4, 8.1_

- [ ] 2.2 Implement HTTP listener and routing
  - Initialize HttpListener bound to 127.0.0.1:15342
  - Implement async request handling loop
  - Create basic routing logic for /api/* endpoints
  - Add error handling for port binding failures
  - _Requirements: 1.1, 2.1, 2.3, 7.1_

- [ ] 2.3 Create CefBridge class
  - Create `Dotnet/Plugins/LocalApiPlugin/CefBridge.cs`
  - Implement ExecuteQuery<T>() using EvaluateScriptAsync
  - Add 5-second timeout for JavaScript execution
  - Implement JSON parsing for CEF responses
  - Add error handling for execution failures
  - _Requirements: 1.3, 5.1, 5.2, 5.3_

- [ ] 2.4 Write property test for CEF JavaScript execution
  - **Property 8: CEF JavaScript execution round-trip**
  - **Validates: Requirements 5.1, 5.2, 5.3**

- [ ] 2.5 Implement ApiRoutes class
  - Create `Dotnet/Plugins/LocalApiPlugin/ApiRoutes.cs`
  - Implement route parsing and parameter extraction
  - Create response builders for JSON serialization
  - Add HTTP status code handling
  - _Requirements: 1.2, 1.4, 1.5_

- [ ] 2.6 Implement /api/info endpoint
  - Handle GET requests to /api/info
  - Return JSON with version, status, apiVersion, uptime
  - Use CefBridge to query VRCX version if needed
  - _Requirements: 1.2_

- [ ] 2.7 Write property test for /api/info endpoint
  - **Property 1: API info endpoint response structure**
  - **Validates: Requirements 1.2**

- [ ] 2.8 Implement /api/users/{userId} endpoint
  - Handle GET requests with userId parameter
  - Construct JavaScript query for Pinia userStore
  - Execute query via CefBridge
  - Return user data JSON or 404 if not found
  - _Requirements: 1.3, 1.4, 1.5_

- [ ] 2.9 Write property test for user data response
  - **Property 2: User data response completeness**
  - **Validates: Requirements 1.4**

- [ ] 2.10 Write property test for missing user handling
  - **Property 3: Missing user error handling**
  - **Validates: Requirements 1.5**

- [ ] 2.11 Implement concurrent request handling
  - Ensure HttpListener uses thread pool for requests
  - Verify CEF calls don't block UI thread
  - Add request queuing if needed
  - _Requirements: 2.4_

- [ ] 2.12 Write property test for concurrent requests
  - **Property 4: Concurrent request handling**
  - **Validates: Requirements 2.4**

- [ ] 2.13 Write property test for configuration updates
  - **Property 11: Configuration update consistency**
  - **Validates: Requirements 4.2, 4.4**

- [ ] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 4. Implement Webhook plugin (frontend)
  - Create Pinia plugin for event monitoring
  - Implement webhook configuration store
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 6.1, 6.2, 6.3, 6.4_

- [ ] 4.1 Create event bus infrastructure
  - Create `src/plugins/eventBus.js` if not exists
  - Implement on(), off(), emit(), once() methods
  - Or identify and document existing VRCX event system
  - _Requirements: 8.1_

- [ ] 4.2 Create useWebhookStore
  - Create `src/stores/webhook.js`
  - Define state schema (enabled, targetUrl, retryCount, events, eventCounts)
  - Implement localStorage persistence
  - Implement sendWebhook() action using window.WebApi
  - Implement updateConfig() action
  - Implement resetStats() action
  - _Requirements: 4.1, 4.2_

- [ ] 4.3 Write property test for webhook configuration persistence
  - **Property 6: Webhook configuration persistence**
  - **Validates: Requirements 4.2**

- [ ] 4.4 Implement webhookPlugin.js
  - Create `src/plugins/webhookPlugin.js`
  - Implement Pinia plugin function
  - Use $onAction to intercept gameLog store actions
  - Identify player-joined and location-changed events
  - _Requirements: 3.1, 6.1, 8.1_

- [ ] 4.5 Implement player-joined webhook handler
  - Detect player-joined events from gameLog
  - Retrieve complete user data from userStore
  - Construct JSON payload with event, timestamp, user data
  - Call webhookStore.sendWebhook() with payload
  - Handle missing user data gracefully
  - _Requirements: 3.1, 3.2, 3.3_

- [ ] 4.6 Write property test for player-joined webhook
  - **Property 5: Player-joined webhook delivery**
  - **Validates: Requirements 3.1, 3.2, 3.3**

- [ ] 4.7 Implement location-changed webhook handler
  - Detect location-changed events from gameLog
  - Extract world ID, instance ID, and details
  - Construct JSON payload with event, timestamp, location data
  - Call webhookStore.sendWebhook() with payload
  - _Requirements: 6.1, 6.2, 6.3_

- [ ] 4.8 Write property test for location-changed webhook
  - **Property 9: Location change webhook delivery**
  - **Validates: Requirements 6.1, 6.2, 6.3**

- [ ] 4.9 Implement webhook disabled state handling
  - Check webhookStore.enabled before sending
  - Check webhookStore.events[eventType] for event-specific enable
  - Skip webhook silently when disabled
  - _Requirements: 3.4, 4.3, 4.4_

- [ ] 4.10 Write property test for webhook disabled state
  - **Property 7: Webhook disabled state**
  - **Validates: Requirements 4.3**

- [ ] 4.11 Implement webhook retry logic
  - Add retry counter and exponential backoff
  - Catch HTTP errors from WebApi
  - Log errors to webhookStore.lastError
  - Update eventCounts.failed on permanent failure
  - _Requirements: 3.5_

- [ ] 4.12 Implement rapid event handling
  - Use async queue for webhook sending
  - Ensure all events are queued without dropping
  - Process queue in order
  - _Requirements: 6.4_

- [ ] 4.13 Write property test for rapid event handling
  - **Property 10: Rapid event handling**
  - **Validates: Requirements 6.4**

- [ ] 4.14 Register webhookPlugin in main.js
  - Import webhookPlugin from './plugins/webhookPlugin'
  - Add pinia.use(webhookPlugin) after Pinia creation (~3 lines)
  - Verify no modifications needed to existing stores
  - _Requirements: 8.2_

- [ ] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Add unified plugin configuration UI
  - Create comprehensive settings panel for both Local API and Webhook
  - Allow users to configure all plugin features through UI
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.7, 4.8_

- [ ] 6.1 Create plugin configuration store
  - Create `src/stores/pluginConfig.js` for Local API settings
  - Define state schema (enabled, port, bindAddress, status, isRunning)
  - Implement localStorage persistence (key: vrcx_plugin_localapi_config)
  - Implement actions:
    - updateConfig(): Save config to localStorage
    - restartServer(): Call window.WebApi.RestartLocalApi()
    - getStatus(): Call window.WebApi.GetLocalApiStatus()
    - testConnection(): Open /api/info in browser
  - Add validation for port (1024-65535) and IP address format
  - Add error handling for WebApi calls
  - _Requirements: 4.2, 4.4, 4.7, 4.8_

- [ ] 6.2 Create unified plugin settings component
  - Create Vue component `src/components/PluginSettings.vue`
  - Add "Plugins" section to VRCX settings navigation
  - Create two-section layout: Local API and Webhook
  - _Requirements: 4.1_

- [ ] 6.3 Implement Local API settings section
  - Add enable/disable toggle with status indicator (Running/Stopped)
  - Add port number input with validation (1024-65535)
  - Add bind address input with dropdown (127.0.0.1, 0.0.0.0, custom)
  - Add security warning for 0.0.0.0 binding
  - Add "Test API" button (opens /api/info in browser)
  - Display current API URL (e.g., http://127.0.0.1:15342)
  - Bind to pluginConfig store
  - _Requirements: 4.1, 4.2, 4.4, 4.7, 4.8_

- [ ] 6.4 Implement Webhook settings section
  - Add enable/disable toggle
  - Add target URL input field with validation
  - Add event type checkboxes (Player Joined, Location Changed)
  - Add retry configuration inputs (count, delay)
  - Add "Test Webhook" button (sends test payload)
  - Display statistics (total sent, failed, last error)
  - Add "Reset Statistics" button
  - Bind to useWebhookStore
  - _Requirements: 4.1, 4.2, 4.3, 4.5, 4.6_

- [ ] 6.5 Implement configuration validation
  - Validate port number range and format
  - Validate IP address format
  - Validate URL format for webhook
  - Show inline error messages for invalid inputs
  - Prevent saving invalid configurations
  - _Requirements: 4.7, 4.8_

- [ ] 6.6 Implement API server restart on configuration change
  - When Local API settings change, call plugin to restart server
  - Show loading indicator during restart
  - Display success/error message after restart
  - Update status indicator to reflect new state
  - _Requirements: 4.2, 4.4_

- [ ] 6.7 Implement test functionality
  - Implement "Test API" button: open /api/info in new tab
  - Implement "Test Webhook" button: send test payload to configured URL
  - Display test results (success/failure) with details
  - _Requirements: 4.1_

- [ ] 6.8 Integrate settings component into VRCX
  - Add "Plugins" menu item to VRCX settings navigation
  - Register PluginSettings component in router
  - Ensure settings persist across application restarts
  - _Requirements: 4.1, 4.2_

- [ ] 6.9 Write unit tests for plugin settings
  - Test Local API configuration validation
  - Test Webhook configuration validation
  - Test configuration persistence
  - Test toggle functionality
  - Test server restart logic
  - Test test button functionality
  - _Requirements: 4.1, 4.2, 4.7, 4.8_

- [ ] 7. Configure GitHub Actions for Fork builds
  - Modify workflow to skip signing and Sentry steps
  - Enable manual workflow dispatch
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

- [ ] 7.1 Modify GitHub Actions workflow file
  - Open `.github/workflows/github_actions.yml`
  - Ensure `workflow_dispatch` trigger is present
  - Remove or comment out code signing steps (Sign Dotnet executables, Sign Cef setup)
  - Remove or comment out Sentry token environment variables
  - Add conditional checks for secrets before using them
  - _Requirements: 9.1, 9.2, 9.3_

- [ ] 7.2 Add conditional compilation support
  - Add MSBuild parameter `/p:DefineConstants=ENABLE_LOCAL_API` to build steps
  - Verify plugin code is included in build
  - Test that build succeeds without signing secrets
  - _Requirements: 8.3, 9.4_

- [ ] 7.3 Test Fork build workflow
  - Trigger workflow manually via GitHub Actions UI
  - Verify build completes successfully
  - Download and verify artifacts are produced
  - Test that unsigned executables run correctly
  - _Requirements: 9.4, 9.5_

- [ ] 8. Final integration testing
  - Test complete API and webhook functionality
  - Verify plugin can be disabled
  - Verify minimal core code changes

- [ ] 8.1 Test Local API Server end-to-end
  - Start VRCX with plugin enabled
  - Verify server listens on localhost:15342
  - Test /api/info endpoint with external HTTP client
  - Test /api/users/{userId} with known user ID
  - Test /api/users/{userId} with unknown user ID
  - Verify responses match expected format
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [ ] 8.2 Test Webhook system end-to-end
  - Configure webhook target URL to test server
  - Enable webhook in settings
  - Trigger player-joined event (join instance with other players)
  - Verify webhook POST received at test server
  - Trigger location-changed event (change worlds)
  - Verify webhook POST received at test server
  - Disable webhook and verify no POSTs sent
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.3, 6.1, 6.2, 6.3_

- [ ] 8.3 Test plugin disable functionality
  - Set LocalApiPlugin.enabled = false in config
  - Restart VRCX
  - Verify API server does not start
  - Verify VRCX functions normally
  - _Requirements: 8.4_

- [ ] 8.4 Verify minimal core code changes
  - Review Program.cs changes (should be ~10-15 lines)
  - Review main.js changes (should be ~3 lines)
  - Verify all other code is in plugin directories
  - Verify conditional compilation directives are in place
  - _Requirements: 8.2, 8.3_

- [ ] 8.5 Test upstream merge compatibility
  - Create test branch with plugin code
  - Attempt to merge hypothetical upstream changes
  - Verify no merge conflicts in core files
  - _Requirements: 8.5_

- [ ] 9. Final Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
