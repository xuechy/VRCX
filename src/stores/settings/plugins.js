// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

import { defineStore } from 'pinia';
import { computed, reactive } from 'vue';
import { ElMessage } from 'element-plus';

export const usePluginSettingsStore = defineStore('PluginSettings',() => {
    const state = reactive({
        config: null,
        loading: false
    });

    // Computed getters for easy access
    const localApiEnabled = computed({
        get: () => state.config?.plugins?.localApiPlugin?.enabled ?? true,
        set: async (value) => {
            if (!state.config) return;
            state.config.plugins.localApiPlugin.enabled = value;
            await saveConfig();
        }
    });

    const localApiPort = computed({
        get: () => state.config?.plugins?.localApiPlugin?.port ?? 15342,
        set: async (value) => {
            if (!state.config) return;
            state.config.plugins.localApiPlugin.port = value;

            await saveConfig();
        }
    });

    const webhookEnabled = computed({
        get: () => state.config?.webhook?.enabled ?? false,
        set: async (value) => {
            if (!state.config) return;
            state.config.webhook.enabled = value;
            await saveConfig();
        }
    });

    const webhookTargetUrl = computed({
        get: () => state.config?.webhook?.targetUrl ?? '',
        set: async (value) => {
            if (!state.config) return;
            state.config.webhook.targetUrl = value;
            await saveConfig();
        }
    });

    const webhookEvents = computed(() => state.config?.webhook?.events ?? {
        playerJoined: true,
        playerLeft: true,
        locationChanged: true,
        videoPlay: false,
        portalSpawn: false
    });

    async function init() {
        try {
            state.loading = true;
            const configJson = await AppApi.GetPluginConfig();
            state.config = JSON.parse(configJson);
            console.log('Plugin config loaded:', state.config);
        } catch (error) {
            console.error('Failed to load plugin config:', error);
            ElMessage.error('Failed to load plugin configuration');
        } finally {
            state.loading = false;
        }
    }

    async function saveConfig() {
        try {
            const configJson = JSON.stringify(state.config, null, 2);
            const success = await AppApi.SetPluginConfig(configJson);
            if (success) {
                console.log('Plugin config saved');
                return true;
            } else {
                ElMessage.error('Failed to save plugin configuration');
                return false;
            }
        } catch (error) {
            console.error('Error saving plugin config:', error);
            ElMessage.error('Error saving plugin configuration');
            return false;
        }
    }

    async function setWebhookEventEnabled(eventType, enabled) {
        if (!state.config?.webhook?.events) return;
        state.config.webhook.events[eventType] = enabled;
        await saveConfig();
    }

    async function testLocalApi() {
        try {
            const port = localApiPort.value;
            const result = await AppApi.TestLocalApi(port);
            const response = JSON.parse(result);
            
            if (response.error) {
                ElMessage.error(`API Test Failed: ${response.error}`);
                return false;
            } else {
                ElMessage.success(`API Test Successful! Version: ${response.version || 'Unknown'}`);
                return true;
            }
        } catch (error) {
            console.error('Error testing API:', error);
            ElMessage.error(`API Test Error: ${error.message}`);
            return false;
        }
    }

    async function testWebhook() {
        try {
            const url = webhookTargetUrl.value;
            if (!url) {
                ElMessage.warning('Please enter a webhook URL');
                return false;
            }

            // Send a test payload
            const testPayload = {
                event: 'test',
                timestamp: new Date().toISOString(),
                data: {
                    message: 'This is a test webhook from VRCX'
                }
            };

            const response = await window.WebApi.execute({
                url,
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(testPayload),
                timeout: state.config?.webhook?.timeout || 5000
            });

            ElMessage.success('Webhook test sent successfully!');
            return true;
        } catch (error) {
            console.error('Error testing webhook:', error);
            ElMessage.error(`Webhook Test Failed: ${error.message}`);
            return false;
        }
    }

    // Initialize on store creation
    init();

    return {
        state,
        localApiEnabled,
        localApiPort,
        webhookEnabled,
        webhookTargetUrl,
        webhookEvents,
        setWebhookEventEnabled,
        testLocalApi,
        testWebhook,
        init,
        saveConfig
    };
});
