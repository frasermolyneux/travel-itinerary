# Offline Support - Progressive Web App (PWA)

## Overview

The Travel Itinerary application has been enhanced with Progressive Web App (PWA) capabilities to provide offline support. This allows users to access their travel itineraries even without an internet connection.

## Features

### What Works Offline

✅ **View Previously Loaded Trips** - Users can browse trips they've already loaded while online
✅ **Browse Cached Itinerary Details** - Itinerary entries, timelines, and details are available offline
✅ **Review Booking Information** - Booking details remain accessible without connectivity
✅ **Static Assets** - CSS, JavaScript, and images are cached for fast loading
✅ **Cache Status Indicator** - Visual indicator showing online/offline status and cached content
✅ **Manual Sync** - Users can trigger a sync to refresh cached content

### What Requires Internet Connection

❌ **Create or Edit Trips** - Creating new trips or editing existing ones requires an online connection
❌ **Add New Bookings** - Adding or modifying booking information needs internet access
❌ **Share Itineraries** - Generating and managing share links requires connectivity
❌ **Load New Data** - Fetching data that hasn't been cached requires internet access
❌ **Initial Authentication** - Sign in operations require online connectivity (see Authentication section below)

## Cache Status & Sync Features

### Cache Status Indicator

A dropdown menu in the navbar provides real-time information about the cache:

- **Online/Offline Badge** - Shows current connectivity status with color-coded badge:
  - **Green**: Online (normal operation)
  - **Blue**: Manual Offline (user has chosen to go offline)
  - **Red**: Offline (network unavailable)
- **Status Bar** - Centered label in the navbar showing current connection state
- **Last Sync Timestamp** - Displays when content was last synchronized globally (e.g., "5 min ago", "Just now")
- **Page Sync Timestamp** - Shows when the current page was last synchronized
- **Cached Items Count** - Shows the number of items currently cached for offline access
- **Manual Offline Toggle** - "Go Offline"/"Go Online" button to control connectivity mode
- **Manual Sync Button** - Allows users to manually refresh cached content

### Manual Offline Mode

The app includes a manual offline mode that allows users to control network usage:

1. Click the cache status icon in the navbar (cloud icon)
2. Click "Go Offline" button in the dropdown
3. The app will:
   - Switch to manual offline mode (blue status indicator)
   - Only serve content from cache
   - Block all network requests
   - Prevent automatic syncing
4. To return to online mode:
   - Click the same icon and select "Go Online"
   - App will resume normal network operations

**Benefits:**
- Prevents slow loading on poor connections
- Preserves cache from failed requests
- Saves data usage
- Provides predictable performance

### Using Manual Sync

1. Click the cache status icon in the navbar (cloud icon with checkmark)
2. Click "Sync Now" button in the dropdown
3. The app will:
   - Clear dynamic cache to fetch fresh data
   - Update the service worker
   - Reload the current page with new content
   - Update both global and page-specific sync timestamps

**Note:** Manual sync requires an internet connection and online mode. If offline or in manual offline mode, the button will show a warning.

### Page-Specific Sync Times

Each page tracks its own last sync time separately from the global sync time:

- **Global Sync**: Shows when you last clicked "Sync Now" (affects all cached content)
- **Page Sync**: Shows when the current page was last loaded while online
- Both timestamps are displayed in the cache status dropdown
- Helps identify if you're viewing stale data on a specific page

### Automatic Sync

The app automatically syncs when:
- Connection is restored after being offline (unless in manual offline mode)
- User manually triggers sync via the "Sync Now" button
- Service worker detects updates (checked hourly)

## Authentication & Session Management

### Extended Session Timeout

The app is configured with extended authentication sessions to improve offline capability:

- **Session Duration**: 7 days
- **Sliding Expiration**: Enabled - session extends automatically with each request
- **Offline Access**: Once authenticated, users remain logged in for up to 7 days, even when offline

### How It Works

1. **Initial Sign-In** - Requires internet connection to authenticate with Microsoft Entra ID
2. **Session Cookie** - A secure authentication cookie is stored in the browser for 7 days
3. **Sliding Window** - Each time you use the app, the session is extended (sliding window of ~3.5 days)
4. **Offline Usage** - You can access the app offline without re-authenticating for up to 7 days
5. **Automatic Extension** - Active users will stay logged in indefinitely as long as they use the app regularly

### What This Means for Users

✅ **No unexpected logouts** - You won't be logged out when returning to the app after days or weeks
✅ **Works offline** - Access your trips offline without authentication issues
✅ **Security maintained** - Sessions still expire after 7 days of complete inactivity
✅ **Seamless experience** - No need to re-authenticate frequently

### Manual Sign-Out

Users can explicitly sign out at any time via the "Sign out" link in the navbar. This immediately invalidates the session cookie.

## Technical Implementation

### Architecture

The PWA implementation uses the following technologies:

1. **Service Worker** (`/wwwroot/sw.js`) - Intercepts network requests and implements caching strategies
2. **Web App Manifest** (`/wwwroot/manifest.json`) - Defines PWA metadata for installability
3. **PWA JavaScript** (`/wwwroot/js/pwa.js`) - Handles service worker registration, cache status, and user notifications
4. **Offline Page** (`/wwwroot/offline.html`) - Fallback page when content isn't cached

### Caching Strategies

#### 1. Cache-First (Static Assets)
Used for CSS, JavaScript, images, fonts, and other static resources.

```
User Request → Check Cache → Return Cached → [Cache Miss] → Network → Update Cache → Return
```

**Files:**
- `/lib/bootstrap/**` - Bootstrap CSS/JS
- `/lib/jquery/**` - jQuery library
- `/css/site.css` - Custom styles
- `/js/site.js` - Application JavaScript
- `/favicon.ico` - Favicon

#### 2. Network-First (Dynamic Content)
Used for HTML pages and API responses to ensure fresh data when online.

```
User Request → Network → Update Cache → Return → [Network Fail] → Check Cache → Return Cached
```

**Benefits:**
- Always gets latest data when online
- Falls back to cached version when offline
- Provides seamless offline experience

### Service Worker Lifecycle

1. **Install** - Caches critical static assets on first load
2. **Activate** - Cleans up old caches from previous versions
3. **Fetch** - Intercepts all network requests and applies caching strategy
4. **Update** - Automatically checks for updates hourly

### User Experience

#### Online/Offline Notifications
The app displays Bootstrap toast notifications when connectivity changes:
- **Online**: "You are back online" (green/success)
- **Offline**: "You are offline. Some features may not be available." (yellow/warning)

#### App Install Prompt
Users can install the PWA to their device:
- Triggered automatically by the browser when criteria are met
- Can be triggered manually via custom install button (if implemented)
- Provides app-like experience with home screen icon

#### Offline Fallback
When completely offline with no cached content, users see `/offline.html` which:
- Explains the offline state
- Lists what works offline vs online
- Provides a "Try Again" button to reload

## Setup and Configuration

### Browser Requirements

Service Workers require HTTPS (or localhost for development). Supported browsers:
- Chrome/Edge 45+
- Firefox 44+
- Safari 11.1+
- Opera 32+

### Development

The PWA works in development mode (localhost) without HTTPS. The service worker will register automatically.

To test offline mode:
1. Open DevTools → Application → Service Workers
2. Check "Offline" to simulate no connection
3. Navigate the app to see cached content

### Production Deployment

Ensure your hosting environment:
- ✅ Serves over HTTPS
- ✅ Has proper MIME types for `.json` files
- ✅ Allows service worker registration
- ✅ Doesn't block `/sw.js` or `/manifest.json`

### Azure App Service Configuration

The application is deployed to Azure App Service. No special configuration is needed as:
- HTTPS is enabled by default
- Static files are served correctly
- Service Worker files are accessible

## Customization

### Adding More Offline Pages

To cache additional pages for offline access, edit `/wwwroot/sw.js`:

```javascript
const STATIC_ASSETS = [
  '/',
  '/offline.html',
  '/trips',  // Add page here
  // ... other assets
];
```

### Adjusting Cache Duration

The service worker caches are versioned. To force a cache refresh:

1. Update `CACHE_VERSION` in `/wwwroot/sw.js`:
```javascript
const CACHE_VERSION = 'v2'; // Increment version
```

2. Deploy the updated service worker
3. Users will automatically get the new cache on next visit

### Custom PWA Icons

The application includes custom PWA icons for proper mobile installation:

**Included Icons:**
- `icon-192.png` (192x192px) - For home screen and app launcher
- `icon-512.png` (512x512px) - For splash screens and high-res displays
- `favicon.ico` (48x48px) - For browser tabs

The icons feature a white airplane design on a blue background (matching the app's theme color #0d6efd), providing a recognizable travel-themed appearance.

**Icon Design:**
- Blue background (#0d6efd) matching theme color
- White airplane silhouette for instant recognition
- Optimized PNG format for fast loading
- "any maskable" purpose for adaptive display across platforms

To customize the icons further:
1. Replace `icon-192.png` and `icon-512.png` in `/wwwroot/`
2. Maintain the same sizes and PNG format
3. Consider using a tool like [PWA Asset Generator](https://github.com/elegantapp/pwa-asset-generator) for comprehensive icon sets

## Testing

### Manual Testing

1. **First Load (Online)**
   - Open the app in a browser
   - Check DevTools → Application → Service Workers to see registration
   - Verify "Service worker registered successfully" in console

2. **Offline Access**
   - Load a few pages (trips, itinerary details)
   - Open DevTools → Application → Service Workers
   - Check "Offline" checkbox
   - Navigate to previously loaded pages
   - Should see cached content

3. **Cache Inspection**
   - DevTools → Application → Cache Storage
   - Verify `static-v1` and `dynamic-v1` caches exist
   - Check cached resources

### Automated Testing

Service Worker testing can be added using tools like:
- **Workbox** - Google's PWA testing library
- **Puppeteer** - Headless browser testing with offline mode
- **Lighthouse** - PWA audit scores

## Monitoring

### Service Worker Status

Check service worker registration status:
```javascript
navigator.serviceWorker.getRegistration().then(reg => {
  console.log('SW registered:', reg);
});
```

### Cache Size

Monitor cache size in DevTools → Application → Cache Storage, or programmatically:
```javascript
caches.keys().then(names => {
  names.forEach(name => {
    caches.open(name).then(cache => {
      cache.keys().then(keys => {
        console.log(`${name}: ${keys.length} items`);
      });
    });
  });
});
```

### Application Insights

The app uses Application Insights for telemetry. Service worker events can be tracked by adding custom telemetry in `/wwwroot/js/pwa.js`.

## Troubleshooting

### Service Worker Not Registering

1. Check browser console for errors
2. Verify HTTPS is being used (or localhost for dev)
3. Check that `/sw.js` is accessible (navigate to it directly)
4. Clear browser cache and try again

### Offline Content Not Available

1. Verify the page was loaded at least once while online
2. Check DevTools → Application → Cache Storage for cached resources
3. Confirm service worker is active in DevTools → Application → Service Workers
4. Check service worker fetch event handling in DevTools → Network tab

### Old Cache Not Updating

1. Increment `CACHE_VERSION` in `/wwwroot/sw.js`
2. Unregister the service worker in DevTools → Application → Service Workers
3. Clear cache storage manually
4. Hard refresh (Ctrl+Shift+R / Cmd+Shift+R)

### POST/PUT Requests Failing Offline

This is expected behavior. Write operations require an online connection:
- Show user-friendly error messages
- Implement optimistic UI updates (optional)
- Queue requests to retry when online (advanced feature)

## Future Enhancements

Potential improvements for offline support:

1. **IndexedDB for Offline Data** - Store full trip data in IndexedDB for richer offline access
2. **Background Sync** - Queue edit operations while offline and sync when connection returns
3. **Offline Editing** - Allow editing with local storage and conflict resolution
4. **Push Notifications** - Notify users of trip updates even when app is closed
5. **Advanced Caching** - Predictive caching based on user behavior
6. **Offline Analytics** - Track offline usage patterns

## Resources

- [Progressive Web Apps (MDN)](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps)
- [Service Worker API (MDN)](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API)
- [Web App Manifest (MDN)](https://developer.mozilla.org/en-US/docs/Web/Manifest)
- [Workbox (Google)](https://developers.google.com/web/tools/workbox)
- [PWA Builder](https://www.pwabuilder.com/)

## License

This PWA implementation follows the same license as the main Travel Itinerary application.
