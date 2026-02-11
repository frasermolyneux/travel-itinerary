# Travel Itinerary
[![Build and Test](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/build-and-test.yml)
[![Code Quality](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/codequality.yml/badge.svg)](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/codequality.yml)
[![Copilot Setup Steps](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/copilot-setup-steps.yml/badge.svg)](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/copilot-setup-steps.yml)
[![Dependabot Automerge](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/dependabot-automerge.yml/badge.svg)](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/dependabot-automerge.yml)
[![Deploy Dev](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/deploy-dev.yml/badge.svg)](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/deploy-dev.yml)
[![Deploy Prd](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/deploy-prd.yml/badge.svg)](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/deploy-prd.yml)
[![Destroy Development](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/destroy-development.yml/badge.svg)](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/destroy-development.yml)
[![Destroy Environment](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/destroy-environment.yml/badge.svg)](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/destroy-environment.yml)
[![PR Verify](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/frasermolyneux/travel-itinerary/actions/workflows/pr-verify.yml)

## Documentation
* [Offline Support](/docs/OFFLINE_SUPPORT.md) - Progressive Web App behavior, caching strategy, and offline usage notes.

## Overview
ASP.NET Core 9 Razor Pages app for sharing travel itineraries with companions. Editing is protected by Microsoft Entra ID via Microsoft.Identity.Web; anonymous share links allow read-only viewing. Azure Table Storage (via DefaultAzureCredential) stores trips, itinerary entries, bookings, and share links, with a PWA service worker providing offline access and manual sync. Terraform defines the Azure infrastructure and GitHub Actions handle build, quality checks, deployments, and environment teardown.

## Contributing
Please read the [contributing](CONTRIBUTING.md) guidance; this is a learning and development project.

## Security
Please read the [security](SECURITY.md) guidance; I am always open to security feedback through email or opening an issue.
