// Copyright(c) 2019-2025 pypy, Natsumi and individual contributors.
// All rights reserved.
//
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT>.

/**
 * Simple event bus for plugin communication
 */
class EventBus {
    constructor() {
        this.events = {};
    }

    on(event, handler) {
        if (!this.events[event]) {
            this.events[event] = [];
        }
        this.events[event].push(handler);
    }

    off(event, handler) {
        if (!this.events[event]) {
            return;
        }
        const index = this.events[event].indexOf(handler);
        if (index > -1) {
            this.events[event].splice(index, 1);
        }
    }

    emit(event, data) {
        if (!this.events[event]) {
            return;
        }
        this.events[event].forEach((handler) => {
            try {
                handler(data);
            } catch (error) {
                console.error(`Error in event handler for ${event}:`, error);
            }
        });
    }

    clear(event) {
        if (event) {
            delete this.events[event];
        } else {
            this.events = {};
        }
    }
}

export const eventBus = new EventBus();
export default eventBus;
