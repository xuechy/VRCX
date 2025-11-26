<template>
    <div>
        <!--//- Plugins | Local API-->
        <div class="options-container" style="margin-top: 0">
            <span class="header">Local API</span>
            <div class="options-container-item" style="margin-top: 10px">
                <el-alert
                    title="功能开发中"
                    type="info"
                    description="Local API 和 Webhook 功能正在开发中。后端代码已实现，但需要添加 C# AppApi 接口支持。"
                    :closable="false"
                    show-icon />
            </div>
            <simple-switch
                label="Enable Local API Server"
                :value="false"
                :disabled="true"
                @change="() => {}" />
            
            <div class="options-container-item">
                <span class="name">Port</span>
                <el-input-number
                    :model-value="15342"
                    :min="1024"
                    :max="65535"
                    :disabled="true"
                    size="small"
                    style="width: 150px; margin-left: 10px" />
            </div>

            <div class="options-container-item">
                <el-button
                    size="small"
                    :disabled="true"
                    @click="() => {}">
                    Test API Connection
                </el-button>
            </div>
        </div>

        <!--//- Plugins | Webhook-->
        <div class="options-container">
            <span class="header">Webhook</span>
            <simple-switch
                label="Enable Webhook"
                :value="false"
                :disabled="true"
                @change="() => {}" />
            
            <div class="options-container-item">
                <span class="name">Target URL</span>
                <el-input
                    model-value=""
                    placeholder="https://your-webhook-url.com/endpoint"
                    :disabled="true"
                    size="small"
                    style="width: 400px; margin-top: 8px" />
            </div>

            <div class="options-container-item">
                <span class="name">Events</span>
                <div style="margin-top: 8px">
                    <el-checkbox :model-value="true" :disabled="true">
                        Player Joined
                    </el-checkbox>
                    <el-checkbox :model-value="true" :disabled="true">
                        Player Left
                    </el-checkbox>
                    <el-checkbox :model-value="true" :disabled="true">
                        Location Changed
                    </el-checkbox>
                    <el-checkbox :model-value="false" :disabled="true">
                        Video Play
                    </el-checkbox>
                    <el-checkbox :model-value="false" :disabled="true">
                        Portal Spawn
                    </el-checkbox>
                </div>
            </div>

            <div class="options-container-item">
                <el-button
                    size="small"
                    :disabled="true"
                    @click="() => {}">
                    Test Webhook
                </el-button>
            </div>
        </div>
    </div>
</template>

<script setup>
import SimpleSwitch from './SimpleSwitch.vue';
</script>
