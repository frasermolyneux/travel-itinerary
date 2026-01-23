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
        
        // Use Bootstrap toast for update notification
        if (typeof bootstrap !== 'undefined' && bootstrap.Toast) {
            const toastHtml = `
                <div class="toast align-items-center text-white bg-info border-0" role="alert" aria-live="assertive" aria-atomic="true">
                    <div class="d-flex">
                        <div class="toast-body">
                            ${message}
                        </div>
                        <button type="button" class="btn btn-sm btn-light me-2 my-auto" id="update-refresh-btn">Refresh</button>
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
            const toast = new bootstrap.Toast(toastElement, { autohide: false });
            toast.show();
            
            // Add click handler for refresh button
            const refreshBtn = document.getElementById('update-refresh-btn');
            if (refreshBtn) {
                refreshBtn.addEventListener('click', () => {
                    worker.postMessage({ type: 'SKIP_WAITING' });
                    window.location.reload();
                });
            }
            
            // Remove toast element after it's hidden
            toastElement.addEventListener('hidden.bs.toast', () => {
                toastElement.remove();
            });
        } else {
            // Fallback to console if Bootstrap is not available
            console.log(`[PWA] ${message}. Please refresh the page.`);
            // Auto-refresh after 5 seconds as fallback
            setTimeout(() => {
                worker.postMessage({ type: 'SKIP_WAITING' });
                window.location.reload();
            }, 5000);
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
        updateNetworkDependentUI();
    });

    window.addEventListener('offline', () => {
        console.log('[PWA] Connection lost');
        showConnectionStatus('You are offline. Some features may not be available.', 'warning');
        updateNetworkDependentUI();
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

    // Get manual offline mode state
    function isManualOfflineMode() {
        return localStorage.getItem('pwa-manual-offline') === 'true';
    }

    // Set manual offline mode
    function setManualOfflineMode(offline) {
        localStorage.setItem('pwa-manual-offline', offline ? 'true' : 'false');
        updateCacheStatus();
        // Notify service worker about the change
        notifyServiceWorkerOfflineState(offline);
        // Update UI elements that require network
        updateNetworkDependentUI();
    }

    // Check if app is effectively offline (no network OR manual offline mode)
    function isEffectivelyOffline() {
        return !navigator.onLine || isManualOfflineMode();
    }

    // Notify service worker about offline state
    function notifyServiceWorkerOfflineState(offline) {
        if (navigator.serviceWorker.controller) {
            navigator.serviceWorker.controller.postMessage({
                type: 'SET_MANUAL_OFFLINE',
                offline: offline
            });
        }
    }

    // Update UI elements that require network connectivity
    function updateNetworkDependentUI() {
        const offline = isEffectivelyOffline();
        
        // Find all elements that should be disabled when offline
        // These are marked with data-requires-network attribute
        const networkElements = document.querySelectorAll('[data-requires-network="true"]');
        
        networkElements.forEach(element => {
            if (offline) {
                // Disable the element and add visual indicator
                element.classList.add('offline-disabled');
                element.setAttribute('disabled', 'disabled');
                element.setAttribute('title', 'This feature requires an internet connection');
                
                // Add offline icon if it's a button or link
                if ((element.tagName === 'BUTTON' || element.tagName === 'A') && 
                    !element.querySelector('.offline-indicator')) {
                    const icon = document.createElement('i');
                    icon.className = 'bi bi-cloud-slash offline-indicator ms-1';
                    icon.style.fontSize = '0.875em';
                    icon.style.opacity = '0.7';
                    element.appendChild(icon);
                }
            } else {
                // Re-enable the element and remove indicators
                element.classList.remove('offline-disabled');
                element.removeAttribute('disabled');
                element.removeAttribute('title');
                
                // Remove offline icon
                const icon = element.querySelector('.offline-indicator');
                if (icon) {
                    icon.remove();
                }
            }
        });
        
        // Also update form inputs that need network (like Google Place Picker)
        const placeInputs = document.querySelectorAll('.google-place-picker');
        placeInputs.forEach(input => {
            if (offline) {
                input.setAttribute('readonly', 'readonly');
                input.setAttribute('placeholder', 'Place search unavailable offline');
                input.classList.add('offline-disabled');
            } else {
                input.removeAttribute('readonly');
                input.classList.remove('offline-disabled');
                // Restore original placeholder if stored
                const originalPlaceholder = input.getAttribute('data-original-placeholder');
                if (originalPlaceholder) {
                    input.setAttribute('placeholder', originalPlaceholder);
                }
            }
        });
    }

    // Initialize cache status UI
    function initCacheStatus() {
        // Create cache status indicator in the top-level navbar container
        const topLevelContainer = document.getElementById('pwa-cache-status-top-level');
        if (!topLevelContainer) return;

        const cacheStatusHtml = `
            <div class="dropdown" id="pwa-cache-status">
                <a class="nav-link text-dark dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                    <i class="bi bi-cloud-check" id="cache-status-icon"></i>
                    <span class="d-none d-md-inline ms-1" id="cache-status-text">Status</span>
                </a>
                <ul class="dropdown-menu dropdown-menu-end" style="min-width: 320px;">
                    <li class="px-3 py-2">
                        <div class="d-flex align-items-center justify-content-between mb-2">
                            <strong>Cache Status</strong>
                            <span class="badge bg-success" id="cache-status-badge">Online</span>
                        </div>
                        <div class="small text-muted mb-2">
                            <i class="bi bi-clock"></i>
                            Last synced: <span id="last-sync-time">Never</span>
                        </div>
                        <div class="small text-muted mb-2">
                            <i class="bi bi-file-earmark"></i>
                            Page synced: <span id="page-sync-time">Never</span>
                        </div>
                        <div class="small text-muted mb-3">
                            <i class="bi bi-database"></i>
                            Cached items: <span id="cached-items-count">0</span>
                        </div>
                        <button class="btn btn-sm btn-outline-primary w-100 mb-2" id="offline-toggle-btn">
                            <span id="offline-toggle-text"><i class="bi bi-wifi-off"></i> Go Offline</span>
                        </button>
                        <button class="btn btn-sm btn-primary w-100" id="manual-sync-btn">
                            <i class="bi bi-arrow-clockwise"></i> Sync Now
                        </button>
                    </li>
                </ul>
            </div>
        `;

        // Insert into the top-level container
        topLevelContainer.innerHTML = cacheStatusHtml;

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

        const offlineToggleBtn = document.getElementById('offline-toggle-btn');
        if (offlineToggleBtn) {
            offlineToggleBtn.addEventListener('click', handleOfflineToggle);
        }

        // Update status when online/offline changes
        window.addEventListener('online', updateCacheStatus);
        window.addEventListener('offline', updateCacheStatus);
    }

    // Handle offline toggle button click
    function handleOfflineToggle() {
        const currentState = isManualOfflineMode();
        setManualOfflineMode(!currentState);
        
        const message = !currentState 
            ? 'You are now in offline mode. The app will only use cached content.'
            : 'You are now in online mode. The app will fetch fresh content when available.';
        showConnectionStatus(message, !currentState ? 'info' : 'success');
    }

    // Update cache status display
    async function updateCacheStatus() {
        const isOnline = navigator.onLine;
        const manualOffline = isManualOfflineMode();
        const statusIcon = document.getElementById('cache-status-icon');
        const statusBadge = document.getElementById('cache-status-badge');
        const statusText = document.getElementById('cache-status-text');
        const offlineToggleBtn = document.getElementById('offline-toggle-btn');
        const offlineToggleText = document.getElementById('offline-toggle-text');
        
        // Status bar elements
        const statusLabel = document.getElementById('pwa-status-label');
        const statusLabelIcon = document.getElementById('pwa-status-icon');
        const statusLabelText = document.getElementById('pwa-status-label-text');
        
        if (!statusIcon || !statusBadge) return;

        // Update offline toggle button
        if (offlineToggleBtn && offlineToggleText) {
            if (manualOffline) {
                offlineToggleText.innerHTML = '<i class="bi bi-wifi"></i> Go Online';
            } else {
                offlineToggleText.innerHTML = '<i class="bi bi-wifi-off"></i> Go Offline';
            }
        }

        // Update online/offline status with color coding
        if (manualOffline) {
            // Manual offline mode - blue
            statusIcon.className = 'bi bi-cloud-slash';
            statusBadge.className = 'badge bg-primary';
            statusBadge.textContent = 'Manual Offline';
            if (statusText) statusText.textContent = 'Manual Offline';
            
            // Update status bar
            if (statusLabel && statusLabelIcon && statusLabelText) {
                statusLabel.className = 'badge bg-primary';
                statusLabelIcon.className = 'bi bi-wifi-off';
                statusLabelText.textContent = 'Manual Offline';
            }
        } else if (isOnline) {
            // Online - green
            statusIcon.className = 'bi bi-cloud-check';
            statusBadge.className = 'badge bg-success';
            statusBadge.textContent = 'Online';
            if (statusText) statusText.textContent = 'Online';
            
            // Update status bar
            if (statusLabel && statusLabelIcon && statusLabelText) {
                statusLabel.className = 'badge bg-success';
                statusLabelIcon.className = 'bi bi-wifi';
                statusLabelText.textContent = 'Online';
            }
        } else {
            // Network offline - red
            statusIcon.className = 'bi bi-cloud-slash';
            statusBadge.className = 'badge bg-danger';
            statusBadge.textContent = 'Offline';
            if (statusText) statusText.textContent = 'Offline';
            
            // Update status bar
            if (statusLabel && statusLabelIcon && statusLabelText) {
                statusLabel.className = 'badge bg-danger';
                statusLabelIcon.className = 'bi bi-wifi-off';
                statusLabelText.textContent = 'Offline';
            }
        }

        // Update cached items count
        await updateCachedItemsCount();
        
        // Update last sync time from localStorage
        updateLastSyncTime();
        
        // Update page sync time
        updatePageSyncTime();
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

    // Update page-specific sync time display
    function updatePageSyncTime() {
        const pageSyncElement = document.getElementById('page-sync-time');
        if (!pageSyncElement) return;

        const pageKey = getPageKey();
        const pageSync = localStorage.getItem(`pwa-page-sync-${pageKey}`);
        
        if (pageSync) {
            const syncDate = new Date(pageSync);
            const now = new Date();
            const diffMinutes = Math.floor((now - syncDate) / 60000);
            
            if (diffMinutes < 1) {
                pageSyncElement.textContent = 'Just now';
            } else if (diffMinutes < 60) {
                pageSyncElement.textContent = `${diffMinutes} min ago`;
            } else if (diffMinutes < 1440) {
                const hours = Math.floor(diffMinutes / 60);
                pageSyncElement.textContent = `${hours} hour${hours > 1 ? 's' : ''} ago`;
            } else {
                const days = Math.floor(diffMinutes / 1440);
                pageSyncElement.textContent = `${days} day${days > 1 ? 's' : ''} ago`;
            }
        } else {
            pageSyncElement.textContent = 'Never';
        }
    }

    // Get a unique key for the current page
    function getPageKey() {
        // Use pathname as the key, normalizing it
        let path = window.location.pathname;
        // Remove trailing slash
        if (path.endsWith('/') && path.length > 1) {
            path = path.slice(0, -1);
        }
        // Default to 'home' for root path
        if (path === '/' || path === '') {
            path = 'home';
        }
        return path;
    }

    // Update page sync time when page is loaded
    function updatePageSyncOnLoad() {
        // Only update if we're online and not in manual offline mode
        // Check if the page was likely fetched from network (not cache)
        // This is a best-effort detection - transferSize > 0 suggests network fetch
        const performanceEntries = performance.getEntriesByType('navigation');
        const likelyFromNetwork = performanceEntries.length > 0 && 
                                  performanceEntries[0].transferSize > 0;
        
        // Only update sync time if we're online and likely fetched from network
        if (navigator.onLine && !isManualOfflineMode() && likelyFromNetwork) {
            const pageKey = getPageKey();
            localStorage.setItem(`pwa-page-sync-${pageKey}`, new Date().toISOString());
            updatePageSyncTime();
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

            if (isManualOfflineMode()) {
                showConnectionStatus('Cannot sync in manual offline mode. Switch to online first.', 'warning');
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
            
            // Also update page sync time
            const pageKey = getPageKey();
            localStorage.setItem(`pwa-page-sync-${pageKey}`, new Date().toISOString());

            // Reload current page to get fresh data
            showConnectionStatus('Sync completed successfully', 'success');
            
            // Update UI
            await updateCacheStatus();
            
            // Brief delay before reload to show success message
            const RELOAD_DELAY_MS = 1000;
            setTimeout(() => {
                window.location.reload();
            }, RELOAD_DELAY_MS);

        } catch (error) {
            console.error('[PWA] Sync failed:', error);
            showConnectionStatus(`Sync failed: ${error.message || 'Unknown error'}`, 'danger');
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
        document.addEventListener('DOMContentLoaded', updatePageSyncOnLoad);
        document.addEventListener('DOMContentLoaded', updateNetworkDependentUI);
        document.addEventListener('DOMContentLoaded', () => {
            // Sync manual offline state with service worker on load
            if (navigator.serviceWorker.controller) {
                notifyServiceWorkerOfflineState(isManualOfflineMode());
            }
        });
    } else {
        initCacheStatus();
        updatePageSyncOnLoad();
        updateNetworkDependentUI();
        // Sync manual offline state with service worker on load
        if (navigator.serviceWorker.controller) {
            notifyServiceWorkerOfflineState(isManualOfflineMode());
        }
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
