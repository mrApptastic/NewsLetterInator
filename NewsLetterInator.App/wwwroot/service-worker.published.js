self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [
    /\\.dll$/,
    /\\.pdb$/,
    /\\.wasm$/,
    /\\.html$/,
    /\\.js$/,
    /\\.json$/,
    /\\.css$/,
    /\\.png$/,
    /\\.ico$/,
    /\\.svg$/,
    /\\.woff$/,
    /\\.woff2$/
];
const offlineAssetsExclude = [
    /^service-worker\\.js$/,
    /^service-worker\\.published\\.js$/
];

async function onInstall() {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    if (event.request.method !== 'GET') {
        return fetch(event.request);
    }

    // Only app-origin static files are cached; Google API calls are cross-origin and bypass this cache.
    const response = await caches.match(event.request);
    return response || fetch(event.request);
}
