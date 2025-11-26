# Design Document

## Overview

This design implements a local HTTP API server and Webhook notification system for VRCX using a **modular plugin architecture**. The architecture leverages VRCX's existing in-memory data structures (Pinia stores) to provide external applications with cached player information without additional VRChat API calls. 

**Key Design Principles:**
- **Minimal Invasiveness**: Only 10-15 lines of code changes to core VRCX files
- **Plugin-Based**: All new functionality encapsulated in independent plugin modules
- **Loose Coupling**: Communication via interfaces and event bus, not direct code modification
- **Conditional Compilation**: Can be completely removed via compile-time flags
- **Easy Maintenance**: Upstream updates merge without conflicts

The system consists of two main plugin components:

1. **Local API Server Plugin (C# Backend)**: An HTTP server plugin that implements `IVRCXPlugin` interface, listens on localhost, and serves cached data by executing JavaScript in the CEF browser context
2. **Webhook Plugin (JavaScript Frontend)**: A Pinia plugin that monitors game events via action interception and pushes notifications to external services

The design prioritizes reusing existing VRCX infrastructure (CEF, WebApi bridge, Pinia stores) while maintaining complete modularity for easy removal and upstream compatibility.

## Architecture

### High-Level Architecture (Modular Plugin Design)

```
┌──────────────────────────────────────────────────────────────────┐
│                      VRCX Core Application                        │
│  ┌────────────────────┐         ┌──────────────────────────────┐ │
│  │   C# Backend       │         │   JavaScript Frontend        │ │
│  │                    │         │                              │ │
│  │  ┌──────────────┐  │         │  ┌─────────────────────────┐│ │
│  │  │  Program.cs  │  │         │  │      main.js            ││ │
│  │  │  (~10 lines) │  │         │  │    (~3 lines added)     ││ │
│  │  └──────┬───────┘  │         │  └───────────┬─────────────┘│ │
│  │         │           │         │              │              │ │
│  │         │ #if ENABLE_LOCAL_API                │              │ │
│  │         ▼           │         │              ▼              │ │
│  │  ┌──────────────┐  │         │  ┌─────────────────────────┐│ │
│  │  │ PluginLoader │  │         │  │   Pinia + Plugin        ││ │
│  │  └──────┬───────┘  │         │  │   pinia.use(webhook)    ││ │
│  │         │           │         │  └───────────┬─────────────┘│ │
│  └─────────┼───────────┘         └──────────────┼──────────────┘ │
└────────────┼──────────────────────────────────────┼───────────────┘
             │                                      │
             │ Loads Plugins                        │ Registers Plugin
             ▼                                      ▼
┌────────────────────────────┐      ┌─────────────────────────────┐
│  LocalApiPlugin (C#)       │      │  webhookPlugin.js           │
│  Dotnet/Plugins/           │      │  src/plugins/               │
│  ┌──────────────────────┐  │      │  ┌────────────────────────┐│
│  │ LocalApiServer.cs    │  │      │  │ Pinia $onAction Hook   ││
│  │ (IVRCXPlugin impl)   │  │      │  │ Intercepts gameLog     ││
│  ├──────────────────────┤  │      │  ├────────────────────────┤│
│  │ ApiRoutes.cs         │  │      │  │ useWebhookStore        ││
│  ├──────────────────────┤  │      │  │ (Independent Store)    ││
│  │ CefBridge.cs         │◄─┼──────┼──┤ Reads Pinia Stores     ││
│  │ (CEF JS Execution)   │  │ CEF  │  └────────────────────────┘│
│  └──────────────────────┘  │ Eval │              │             │
│           │                 │      │              │             │
└───────────┼─────────────────┘      └──────────────┼─────────────┘
            │                                       │
            │ HTTP Response                         │ HTTP POST
            ▼                                       ▼
   ┌─────────────────┐                   ┌──────────────────────┐
   │ External Apps   │                   │ External Webhook     │
   │ (HTTP GET)      │                   │ Server (HTTP POST)   │
   │ localhost:15342 │                   │ User-configured URL  │
   └─────────────────┘                   └──────────────────────┘

Key Design Features:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ Minimal Core Changes: Only ~13 lines added to VRCX core
✓ Plugin Isolation: All new code in separate directories
✓ Conditional Compilation: Can be removed via #if ENABLE_LOCAL_API
✓ Zero Store Modifications: Pinia plugin intercepts actions
✓ Easy Upstream Merge: No conflicts with VRCX updates
```

### Data Flow

**API Query Flow:**
1. External application sends HTTP GET request to `http://127.0.0.1:15342/api/users/{userId}`
2. LocalApiServer receives request and constructs JavaScript query expression
3. LocalApiServer executes JavaScript in CEF context via `EvaluateScriptAsync`
4. JavaScript accesses Pinia userStore and returns serialized user data
5. LocalApiServer parses JSON response and returns HTTP response to client

**Webhook Push Flow:**
1. VRChat game log is parsed by existing gameLogStore
2. Event (player-joined, location-change) is detected
3. Webhook system retrieves complete user/location data from Pinia stores
4. Webhook system constructs JSON payload with event details
5. Webhook system calls WebApi bridge to send HTTP POST
6. C# WebApi sends POST request to configured external URL

## Components and Interfaces

### C# Backend Components

#### Plugin Infrastructure

##### IVRCXPlugin Interface

**Location:** `Dotnet/Plugins/IVRCXPlugin.cs`

**Purpose:** Defines the standard contract for all VRCX plugins

**Interface Definition:**
```csharp
public interface IVRCXPlugin
{
    string Name { get; }
    string Version { get; }
    bool IsEnabled { get; }
    
    void Initialize(IVRCXContext context);
    void Start();
    void Stop();
    void Dispose();
}

public interface IVRCXContext
{
    IWebBrowser CefBrowser { get; }
    IConfiguration Configuration { get; }
    ILogger Logger { get; }
}
```

##### PluginLoader Class

**Location:** `Dotnet/Plugins/PluginLoader.cs`

**Responsibilities:**
- Load plugin configuration from `config/plugins.json`
- Dynamically instantiate plugin classes
- Manage plugin lifecycle (Initialize → Start → Stop → Dispose)
- Handle plugin errors gracefully without crashing VRCX

**Public Interface:**
```csharp
public class PluginLoader
{
    public void LoadPlugins(IVRCXContext context);
    public void UnloadPlugins();
    public IEnumerable<IVRCXPlugin> GetLoadedPlugins();
}
```

#### LocalApiPlugin (Independent Module)

##### LocalApiServer Class

**Location:** `Dotnet/Plugins/LocalApiPlugin/LocalApiServer.cs`

**Responsibilities:**
- Implement `IVRCXPlugin` interface
- Initialize and manage HttpListener lifecycle
- Route incoming HTTP requests to appropriate handlers
- Execute JavaScript in CEF context to retrieve frontend data
- Serialize and return JSON responses

**Public Interface:**
```csharp
public class LocalApiServer : IVRCXPlugin
{
    public string Name => "LocalApiPlugin";
    public string Version => "1.0.0";
    public bool IsEnabled { get; private set; }
    public bool IsRunning { get; private set; }
    
    // IVRCXPlugin implementation
    public void Initialize(IVRCXContext context);
    public void Start();
    public void Stop();
    public void Dispose();
    
    // Configuration management
    public void UpdateConfiguration(ApiConfiguration config);
    public void Restart();
    public ApiConfiguration GetCurrentConfiguration();
    
    // Internal components (private)
    private ApiRoutes _routes;
    private CefBridge _cefBridge;
    private HttpListener _listener;
    private ApiConfiguration _config;
}

public class ApiConfiguration
{
    public bool Enabled { get; set; }
    public int Port { get; set; }
    public string BindAddress { get; set; }
    public int RequestTimeout { get; set; }
}
```

**Configuration:**
- Default port: 15342
- Bind address: 127.0.0.1 (localhost only)
- Request timeout: 5 seconds for JavaScript execution

##### ApiRoutes Class

**Location:** `Dotnet/Plugins/LocalApiPlugin/ApiRoutes.cs`

**Responsibilities:**
- Handle HTTP request routing
- Parse URL parameters
- Construct JSON responses
- Handle errors and return appropriate status codes

##### CefBridge Class

**Location:** `Dotnet/Plugins/LocalApiPlugin/CefBridge.cs`

**Responsibilities:**
- Encapsulate CEF JavaScript execution
- Construct JavaScript queries for Pinia stores
- Parse JSON responses from JavaScript
- Handle execution timeouts and errors

**Public Interface:**
```csharp
public class CefBridge
{
    public CefBridge(IWebBrowser browser);
    public Task<T> ExecuteQuery<T>(string script, int timeoutMs = 5000);
    public Task<UserData> GetUserData(string userId);
}
```

#### Program.cs Modifications (Minimal Changes)

**Changes Required:**
- Add plugin loader initialization (wrapped in conditional compilation)
- Expose plugin control methods to WebApi bridge
- Total lines added: ~15-20

```csharp
#if ENABLE_LOCAL_API
    // After CEF initialization
    private static PluginLoader _pluginLoader;
    
    // In startup sequence
    var context = new VRCXContext {
        CefBrowser = mainForm.Browser,
        Configuration = config,
        Logger = logger
    };
    _pluginLoader = new PluginLoader();
    _pluginLoader.LoadPlugins(context);
    
    // Expose plugin control to WebApi (for UI configuration)
    WebApi.RegisterPluginControl(_pluginLoader);
    
    // In application exit handler
    _pluginLoader?.UnloadPlugins();
#endif
```

#### WebApi Extensions (for Plugin Control)

**Location:** `Dotnet/WebApi.cs` (minimal additions)

**Purpose:** Allow JavaScript UI to control plugins

**Added Methods:**
```csharp
#if ENABLE_LOCAL_API
    private static PluginLoader _pluginLoader;
    
    public static void RegisterPluginControl(PluginLoader loader)
    {
        _pluginLoader = loader;
    }
    
    // Called from JavaScript
    public static string RestartLocalApi(string configJson)
    {
        var plugin = _pluginLoader.GetPlugin("LocalApiPlugin") as LocalApiServer;
        var config = JsonConvert.DeserializeObject<ApiConfiguration>(configJson);
        plugin?.UpdateConfiguration(config);
        plugin?.Restart();
        return JsonConvert.SerializeObject(new { success = true, status = plugin?.IsRunning });
    }
    
    public static string GetLocalApiStatus()
    {
        var plugin = _pluginLoader.GetPlugin("LocalApiPlugin") as LocalApiServer;
        return JsonConvert.SerializeObject(new {
            isRunning = plugin?.IsRunning ?? false,
            config = plugin?.GetCurrentConfiguration()
        });
    }
#endif
```

**Advantages:**
- Can be completely removed via compile flag
- No changes to existing VRCX logic
- Easy to merge upstream updates
- Enables UI to control backend plugins

### JavaScript Frontend Components

#### Event Bus (Infrastructure)

**Location:** `src/plugins/eventBus.js`

**Purpose:** Provide decoupled communication between plugins and stores

**Public Interface:**
```javascript
export const eventBus = {
    on(event, handler),
    off(event, handler),
    emit(event, data),
    once(event, handler)
};
```

**Note:** If VRCX already has an event system, reuse it instead of creating new one.

#### Webhook Store (Independent Module)

**Location:** `src/stores/webhook.js`

**Responsibilities:**
- Manage webhook configuration (enabled, targetUrl, retryCount)
- Persist configuration to localStorage
- Provide methods to send webhook notifications
- Handle retry logic for failed requests

**State Schema:**
```javascript
{
    enabled: boolean,
    targetUrl: string,
    retryCount: number,
    retryDelay: number,
    lastError: string | null,
    eventCounts: {
        playerJoined: number,
        locationChanged: number,
        failed: number
    },
    events: {
        playerJoined: boolean,
        locationChanged: boolean
    }
}
```

**Public Interface:**
```javascript
export const useWebhookStore = defineStore('webhook', {
    state: () => ({ /* ... */ }),
    
    actions: {
        async sendWebhook(event, data),
        updateConfig(config),
        resetStats(),
        testWebhook()
    },
    
    getters: {
        isEventEnabled: (state) => (eventType) => state.events[eventType]
    }
});
```

#### Webhook Plugin (Pinia Plugin)

**Location:** `src/plugins/webhookPlugin.js`

**Purpose:** Intercept Pinia store actions to trigger webhooks without modifying existing stores

**Implementation Strategy:**
```javascript
export function webhookPlugin({ store }) {
    // Only intercept gameLog store
    if (store.$id === 'gameLog') {
        // Use Pinia's $onAction to listen for actions
        store.$onAction(({ name, args, after, onError }) => {
            after((result) => {
                // Trigger webhooks based on action name
                if (name === 'addLogEntry') {
                    handleLogEntry(args[0]);
                }
            });
        });
    }
}

async function handleLogEntry(logEntry) {
    const webhookStore = useWebhookStore();
    if (!webhookStore.enabled) return;
    
    // Parse log entry and trigger appropriate webhook
    if (logEntry.type === 'player-joined') {
        await sendPlayerJoinedWebhook(logEntry);
    } else if (logEntry.type === 'location-changed') {
        await sendLocationChangedWebhook(logEntry);
    }
}
```

**Advantages:**
- **Zero modifications** to gameLog.js
- Uses Pinia's built-in plugin system
- Can be disabled by not registering the plugin
- Completely decoupled from existing code

#### main.js Modifications (Minimal Changes)

**Changes Required:**
- Register webhook plugin with Pinia
- Total lines added: ~3

```javascript
// After Pinia initialization
import { webhookPlugin } from './plugins/webhookPlugin';

const pinia = createPinia();
pinia.use(webhookPlugin);  // Add this single line

app.use(pinia);
```

**Advantages:**
- Single line of code to enable/disable
- No changes to existing stores
- Easy to remove for upstream merges

#### gameLog.js (NO MODIFICATIONS REQUIRED)

**Integration Method:**
- Webhook plugin subscribes to gameLog actions via Pinia's `$onAction` hook
- No direct code changes needed in gameLog.js
- Maintains complete separation of concerns

**How It Works:**
1. gameLog.js adds log entries as normal (no changes)
2. Pinia's plugin system notifies webhookPlugin of the action
3. webhookPlugin inspects the log entry and triggers webhooks
4. If webhookPlugin is not registered, gameLog.js works exactly as before

## Data Models

### API Response Models

#### InfoResponse
```json
{
    "version": "string",
    "status": "running" | "error",
    "apiVersion": "1.0",
    "uptime": "number (seconds)"
}
```

#### UserResponse
```json
{
    "id": "string (usr_xxx)",
    "displayName": "string",
    "currentAvatarImageUrl": "string (URL)",
    "$trustLevel": "string",
    "status": "active" | "join me" | "ask me" | "busy" | "offline",
    "statusDescription": "string",
    "location": "string (wrld_xxx:instance_id)",
    "lastSeen": "string (ISO8601)"
}
```

#### ErrorResponse
```json
{
    "error": "string",
    "message": "string",
    "statusCode": "number"
}
```

### Webhook Payload Models

#### PlayerJoinedEvent
```json
{
    "event": "player-joined",
    "timestamp": "string (ISO8601)",
    "data": {
        "id": "string (usr_xxx)",
        "displayName": "string",
        "status": "string",
        "location": "string",
        "trustLevel": "string",
        "avatarImageUrl": "string"
    }
}
```

#### LocationChangedEvent
```json
{
    "event": "location-changed",
    "timestamp": "string (ISO8601)",
    "data": {
        "worldId": "string (wrld_xxx)",
        "instanceId": "string",
        "worldName": "string",
        "accessType": "public" | "friends+" | "friends" | "invite+" | "invite",
        "playerCount": "number"
    }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property Reflection

Before defining properties, I've reviewed the prework analysis to eliminate redundancy:

- Properties 3.3 and 6.3 both test webhook sending mechanism - these can be combined into a single property about webhook delivery
- Properties 1.3, 5.1, and 5.2 all test the JavaScript execution pipeline - these can be combined into a comprehensive property about CEF integration
- Properties 3.1, 3.2, and 3.3 form a pipeline that can be tested as a single end-to-end property for player-joined events
- Properties 6.1, 6.2, and 6.3 form a similar pipeline for location events

After consolidation, the following properties provide unique validation value:

### Property 1: API info endpoint response structure
*For any* request to /api/info endpoint, the response should be valid JSON containing "version", "status", and "apiVersion" fields
**Validates: Requirements 1.2**

### Property 2: User data response completeness
*For any* userId that exists in the Pinia Store, the API response should contain all required fields: id, displayName, currentAvatarImageUrl, and $trustLevel
**Validates: Requirements 1.4**

### Property 3: Missing user error handling
*For any* userId that does not exist in the Pinia Store, the API should return HTTP status code 404
**Validates: Requirements 1.5**

### Property 4: Concurrent request handling
*For any* set of concurrent HTTP requests, all requests should receive responses without blocking the UI thread
**Validates: Requirements 2.4**

### Property 5: Player-joined webhook delivery
*For any* player-joined event with complete user data, a webhook POST request should be sent containing all required fields (event, timestamp, user data)
**Validates: Requirements 3.1, 3.2, 3.3**

### Property 6: Webhook configuration persistence
*For any* webhook configuration change, the new configuration should be immediately retrievable from localStorage
**Validates: Requirements 4.2**

### Property 7: Webhook disabled state
*For any* game event when webhook is disabled, no HTTP POST requests should be sent
**Validates: Requirements 4.3**

### Property 8: CEF JavaScript execution round-trip
*For any* user data object in Pinia Store, executing JavaScript to retrieve it and parsing the result should produce an equivalent object
**Validates: Requirements 5.1, 5.2, 5.3**

### Property 9: Location change webhook delivery
*For any* location-changed event with location details, a webhook POST request should be sent containing event type, timestamp, worldId, and instance details
**Validates: Requirements 6.1, 6.2, 6.3**

### Property 10: Rapid event handling
*For any* sequence of rapid location changes, the number of webhooks sent should equal the number of events without dropping any
**Validates: Requirements 6.4**

### Property 11: Configuration update consistency
*For any* valid configuration change (port, bind address), after restarting the API server, querying the server status should return the new configuration values
**Validates: Requirements 4.2, 4.4**

## Error Handling

### HTTP API Errors

**Port Binding Failure:**
- Catch `HttpListenerException` during Start()
- Log error with port number and exception details
- Set internal state to indicate API is unavailable
- Allow VRCX to continue startup normally

**JavaScript Execution Timeout:**
- Set 5-second timeout on `EvaluateScriptAsync`
- Return HTTP 504 Gateway Timeout if exceeded
- Log timeout event with requested userId

**JavaScript Execution Error:**
- Catch exceptions from `EvaluateScriptAsync`
- Return HTTP 500 Internal Server Error
- Include sanitized error message in response
- Log full exception details

**Invalid Request Format:**
- Validate URL path and parameters
- Return HTTP 400 Bad Request for malformed requests
- Include descriptive error message

### Webhook Errors

**Network Failure:**
- Catch HTTP exceptions from WebApi POST
- Log error with webhook URL and exception
- Optionally retry based on configuration (max 3 retries with exponential backoff)
- Update webhook statistics (failed count)

**Missing Configuration:**
- Check if targetUrl is empty or null before sending
- Skip webhook silently (no error logging)
- This is expected behavior, not an error

**Data Unavailable:**
- If user data cannot be retrieved from store, skip webhook
- Log warning indicating incomplete data
- Do not send partial webhook payloads

## Testing Strategy

### Unit Testing Framework

**C# Backend:**
- Framework: xUnit or NUnit
- Mocking: Moq for HttpListener and CEF interfaces
- Test project: `Dotnet.Tests/LocalApiServerTests.cs`

**JavaScript Frontend:**
- Framework: Jest (already configured in project)
- Test files: `src/stores/__tests__/webhook.test.js`

### Property-Based Testing

**Framework:** 
- C#: FsCheck (F# property testing library with C# support)
- JavaScript: fast-check

**Configuration:**
- Minimum 100 iterations per property test
- Each property test must reference its design document property number

**Test Tagging Format:**
```csharp
// Feature: vrcx-local-api-webhook, Property 2: User data response completeness
[Property]
public Property UserDataResponseCompleteness() { /* ... */ }
```

```javascript
// Feature: vrcx-local-api-webhook, Property 5: Player-joined webhook delivery
fc.assert(fc.property(/* ... */), { numRuns: 100 });
```

### Unit Testing Approach

**API Server Tests:**
- Test server lifecycle (start, stop, restart)
- Test route handling for each endpoint
- Test error responses for invalid requests
- Test localhost binding configuration
- Mock CEF browser for JavaScript execution tests

**Webhook Store Tests:**
- Test configuration persistence to localStorage
- Test webhook sending with mocked WebApi
- Test enabled/disabled state behavior
- Test statistics tracking

**Integration Points:**
- Test CEF JavaScript execution with test browser instance
- Test gameLog event triggering with test events
- Test WebApi bridge with mock HTTP client

### Test Coverage Goals

- C# Backend: >80% line coverage for LocalApiServer
- JavaScript Frontend: >80% line coverage for webhook store
- All correctness properties must have corresponding property-based tests
- All error handling paths must have unit tests

## Security Considerations

### Network Security

**Localhost Binding:**
- HttpListener must bind only to 127.0.0.1
- Reject any configuration attempts to bind to 0.0.0.0 or external IPs
- Document that API is intentionally local-only

**No Authentication:**
- Rely on localhost binding as security boundary
- Any process on the local machine can access the API
- This is acceptable for the use case (local overlay applications)

### Data Privacy

**Cached Data Only:**
- API only exposes data already cached by VRCX
- No additional VRChat API calls are made
- Users have already consented to VRCX caching this data

**Webhook Data:**
- Webhooks send data to user-configured URLs
- Users are responsible for securing their webhook endpoints
- Include warning in configuration UI about data being sent externally

### Input Validation

**API Requests:**
- Validate userId format (must match usr_xxx pattern)
- Sanitize all user inputs before using in JavaScript expressions
- Prevent JavaScript injection through userId parameter

**Webhook Configuration:**
- Validate URL format for webhook target
- Warn users about sending data to external services
- No validation of SSL certificates (user responsibility)

## Performance Considerations

### API Server Performance

**Request Handling:**
- Use async/await for all I/O operations
- HttpListener handles requests on thread pool
- JavaScript execution may block briefly (typically <100ms)
- Recommend external apps limit queries to 1 per second

**Memory Usage:**
- Minimal overhead (HttpListener + small routing logic)
- No caching beyond what VRCX already does
- Response objects are short-lived

### Webhook Performance

**Event Processing:**
- Webhooks are sent asynchronously (fire-and-forget)
- Failed webhooks with retry may queue up
- Implement max queue size (100 pending webhooks)
- Drop oldest pending webhook if queue is full

**Rate Limiting:**
- No rate limiting on webhook sending
- External services should implement their own rate limiting
- Consider adding optional rate limiting in future (e.g., max 10/second)

## Configuration

### Unified Plugin Configuration UI

**Location:** VRCX Settings → Plugins (new section)

**UI Components:**

#### Local API Settings Section
- **Enable/Disable Toggle**: Start/stop the API server
- **Port Input**: Configure listening port (default: 15342, range: 1024-65535)
- **Bind Address Input**: Configure bind address (default: 127.0.0.1)
  - Dropdown with common options: 127.0.0.1 (localhost only), 0.0.0.0 (all interfaces)
  - Warning message when selecting 0.0.0.0 about security implications
- **Status Indicator**: Shows "Running" or "Stopped" with colored badge
- **Test Button**: Opens http://localhost:{port}/api/info in browser
- **Current URL Display**: Shows the full API endpoint URL

#### Webhook Settings Section
- **Enable/Disable Toggle**: Enable/disable webhook notifications
- **Target URL Input**: Configure webhook destination URL
- **Event Type Checkboxes**:
  - Player Joined Events
  - Location Changed Events
- **Retry Configuration**:
  - Retry Count (default: 3)
  - Retry Delay (default: 1000ms)
- **Test Webhook Button**: Send test payload to configured URL
- **Statistics Display**:
  - Total Sent: {count}
  - Failed: {count}
  - Last Error: {message}
  - Reset Statistics Button

### Configuration Storage

**API Server Configuration:**
```json
{
    "localApi": {
        "enabled": true,
        "port": 15342,
        "bindAddress": "127.0.0.1",
        "requestTimeout": 5000
    }
}
```
**Storage Location:** localStorage (key: `vrcx_plugin_localapi_config`)

**Webhook Configuration:**
```json
{
    "webhook": {
        "enabled": false,
        "targetUrl": "",
        "retryCount": 3,
        "retryDelay": 1000,
        "events": {
            "playerJoined": true,
            "locationChanged": true
        }
    }
}
```
**Storage Location:** localStorage (key: `vrcx_plugin_webhook_config`)

### Configuration Validation

**Port Validation:**
- Must be integer between 1024 and 65535
- Show error message for invalid ports
- Warn if port is commonly used (e.g., 8080, 3000)

**Bind Address Validation:**
- Must be valid IPv4 address format
- Show security warning for 0.0.0.0
- Recommend 127.0.0.1 for security

**URL Validation:**
- Must be valid HTTP/HTTPS URL
- Show warning for HTTP (not HTTPS)
- Test connectivity before saving

## Future Enhancements

### Potential API Endpoints

- `/api/friends` - List all cached friends
- `/api/worlds/{worldId}` - Get cached world information
- `/api/instances/{instanceId}` - Get current instance details
- `/api/query` - Execute limited SQL queries on local database

### Potential Webhook Events

- `friend-online` - Friend comes online
- `friend-offline` - Friend goes offline
- `notification-received` - VRChat notification received
- `instance-joined` - User joins an instance
- `instance-left` - User leaves an instance

### Advanced Features

- WebSocket support for real-time streaming
- Webhook payload customization (user-defined templates)
- API authentication with local API keys
- Rate limiting configuration
- Webhook delivery confirmation and retry queue UI
