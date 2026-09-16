namespace HerYerde.Web.Controllers;

/// <summary>Service worker kaynağı; {{VERSION}} PwaController'da statik varlık özetiyle değişir.</summary>
internal static class ServiceWorkerScript
{
    public const string Source = """
        // HerYerde service worker (D16). Statik varlıklar (css, js, yazı tipi, ikon) önbellek-önce; vitrin sayfaları ağ-önce,
        // başarılı son 20 sayfa çevrimdışı yedek. Sepet, ödeme, yönetim, hesap, sipariş, çeyiz yönetimi ve beacon hiç önbelleğe
        // girmez; POST ve başka kökene giden istek geçer.
        const VERSION = "{{VERSION}}";
        const STATIC_CACHE = "heryerde-statik-" + VERSION;
        const PAGE_CACHE = "heryerde-sayfa-" + VERSION;
        const PAGE_LIMIT = 20;
        const OFFLINE_URL = "/cevrimdisi";
        const PRIVATE_PATHS = ["/sepet", "/odeme", "/admin", "/hesap", "/siparis", "/olay", "/ceyizlistesi/yonet"];
        const STATIC_PREFIXES = ["/css/", "/js/", "/fonts/", "/icon-", "/favicon", "/apple-touch-icon"];

        const isPrivate = (path) => PRIVATE_PATHS.some((prefix) => path === prefix || path.startsWith(prefix + "/"));

        // Kurulumda çevrimdışı sayfası ve onun stil/yazı tipi dosyaları alınır: bağlantı yokken sayfa markalı görünür.
        self.addEventListener("install", (event) => {
          event.waitUntil((async () => {
            const cache = await caches.open(STATIC_CACHE);
            const response = await fetch(OFFLINE_URL, { credentials: "omit" });
            const html = await response.clone().text();
            await cache.put(OFFLINE_URL, response);
            const assets = [...html.matchAll(/(?:href|src)="(\/(?:css|js|fonts)\/[^"]+)"/g)].map((match) => match[1]);
            await cache.addAll(assets);
            await self.skipWaiting();
          })());
        });

        self.addEventListener("activate", (event) => {
          event.waitUntil((async () => {
            for (const key of await caches.keys()) {
              if (key.startsWith("heryerde-") && !key.endsWith(VERSION)) {
                await caches.delete(key);
              }
            }

            await self.clients.claim();
          })());
        });

        async function rememberPage(request, response) {
          const cache = await caches.open(PAGE_CACHE);
          await cache.delete(request);
          await cache.put(request, response);
          const keys = await cache.keys();
          for (const key of keys.slice(0, Math.max(0, keys.length - PAGE_LIMIT))) {
            await cache.delete(key);
          }
        }

        self.addEventListener("fetch", (event) => {
          const request = event.request;
          if (request.method !== "GET") {
            return;
          }

          const url = new URL(request.url);
          if (url.origin !== self.location.origin || isPrivate(url.pathname)) {
            return;
          }

          if (STATIC_PREFIXES.some((prefix) => url.pathname.startsWith(prefix))) {
            event.respondWith((async () => {
              const hit = await caches.match(request);
              if (hit) {
                return hit;
              }

              const response = await fetch(request);
              if (response.ok) {
                const cache = await caches.open(STATIC_CACHE);
                await cache.put(request, response.clone());
              }

              return response;
            })());
            return;
          }

          if (request.mode === "navigate") {
            event.respondWith((async () => {
              try {
                const response = await fetch(request);
                // Yönlendirilen ya da hatalı yanıt saklanmaz (ör. sepetsiz /odeme → /sepet).
                if (response.ok && !response.redirected && response.type === "basic") {
                  event.waitUntil(rememberPage(request, response.clone()));
                }

                return response;
              } catch {
                return (await caches.match(request, { cacheName: PAGE_CACHE })) || (await caches.match(OFFLINE_URL));
              }
            })());
          }
        });
        """;
}
