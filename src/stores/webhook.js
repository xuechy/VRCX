// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

import { defineStore } from 'pinia';
import { reactive } from 'vue';

export const useWebhookStore = defineStore('webhook', () => {
    // Import plugin settings store for centralized config
    let pluginSettingsStore = null;
    
    const state = reactive({
        lastError: null,
        statistics: {
            sent: 0,
            failed: 0,
            lastSent: null
        }
    });

    function getPluginSettings() {
        if (!pluginSettingsStore) {
            // Lazy import to avoid circular dependency
            const { usePluginSettingsStore } = require('./settings/plugins');
            pluginSettingsStore = usePluginSettingsStore();
        }
        return pluginSettingsStore;
    }

    async function sendWebhook(eventType, data) {
        const settings = getPluginSettings();
        
        if (!settings.webhookEnabled || !settings.webhookTargetUrl) {
            return;
        }

        // Check if this event type is enabled
        const events = settings.webhookEvents;
        const eventKey = eventType.replace(/-([a-z])/g, (g) => g[1].toUpperCase()); // convert kebab-case to camelCase
        if (!events[eventKey]) {
            return;
        }

        const payload = {
            event: eventType,
            timestamp: new Date().toISOString(),
            data
        };

        try {
            const config = settings.state.config.webhook;
            
            const response = await window.WebApi.execute({
                url: settings.webhookTargetUrl,
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(payload),
                timeout: config.timeout || 5000
            });

            state.statistics.sent++;
            state.statistics.lastSent = new Date().toISOString();
            state.lastError = null;

            console.log('Webhook sent successfully:', eventType);
            return response;
        } catch (error) {
            state.statistics.failed++;
            state.lastError = {
                event: eventType,
                error: error.message,
                timestamp: new Date().toISOString()
            };
            console.error('Failed to send webhook:', error);
            throw error;
        }
    }

    return {
        state,
        statistics: () => state.statistics,
        lastError: () => state.lastError,
        sendWebhook
    };
});
