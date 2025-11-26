// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

import { useWebhookStore } from '../stores/webhook';
import { useUserStore } from '../stores/user';
import { useLocationStore } from '../stores/location';

/**
 * Pinia plugin for webhook functionality
 * Monitors gameLog store actions and triggers webhooks
 */
export function webhookPlugin({ store }) {
    // Only hook into gameLog store
    if (store.$id !== 'GameLog') {
        return;
    }

    console.log('Webhook plugin registered on GameLog store');

    const webhookStore = useWebhookStore();

    // Subscribe to all actions on the gameLog store
    store.$onAction(({ name, args, after, onError }) => {
        // We're interested in when logs are added
        if (name !== 'addGameLog') {
            return;
        }

        // Process after the action completes
        after(() => {
            try {
                const entry = args[0]; // First argument is the entry
                if (!entry || !entry.type) {
                    return;
                }

                handleGameLogEntry(entry, webhookStore);
            } catch (error) {
                console.error('Error in webhook plugin:', error);
            }
        });

        onError((error) => {
            console.error('Error in gameLog action:', error);
        });
    });
}

/**
 * Handle a game log entry and send appropriate webhook
 */
async function handleGameLogEntry(entry, webhookStore) {
    const userStore = useUserStore();
    const locationStore = useLocationStore();

    switch (entry.type) {
        case 'OnPlayerJoined':
            if (webhookStore.state.events.playerJoined) {
                const user = userStore.cachedUsers.get(entry.userId);
                await webhookStore.sendWebhook('player-joined', {
                    userId: entry.userId,
                    displayName: entry.displayName,
                    location: entry.location || locationStore.lastLocation?.location,
                    trustLevel: user?.$trustLevel || 'Unknown',
                    timestamp: entry.created_at
                });
            }
            break;

        case 'OnPlayerLeft':
            if (webhookStore.state.events.playerLeft) {
                await webhookStore.sendWebhook('player-left', {
                    userId: entry.userId,
                    displayName: entry.displayName,
                    location: entry.location || locationStore.lastLocation?.location,
                    timeSpent: entry.time,
                    timestamp: entry.created_at
                });
            }
            break;

        case 'Location':
            if (webhookStore.state.events.locationChanged) {
                await webhookStore.sendWebhook('location-changed', {
                    location: entry.location,
                    worldId: entry.worldId,
                    worldName: entry.worldName,
                    groupName: entry.groupName,
                    timestamp: entry.created_at
                });
            }
            break;

        case 'VideoPlay':
            if (webhookStore.state.events.videoPlay) {
                const user = userStore.cachedUsers.get(entry.userId);
                await webhookStore.sendWebhook('video-play', {
                    userId: entry.userId,
                    displayName: entry.displayName,
                    videoUrl: entry.videoUrl,
                    videoName: entry.videoName,
                    location: entry.location || locationStore.lastLocation?.location,
                    trustLevel: user?.$trustLevel || 'Unknown',
                    timestamp: entry.created_at
                });
            }
            break;

        case 'PortalSpawn':
            if (webhookStore.state.events.portalSpawn) {
                await webhookStore.sendWebhook('portal-spawn', {
                    userId: entry.userId,
                    displayName: entry.displayName,
                    instanceId: entry.instanceId,
                    worldName: entry.worldName,
                    location: entry.location,
                    timestamp: entry.created_at
                });
            }
            break;
    }
}

export default webhookPlugin;
