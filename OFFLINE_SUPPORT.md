# Offline Support - Progressive Web App (PWA)

## Overview

The Travel Itinerary application has been enhanced with Progressive Web App (PWA) capabilities to provide offline support. This allows users to access their travel itineraries even without an internet connection.

## Features

### What Works Offline

✅ **View Previously Loaded Trips** - Users can browse trips they've already loaded while online
✅ **Browse Cached Itinerary Details** - Itinerary entries, timelines, and details are available offline
✅ **Review Booking Information** - Booking details remain accessible without connectivity
✅ **Static Assets** - CSS, JavaScript, and images are cached for fast loading

### What Requires Internet Connection

❌ **Create or Edit Trips** - Creating new trips or editing existing ones requires an online connection
❌ **Add New Bookings** - Adding or modifying booking information needs internet access
❌ **Share Itineraries** - Generating and managing share links requires connectivity
❌ **Load New Data** - Fetching data that hasn't been cached requires internet access
❌ **Authentication** - Sign in/sign out operations require online connectivity

## Technical Implementation

### Architecture

The PWA implementation uses the following technologies:

1. **Service Worker** (`/wwwroot/sw.js`) - Intercepts network requests and implements caching strategies
2. **Web App Manifest** (`/wwwroot/manifest.json`) - Defines PWA metadata for installability
3. **PWA JavaScript** (`/wwwroot/js/pwa.js`) - Handles service worker registration and user notifications
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

The manifest currently uses only the favicon. For a complete PWA experience, add custom icons:

1. Create icons in multiple sizes:
   - `icon-192.png` (192x192px)
   - `icon-512.png` (512x512px)

2. Update `/wwwroot/manifest.json`:
```json
{
  "icons": [
    {
      "src": "/icon-192.png",
      "sizes": "192x192",
      "type": "image/png"
    },
    {
      "src": "/icon-512.png",
      "sizes": "512x512",
      "type": "image/png"
    }
  ]
}
```

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
