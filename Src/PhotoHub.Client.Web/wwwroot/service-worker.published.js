// Blazor PWA service worker — cache app shell, skip /api/ requests

const CACHE_NAME = 'photohub-cache-v1';

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache =>
            fetch('service-worker-assets.js')
                .then(response => response.text())
                .then(text => {
                    // The manifest is a JS file that assigns to self.assetsManifest
                    const fn = new Function(text);
                    fn();
                    const assets = self.assetsManifest.assets
                        .filter(a => a.url !== 'service-worker.js')
                        .map(a => new Request(a.url, { integrity: a.hash, cache: 'no-cache' }));
                    return cache.addAll(assets);
                })
        )
    );
    // Do NOT call self.skipWaiting() here — the app will prompt the user
    // and call it explicitly via the SKIP_WAITING message below.
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);

    // Always go to network for API calls
    if (url.pathname.startsWith('/api/')) {
        return;
    }

    // For navigation requests, serve index.html from cache (SPA routing)
    if (event.request.mode === 'navigate') {
        event.respondWith(
            caches.match('index.html').then(cached => cached || fetch(event.request))
        );
        return;
    }

    // For other requests, try cache first, then network
    event.respondWith(
        caches.match(event.request).then(cached => cached || fetch(event.request))
    );
});
