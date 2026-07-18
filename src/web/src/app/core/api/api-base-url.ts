import { InjectionToken } from '@angular/core';

/**
 * The base path every typed service prepends to its gateway routes. It is a same-origin relative mount
 * (`/api`), NOT a service address: the dev-server proxy (proxy.conf.js) forwards `/api/*` to the gateway
 * address Aspire injects. Provided in app.config.ts so it stays the single, swappable seam.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
