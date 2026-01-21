// Service Worker Registration
// Registers the service worker for offline support

(function() {
    'use strict';

    // Check if service workers are supported
    if (!('serviceWorker' in navigator)) {
        console.log('Service workers are not supported in this browser');
        return;
    }

    // Register the service worker
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/sw.js')
            .then((registration) => {
                console.log('[PWA] Service worker registered successfully:', registration.scope);

                // Check for updates periodically
                registration.addEventListener('updatefound', () => {
                    const newWorker = registration.installing;
                    console.log('[PWA] New service worker found, installing...');

                    newWorker.addEventListener('statechange', () => {
                        if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                            // New service worker available, notify user
                            showUpdateNotification(newWorker);
                        }
                    });
                });

                // Check for updates every hour
                setInterval(() => {
                    registration.update();
                }, 60 * 60 * 1000);
            })
            .catch((error) => {
                console.error('[PWA] Service worker registration failed:', error);
            });
    });

    // Show notification when an update is available
    function showUpdateNotification(worker) {
        const message = 'A new version is available. Click to refresh.';
        
        // Check if the app has a notification area, otherwise use confirm dialog
        if (confirm(message)) {
            worker.postMessage({ type: 'SKIP_WAITING' });
            window.location.reload();
        }
    }

    // Handle service worker controller change (new SW activated)
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        console.log('[PWA] Service worker controller changed, reloading page');
        window.location.reload();
    });

    // Listen for online/offline events to notify the user
    window.addEventListener('online', () => {
        console.log('[PWA] Connection restored');
        showConnectionStatus('You are back online', 'success');
    });

    window.addEventListener('offline', () => {
        console.log('[PWA] Connection lost');
        showConnectionStatus('You are offline. Some features may not be available.', 'warning');
    });

    // Show connection status notification
    function showConnectionStatus(message, type) {
        // Check if Bootstrap toast is available
        if (typeof bootstrap !== 'undefined' && bootstrap.Toast) {
            // Create a toast notification
            const toastHtml = `
                <div class="toast align-items-center text-white bg-${type === 'success' ? 'success' : 'warning'} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                    <div class="d-flex">
                        <div class="toast-body">
                            ${message}
                        </div>
                        <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                    </div>
                </div>
            `;
            
            // Find or create toast container
            let toastContainer = document.querySelector('.toast-container');
            if (!toastContainer) {
                toastContainer = document.createElement('div');
                toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
                document.body.appendChild(toastContainer);
            }
            
            // Add toast to container
            toastContainer.insertAdjacentHTML('beforeend', toastHtml);
            const toastElement = toastContainer.lastElementChild;
            const toast = new bootstrap.Toast(toastElement, { autohide: true, delay: 5000 });
            toast.show();
            
            // Remove toast element after it's hidden
            toastElement.addEventListener('hidden.bs.toast', () => {
                toastElement.remove();
            });
        } else {
            // Fallback to console if Bootstrap is not available
            console.log(`[PWA] ${message}`);
        }
    }

    // Display install prompt for PWA
    let deferredPrompt;
    window.addEventListener('beforeinstallprompt', (e) => {
        // Prevent the mini-infobar from appearing on mobile
        e.preventDefault();
        deferredPrompt = e;
        console.log('[PWA] Install prompt available');
        
        // Show custom install button if it exists
        const installButton = document.getElementById('pwa-install-button');
        if (installButton) {
            installButton.style.display = 'block';
            installButton.addEventListener('click', () => {
                deferredPrompt.prompt();
                deferredPrompt.userChoice.then((choiceResult) => {
                    if (choiceResult.outcome === 'accepted') {
                        console.log('[PWA] User accepted the install prompt');
                    }
                    deferredPrompt = null;
                    installButton.style.display = 'none';
                });
            });
        }
    });

    // Log when PWA is installed
    window.addEventListener('appinstalled', () => {
        console.log('[PWA] App installed successfully');
        deferredPrompt = null;
    });

})();
