const CACHE_KEY = 'backendSelector.cache';
const CACHE_TTL = 5 * 60 * 1000; // 5分钟
let refreshTimer = null;

function fetchWithTimeout(url, timeout = 1000) {
  const controller = new AbortController();
  const signal = controller.signal;
  const timer = setTimeout(() => controller.abort(), timeout);
  console.log(`[fetchWithTimeout] Fetching: ${url}`);
  return fetch(url, { signal }).finally(() => clearTimeout(timer));
}

export async function testEndpoint(endpoint) {
  const start = performance.now();
  console.log(`[testEndpoint] Testing ${endpoint.url} with priority ${endpoint.priority}`);
  try {
    const response = await fetchWithTimeout(`${endpoint.url}/ok`, 1000);
    const latency = performance.now() - start;
    const json = await response.json();
    const isValid = json?.ok === 'ojbk';

    console.log(`[testEndpoint] Response from ${endpoint.url}:`, json);
    console.log(`[testEndpoint] Valid: ${isValid}, Latency: ${latency.toFixed(2)}ms`);

    if (isValid) {
      return { ...endpoint, latency, valid: true };
    } else {
      await new Promise(resolve => setTimeout(resolve, 1000));
      return { ...endpoint, latency: Infinity, valid: false };
    }
  } catch (err) {
    console.warn(`[testEndpoint] Error from ${endpoint.url}:`, err);
    await new Promise(resolve => setTimeout(resolve, 1000));
    return { ...endpoint, latency: Infinity, valid: false };
  }
}

function getCachedUrl() {
  const raw = sessionStorage.getItem(CACHE_KEY);
  if (!raw) {
    console.log('[getCachedUrl] No cache found');
    return null;
  }

  try {
    const { url, timestamp } = JSON.parse(raw);
    const age = Date.now() - timestamp;
    if (age < CACHE_TTL) {
      console.log(`[getCachedUrl] Cache hit: ${url}, age: ${age}ms`);
      return url;
    } else {
      console.log(`[getCachedUrl] Cache expired: ${url}, age: ${age}ms`);
    }
  } catch (err) {
    console.warn('[getCachedUrl] Cache parse error:', err);
  }

  return null;
}

function setCachedUrl(url) {
  console.log(`[setCachedUrl] Caching URL: ${url}`);
  sessionStorage.setItem(CACHE_KEY, JSON.stringify({
    url,
    timestamp: Date.now()
  }));
  window.backendUrl = url;
}

export async function selectBackend(endpoints, useCache = true) {
  console.log(`[selectBackend] Called with useCache=${useCache}`);

  if (!Array.isArray(endpoints) || endpoints.length === 0) {
    throw new Error('Endpoint list is empty.');
  }

  if (endpoints.length === 1) {
    const singleUrl = endpoints[0].url;
    console.log(`[selectBackend] Only one endpoint, using: ${singleUrl}`);
    setCachedUrl(singleUrl);
    return singleUrl;
  }

  if (useCache) {
    const cached = getCachedUrl();
    if (cached) {
      window.backendUrl = cached;

      if (refreshTimer) {
        console.log('[selectBackend] Refresh already scheduled, delaying');
        clearTimeout(refreshTimer);
      }

      refreshTimer = setTimeout(() => {
        console.log('[selectBackend] Refreshing backend selection');
        selectBackend(endpoints, false);
        refreshTimer = null;
      }, 1000);

      return cached;
    }
  }

  console.log('[selectBackend] Running full selection logic');
  const allTasks = endpoints.map(testEndpoint);
  const firstResult = await Promise.race(allTasks);

  console.log(`[selectBackend] First result: ${firstResult.url}, latency: ${firstResult.latency}, priority: ${firstResult.priority}`);

  const isValid = firstResult.valid && firstResult.latency < Infinity;
  const isSlow = firstResult.latency >= 100;
  const isHighPriority = firstResult.priority <= 2;

  if (!isValid || isHighPriority || isSlow) {
    console.log('[selectBackend] Accepting first result due to validity or priority/latency');
    setCachedUrl(firstResult.url);
    return firstResult.url;
  }

  console.log('[selectBackend] Waiting for higher priority endpoints');
  const highPriorityTasks = endpoints
    .filter(ep => ep.priority <= 2)
    .map(testEndpoint);
  const result2 = await Promise.race([
    ...highPriorityTasks,
    new Promise(resolve => setTimeout(() => resolve({ timeout: true }), 500))
  ]);

  if (result2.url && result2.valid) {
    console.log(`[selectBackend] Higher priority result accepted: ${result2.url}`);
    setCachedUrl(result2.url);
    return result2.url;
  }

  console.log('[selectBackend] Falling back to first result');
  setCachedUrl(firstResult.url);
  return firstResult.url;
}

export function clearBackendCache() {
  console.log('[clearBackendCache] Clearing backend cache');
  sessionStorage.removeItem('backendSelector.cache');
  window.backendUrl = undefined;
}
