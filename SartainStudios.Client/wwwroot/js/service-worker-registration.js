(function () {
    if (!('serviceWorker' in navigator)) {
        return;
    }
    let refreshing = false;
    navigator.serviceWorker.addEventListener('controllerchange', function () {
        if (refreshing) {
            return;
        }
        refreshing = true;
        window.location.reload();
    });
    window.addEventListener('load', function () {
        navigator.serviceWorker.register('service-worker.js', {updateViaCache: 'none'}).then(function (registration) {
            registration.update();
            setInterval(function () {
                registration.update();
            }, 60 * 60 * 1000);
            document.addEventListener('visibilitychange', function () {
                if (document.visibilityState === 'visible') {
                    registration.update();
                }
            });
            if (registration.waiting && navigator.serviceWorker.controller) {
                notifyUpdateAvailable(registration);
            }
            registration.addEventListener('updatefound', function () {
                const installingWorker = registration.installing;
                if (!installingWorker) {
                    return;
                }
                installingWorker.addEventListener('statechange', function () {
                    if (installingWorker.state === 'installed' && navigator.serviceWorker.controller) {
                        notifyUpdateAvailable(registration);
                    }
                });
            });
        }).catch(function (error) {
            console.error('Service worker registration failed:', error);
        });
    });

    function notifyUpdateAvailable(registration) {
        if (document.getElementById('update-available-banner')) {
            return;
        }
        const banner = document.createElement('div');
        banner.id = 'update-available-banner';
        banner.style.cssText = 'position:fixed;bottom:16px;left:50%;transform:translateX(-50%);z-index:9999;' +
            'display:flex;align-items:center;gap:16px;padding:12px 20px;border-radius:8px;' +
            'background:#27272F;color:#fff;font-family:Roboto,Helvetica,Arial,sans-serif;font-size:14px;' +
            'box-shadow:0 4px 16px rgba(0,0,0,0.35);';
        const text = document.createElement('span');
        text.textContent = 'Update Available';
        const refreshButton = document.createElement('button');
        refreshButton.textContent = 'Refresh';
        refreshButton.style.cssText = 'background:#594AE2;color:#fff;border:none;border-radius:4px;' +
            'padding:6px 14px;font-weight:500;cursor:pointer;';
        refreshButton.onclick = function () {
            if (registration.waiting) {
                registration.waiting.postMessage({type: 'SKIP_WAITING'});
            }
            banner.remove();
        };
        const dismissButton = document.createElement('button');
        dismissButton.textContent = '✕';
        dismissButton.title = 'Dismiss';
        dismissButton.style.cssText = 'background:transparent;color:#A0A0AB;border:none;' +
            'font-size:14px;cursor:pointer;padding:0 4px;';
        dismissButton.onclick = function () {
            banner.remove();
        };
        banner.appendChild(text);
        banner.appendChild(refreshButton);
        banner.appendChild(dismissButton);
        document.body.appendChild(banner);
    }
})();