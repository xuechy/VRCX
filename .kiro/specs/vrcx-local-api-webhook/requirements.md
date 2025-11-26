# Requirements Document

## Introduction

This feature adds local HTTP API query capabilities and event-based Webhook push functionality to VRCX using a **modular plugin architecture**. The system will reuse VRCX's existing in-memory data (Vuex/Pinia stores) to avoid additional VRChat API calls. 

The architecture combines a C# backend HTTP server plugin with a JavaScript frontend Pinia plugin, enabling external applications to query cached player data and receive real-time event notifications. The design prioritizes minimal invasiveness to VRCX core code (only ~13 lines of changes), making it easy to maintain and merge with upstream updates.

## Glossary

- **VRCX**: VRChat Extended - A desktop application that enhances the VRChat experience with additional features
- **Plugin**: A self-contained module that extends VRCX functionality without modifying core code
- **IVRCXPlugin**: C# interface that defines the contract for VRCX plugins
- **PluginLoader**: C# component that dynamically loads and manages plugin lifecycle
- **Local API Server Plugin**: A C# plugin that implements HTTP server functionality
- **Webhook Plugin**: A Pinia plugin that monitors events and sends HTTP notifications
- **Pinia Plugin**: Vue.js plugin mechanism that intercepts store actions without modifying store code
- **CEF**: Chromium Embedded Framework - The browser engine used by VRCX
- **Pinia Store**: Vue.js state management library used by VRCX frontend
- **WebApi Bridge**: VRCX's existing C# bridge that allows JavaScript to make HTTP requests
- **Trust Level**: VRChat's user reputation system (e.g., "Visitor", "New User", "User", "Known User", "Trusted User", "Veteran User")
- **Game Log**: VRCX's log parsing system that monitors VRChat client logs for events
- **Conditional Compilation**: C# preprocessor directives (#if) that allow code to be included/excluded at compile time

## Requirements

### Requirement 1

**User Story:** As an external application developer, I want to query cached player information from VRCX via HTTP API, so that I can build overlay applications without making additional VRChat API calls.

#### Acceptance Criteria

1. WHEN the Local API Server starts THEN the system SHALL bind to localhost address 127.0.0.1 on port 15342
2. WHEN an external application sends a GET request to /api/info THEN the system SHALL return a JSON response containing VRCX version and service status
3. WHEN an external application sends a GET request to /api/users/{userId} THEN the system SHALL execute JavaScript in the CEF context to retrieve user data from Pinia Store
4. WHEN user data exists in the Pinia Store THEN the system SHALL return a JSON response containing user ID, display name, avatar image URL, and trust level
5. WHEN user data does not exist in the Pinia Store THEN the system SHALL return an appropriate error response with HTTP status code 404

### Requirement 2

**User Story:** As a VRCX user, I want the Local API Server to integrate seamlessly with VRCX lifecycle, so that the service starts and stops automatically with the application.

#### Acceptance Criteria

1. WHEN VRCX application starts THEN the system SHALL initialize the Local API Server and begin listening for HTTP requests
2. WHEN VRCX application exits THEN the system SHALL gracefully shutdown the Local API Server and release the listening port
3. WHEN the Local API Server encounters a port binding error THEN the system SHALL log the error and continue VRCX startup without the API service
4. WHEN the Local API Server is running THEN the system SHALL handle multiple concurrent HTTP requests without blocking the main UI thread

### Requirement 3

**User Story:** As an external service operator, I want to receive real-time event notifications via Webhook when players join instances, so that I can track player activity.

#### Acceptance Criteria

1. WHEN the Game Log parser detects a player-joined event THEN the Webhook System SHALL retrieve complete user data from the user store
2. WHEN complete user data is available THEN the Webhook System SHALL construct a JSON payload containing event type, timestamp, user ID, display name, status, and location
3. WHEN the JSON payload is constructed THEN the Webhook System SHALL send an HTTP POST request to the configured Webhook URL via the WebApi Bridge
4. WHEN the Webhook URL is not configured THEN the Webhook System SHALL skip sending the notification without errors
5. WHEN the HTTP POST request fails THEN the Webhook System SHALL log the error and optionally retry based on configuration

### Requirement 4

**User Story:** As a VRCX user, I want to configure plugin settings including Local API and Webhook options through a unified settings UI, so that I can control all plugin features without editing configuration files.

#### Acceptance Criteria

1. WHEN the user opens VRCX settings THEN the system SHALL display a plugin configuration panel with sections for Local API and Webhook settings
2. WHEN a user modifies Local API settings (enabled, port, bind address) THEN the system SHALL persist the changes and restart the API server with new configuration
3. WHEN a user modifies Webhook settings (enabled, target URL, event types) THEN the system SHALL persist the changes to localStorage immediately
4. WHEN Local API is disabled in settings THEN the system SHALL stop the HTTP server and release the listening port
5. WHEN Webhook is disabled in settings THEN the Webhook System SHALL not send any HTTP POST requests regardless of events
6. WHEN Webhook target URL is empty THEN the Webhook System SHALL treat it as disabled configuration
7. WHEN the user changes the API listening port THEN the system SHALL validate the port number is between 1024 and 65535
8. WHEN the user changes the API bind address THEN the system SHALL validate it is a valid IP address format

### Requirement 5

**User Story:** As an external application developer, I want the Local API Server to communicate with the frontend via CEF JavaScript execution, so that I can access real-time cached data without database queries.

#### Acceptance Criteria

1. WHEN the Local API Server needs user data THEN the system SHALL construct a JavaScript expression to query the Pinia Store
2. WHEN the JavaScript expression is constructed THEN the system SHALL execute it in the CEF browser context using EvaluateScriptAsync
3. WHEN the JavaScript execution completes THEN the system SHALL parse the returned JSON string into a C# object
4. WHEN the JavaScript execution fails THEN the system SHALL return an error response with HTTP status code 500
5. WHEN the JavaScript execution times out THEN the system SHALL return an error response with HTTP status code 504

### Requirement 6

**User Story:** As a VRCX user, I want the system to monitor location change events and trigger Webhooks, so that external services can track when I move between worlds.

#### Acceptance Criteria

1. WHEN the Game Log parser detects a location change event THEN the Webhook System SHALL retrieve the new location details
2. WHEN location details are available THEN the Webhook System SHALL construct a JSON payload containing event type, timestamp, world ID, and instance details
3. WHEN the JSON payload is constructed THEN the Webhook System SHALL send an HTTP POST request to the configured Webhook URL
4. WHEN multiple location changes occur rapidly THEN the Webhook System SHALL send notifications for each change without dropping events

### Requirement 7

**User Story:** As a security-conscious user, I want the Local API Server to only accept connections from localhost, so that external networks cannot access my VRCX data.

#### Acceptance Criteria

1. WHEN the Local API Server binds to a network interface THEN the system SHALL use only the 127.0.0.1 loopback address
2. WHEN an external network attempts to connect THEN the connection SHALL be rejected at the network layer
3. WHEN the Local API Server receives a request THEN the system SHALL not implement additional authentication mechanisms beyond localhost binding

### Requirement 8

**User Story:** As a VRCX maintainer, I want all new functionality implemented as plugins with minimal core code changes, so that I can easily merge upstream updates without conflicts.

#### Acceptance Criteria

1. WHEN new functionality is added THEN the system SHALL encapsulate it in independent plugin modules located in dedicated directories
2. WHEN integrating plugins with VRCX core THEN the system SHALL modify no more than 15 lines of code in existing core files
3. WHEN compiling VRCX THEN the system SHALL support conditional compilation flags to include or exclude plugin functionality
4. WHEN a plugin is disabled THEN the system SHALL function normally without the plugin's features
5. WHEN merging upstream VRCX updates THEN the plugin architecture SHALL not create merge conflicts in core files

### Requirement 9

**User Story:** As a developer maintaining a Fork of VRCX, I want to build the application via GitHub Actions without private signing certificates, so that I can test my changes in a cloud environment.

#### Acceptance Criteria

1. WHEN the GitHub Actions workflow is triggered via workflow_dispatch THEN the system SHALL execute the build process
2. WHEN the build process encounters code signing steps THEN the workflow SHALL skip these steps if signing secrets are not available
3. WHEN the build process encounters Sentry token requirements THEN the workflow SHALL skip Sentry upload steps if the token is not available
4. WHEN the build completes successfully THEN the system SHALL produce unsigned executable and zip artifacts
5. WHEN the build completes THEN the artifacts SHALL be available for download in the GitHub Actions run details page
