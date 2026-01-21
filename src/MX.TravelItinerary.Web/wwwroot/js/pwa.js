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

    // ========================================
    // Cache Status and Manual Sync Features
    // ========================================

    // Initialize cache status UI
    function initCacheStatus() {
        // Create cache status indicator in the navbar
        const navbar = document.querySelector('.navbar-nav.ms-auto');
        if (!navbar) return;

        const cacheStatusHtml = `
            <li class="nav-item dropdown" id="pwa-cache-status">
                <a class="nav-link dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                    <i class="bi bi-cloud-check" id="cache-status-icon"></i>
                    <span class="d-none d-md-inline ms-1">Offline</span>
                </a>
                <ul class="dropdown-menu dropdown-menu-end" style="min-width: 300px;">
                    <li class="px-3 py-2">
                        <div class="d-flex align-items-center justify-content-between mb-2">
                            <strong>Cache Status</strong>
                            <span class="badge bg-success" id="cache-status-badge">Online</span>
                        </div>
                        <div class="small text-muted mb-2">
                            <i class="bi bi-clock"></i>
                            Last synced: <span id="last-sync-time">Never</span>
                        </div>
                        <div class="small text-muted mb-3">
                            <i class="bi bi-database"></i>
                            Cached items: <span id="cached-items-count">0</span>
                        </div>
                        <button class="btn btn-sm btn-primary w-100" id="manual-sync-btn">
                            <i class="bi bi-arrow-clockwise"></i> Sync Now
                        </button>
                    </li>
                </ul>
            </li>
        `;

        // Insert before the user name or sign in link
        const firstNavItem = navbar.querySelector('li');
        if (firstNavItem) {
            firstNavItem.insertAdjacentHTML('beforebegin', cacheStatusHtml);
        }

        // Set up event listeners
        setupCacheStatusListeners();
        updateCacheStatus();
        
        // Update cache status every 30 seconds
        setInterval(updateCacheStatus, 30000);
    }

    // Set up event listeners for cache status features
    function setupCacheStatusListeners() {
        const syncButton = document.getElementById('manual-sync-btn');
        if (syncButton) {
            syncButton.addEventListener('click', handleManualSync);
        }

        // Update status when online/offline changes
        window.addEventListener('online', updateCacheStatus);
        window.addEventListener('offline', updateCacheStatus);
    }

    // Update cache status display
    async function updateCacheStatus() {
        const isOnline = navigator.onLine;
        const statusIcon = document.getElementById('cache-status-icon');
        const statusBadge = document.getElementById('cache-status-badge');
        
        if (!statusIcon || !statusBadge) return;

        // Update online/offline status
        if (isOnline) {
            statusIcon.className = 'bi bi-cloud-check';
            statusBadge.className = 'badge bg-success';
            statusBadge.textContent = 'Online';
        } else {
            statusIcon.className = 'bi bi-cloud-slash';
            statusBadge.className = 'badge bg-warning';
            statusBadge.textContent = 'Offline';
        }

        // Update cached items count
        await updateCachedItemsCount();
        
        // Update last sync time from localStorage
        updateLastSyncTime();
    }

    // Count cached items
    async function updateCachedItemsCount() {
        const countElement = document.getElementById('cached-items-count');
        if (!countElement) return;

        try {
            if ('caches' in window) {
                const cacheNames = await caches.keys();
                let totalItems = 0;
                
                for (const cacheName of cacheNames) {
                    const cache = await caches.open(cacheName);
                    const keys = await cache.keys();
                    totalItems += keys.length;
                }
                
                countElement.textContent = totalItems;
            }
        } catch (error) {
            console.error('[PWA] Error counting cached items:', error);
            countElement.textContent = '?';
        }
    }

    // Update last sync time display
    function updateLastSyncTime() {
        const lastSyncElement = document.getElementById('last-sync-time');
        if (!lastSyncElement) return;

        const lastSync = localStorage.getItem('pwa-last-sync');
        if (lastSync) {
            const syncDate = new Date(lastSync);
            const now = new Date();
            const diffMinutes = Math.floor((now - syncDate) / 60000);
            
            if (diffMinutes < 1) {
                lastSyncElement.textContent = 'Just now';
            } else if (diffMinutes < 60) {
                lastSyncElement.textContent = `${diffMinutes} min ago`;
            } else if (diffMinutes < 1440) {
                const hours = Math.floor(diffMinutes / 60);
                lastSyncElement.textContent = `${hours} hour${hours > 1 ? 's' : ''} ago`;
            } else {
                const days = Math.floor(diffMinutes / 1440);
                lastSyncElement.textContent = `${days} day${days > 1 ? 's' : ''} ago`;
            }
        } else {
            lastSyncElement.textContent = 'Never';
        }
    }

    // Handle manual sync
    async function handleManualSync() {
        const syncButton = document.getElementById('manual-sync-btn');
        if (!syncButton) return;

        // Disable button and show loading state
        syncButton.disabled = true;
        const originalHtml = syncButton.innerHTML;
        syncButton.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Syncing...';

        try {
            if (!navigator.onLine) {
                showConnectionStatus('Cannot sync while offline', 'warning');
                return;
            }

            // Clear dynamic cache to force fresh data
            const cacheNames = await caches.keys();
            for (const cacheName of cacheNames) {
                if (cacheName.includes('dynamic')) {
                    await caches.delete(cacheName);
                    console.log('[PWA] Cleared cache:', cacheName);
                }
            }

            // Update service worker
            const registration = await navigator.serviceWorker.getRegistration();
            if (registration) {
                await registration.update();
            }

            // Store sync timestamp
            localStorage.setItem('pwa-last-sync', new Date().toISOString());

            // Reload current page to get fresh data
            showConnectionStatus('Sync completed successfully', 'success');
            
            // Update UI
            await updateCacheStatus();
            
            // Reload page after a short delay
            setTimeout(() => {
                window.location.reload();
            }, 1000);

        } catch (error) {
            console.error('[PWA] Sync failed:', error);
            showConnectionStatus('Sync failed. Please try again.', 'danger');
        } finally {
            // Re-enable button
            syncButton.disabled = false;
            syncButton.innerHTML = originalHtml;
        }
    }

    // Mark page as cached when loaded via service worker
    if (navigator.serviceWorker.controller) {
        // Page was served by service worker (likely from cache)
        document.addEventListener('DOMContentLoaded', () => {
            // Add a subtle indicator that this page is cached
            const pageIndicator = document.createElement('div');
            pageIndicator.id = 'cached-page-indicator';
            pageIndicator.className = 'd-none'; // Hidden by default, can be shown if needed
            pageIndicator.innerHTML = '<i class="bi bi-cloud-download"></i> Viewing cached content';
            document.body.appendChild(pageIndicator);
        });
    }

    // Initialize cache status UI when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCacheStatus);
    } else {
        initCacheStatus();
    }

    // Auto-sync when coming back online
    window.addEventListener('online', () => {
        console.log('[PWA] Back online, checking for updates...');
        // Automatically update service worker when connection restored
        navigator.serviceWorker.getRegistration().then(registration => {
            if (registration) {
                registration.update();
            }
        });
    });

})();
