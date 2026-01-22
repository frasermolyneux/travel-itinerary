// Shared Trips - Local Storage Management
// Manages saved share links for anonymous users using browser localStorage

(function() {
    'use strict';

    const STORAGE_KEY = 'travel-itinerary-saved-trips';

    // Initialize on page load
    document.addEventListener('DOMContentLoaded', function() {
        // Check if we're on the share view page
        const saveTripBtn = document.getElementById('save-trip-btn');
        if (saveTripBtn) {
            setupSaveTripButton();
        }

        // Check if we're on the trips index page
        if (window.location.pathname === '/trips' || window.location.pathname === '/Trips/Index') {
            displaySavedTripsForAnonymous();
        }
    });

    // Setup the save trip button for anonymous users
    function setupSaveTripButton() {
        const saveTripBtn = document.getElementById('save-trip-btn');
        if (!saveTripBtn) return;

        // Only handle for anonymous users - authenticated users use server-side logic
        const isAuthenticated = document.querySelector('a[href*="/MicrosoftIdentity/Account/SignOut"]') !== null;
        if (isAuthenticated) return;

        saveTripBtn.addEventListener('click', function(e) {
            e.preventDefault();
            
            // Extract trip info from page
            const tripName = document.querySelector('h1')?.textContent?.trim() || 'Shared Trip';
            const pathParts = window.location.pathname.split('/');
            const tripSlug = pathParts[pathParts.length - 2];
            const shareCode = pathParts[pathParts.length - 1];

            if (!tripSlug || !shareCode) {
                console.error('[Shared Trips] Could not extract trip information from URL');
                return;
            }

            // Save to local storage
            saveTripLocally(tripSlug, shareCode, tripName);

            // Show success message
            showSuccessMessage('Trip saved locally! Sign in to save it to your account.');

            // Update button to show saved state
            saveTripBtn.outerHTML = `
                <a class="btn btn-outline-success" href="/trips">
                    <i class="bi bi-bookmark-check"></i> Saved
                </a>
            `;
        });
    }

    // Save a trip to local storage
    function saveTripLocally(tripSlug, shareCode, tripName) {
        const savedTrips = getSavedTrips();
        
        // Check if already saved
        const existing = savedTrips.find(trip => 
            trip.shareCode === shareCode && trip.tripSlug === tripSlug
        );
        
        if (existing) {
            console.log('[Shared Trips] Trip already saved');
            return;
        }

        // Add new saved trip
        savedTrips.push({
            savedLinkId: generateId(),
            tripSlug: tripSlug,
            shareCode: shareCode,
            tripName: tripName,
            savedOn: new Date().toISOString()
        });

        localStorage.setItem(STORAGE_KEY, JSON.stringify(savedTrips));
        console.log('[Shared Trips] Trip saved to local storage');
    }

    // Get saved trips from local storage
    function getSavedTrips() {
        try {
            const stored = localStorage.getItem(STORAGE_KEY);
            return stored ? JSON.parse(stored) : [];
        } catch (error) {
            console.error('[Shared Trips] Error reading from localStorage:', error);
            return [];
        }
    }

    // Display saved trips for anonymous users on the trips page
    function displaySavedTripsForAnonymous() {
        // Only for anonymous users
        const isAuthenticated = document.querySelector('a[href*="/MicrosoftIdentity/Account/SignOut"]') !== null;
        if (isAuthenticated) return;

        const savedTrips = getSavedTrips();
        if (savedTrips.length === 0) return;

        // Find the card body where we should add saved trips
        const cardBody = document.querySelector('.col-12.col-lg-7 .card-body');
        if (!cardBody) return;

        // Create the saved trips section
        const savedTripsHtml = `
            <hr class="my-4" />
            <h5 class="mb-3">Trips Shared With Me</h5>
            <div class="table-responsive">
                <table class="table align-middle">
                    <thead>
                        <tr>
                            <th scope="col">Name</th>
                            <th scope="col">Saved On</th>
                            <th scope="col" class="text-end">Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${savedTrips.map(trip => `
                            <tr class="table-light">
                                <td>
                                    <div class="fw-semibold">
                                        <i class="bi bi-share me-1"></i>${escapeHtml(trip.tripName)}
                                    </div>
                                    <div class="text-muted small">Shared trip (local)</div>
                                </td>
                                <td>
                                    ${formatDate(trip.savedOn)}
                                </td>
                                <td class="text-end">
                                    <a class="btn btn-sm btn-link" href="/shares/${trip.tripSlug}/${trip.shareCode}">View</a>
                                    <button type="button" class="btn btn-sm btn-link text-danger" 
                                            onclick="removeLocalTrip('${trip.savedLinkId}')"
                                            data-confirm="Remove this saved trip?">Remove</button>
                                </td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
            <div class="alert alert-info mt-3">
                <i class="bi bi-info-circle me-2"></i>
                <strong>Sign in</strong> to sync your saved trips across devices and access them when online.
            </div>
        `;

        cardBody.insertAdjacentHTML('beforeend', savedTripsHtml);
    }

    // Remove a trip from local storage (exposed globally for onclick handlers)
    window.removeLocalTrip = function(savedLinkId) {
        if (!confirm('Remove this saved trip?')) return;

        const savedTrips = getSavedTrips();
        const filtered = savedTrips.filter(trip => trip.savedLinkId !== savedLinkId);
        
        localStorage.setItem(STORAGE_KEY, JSON.stringify(filtered));
        console.log('[Shared Trips] Trip removed from local storage');
        
        // Reload page to refresh the list
        window.location.reload();
    };

    // Show success message
    function showSuccessMessage(message) {
        // Check if Bootstrap toast is available
        if (typeof bootstrap !== 'undefined' && bootstrap.Toast) {
            const toastHtml = `
                <div class="toast align-items-center text-white bg-success border-0" role="alert" aria-live="assertive" aria-atomic="true">
                    <div class="d-flex">
                        <div class="toast-body">
                            ${message}
                        </div>
                        <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                    </div>
                </div>
            `;
            
            let toastContainer = document.querySelector('.toast-container');
            if (!toastContainer) {
                toastContainer = document.createElement('div');
                toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
                document.body.appendChild(toastContainer);
            }
            
            toastContainer.insertAdjacentHTML('beforeend', toastHtml);
            const toastElement = toastContainer.lastElementChild;
            const toast = new bootstrap.Toast(toastElement, { autohide: true, delay: 5000 });
            toast.show();
            
            toastElement.addEventListener('hidden.bs.toast', () => {
                toastElement.remove();
            });
        } else {
            alert(message);
        }
    }

    // Helper: Generate a unique ID
    function generateId() {
        return 'local-' + Date.now() + '-' + Math.random().toString(36).substring(2, 9);
    }

    // Helper: Format date
    function formatDate(dateString) {
        try {
            const date = new Date(dateString);
            return date.toLocaleString('en-US', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit'
            });
        } catch (error) {
            return dateString;
        }
    }

    // Helper: Escape HTML
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Export for migration on login
    window.TravelItinerary = window.TravelItinerary || {};
    window.TravelItinerary.SharedTrips = {
        getSavedTrips: getSavedTrips,
        clearSavedTrips: function() {
            localStorage.removeItem(STORAGE_KEY);
        }
    };

})();
