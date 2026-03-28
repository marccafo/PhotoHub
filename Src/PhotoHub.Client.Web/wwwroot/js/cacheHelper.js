window.thumbnailCache = {
    CACHE_NAME: 'photohub-thumbnails-v1',

    getCount: async function () {
        if (!('caches' in window)) return 0;
        try {
            const cache = await caches.open(this.CACHE_NAME);
            const keys = await cache.keys();
            return keys.length;
        } catch {
            return 0;
        }
    },

    clear: async function () {
        if (!('caches' in window)) return;
        await caches.delete(this.CACHE_NAME);
    }
};
