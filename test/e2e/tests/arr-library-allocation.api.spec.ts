/**
 * Per-instance library allocation for Arr instances, end to end against real Jellyfin
 * plus the mock Arr server. Proves that with multiple instances the compare is scoped to
 * the libraries an instance owns (manual override or auto-matched root folders), and that
 * the scope is constrained to the instance collection type.
 */
import { test, expect, type APIRequestContext } from '@playwright/test';
import { apiContext, loadAuth, p, assertPluginActive } from '../setup/api-client.ts';

const ARR_URL = process.env.MOCK_ARR_URL ?? 'http://mock-arr:9000';

// The E2E Jellyfin has a "Movies" (movies) and a "Shows" (tvshows) library (see global-setup).
const MOVIES_LIB = 'Movies';
const SHOWS_LIB = 'Shows';

interface CompareResult {
  InBoth: string[];
  InArrOnly: string[];
  InArrOnlyMissing: string[];
  InJellyfinOnly: string[];
}

let ctx: APIRequestContext;

test.beforeAll(async () => {
  ctx = await apiContext(loadAuth());
});
test.afterAll(async () => {
  // Leave a single reachable instance so later files that assume one Radarr keep working.
  await ctx.put(p('Configuration'), {
    headers: { 'Content-Type': 'application/json' },
    data: {
      RadarrInstances: [{ Name: 'Mock Radarr', Url: ARR_URL, ApiKey: 'radarr-key' }],
      SonarrInstances: [{ Name: 'Mock Sonarr', Url: ARR_URL, ApiKey: 'sonarr-key' }],
    },
  });
  await ctx.dispose();
});

async function seedRadarr(instances: Array<{ Name: string; Libraries?: string }>): Promise<void> {
  const res = await ctx.put(p('Configuration'), {
    headers: { 'Content-Type': 'application/json' },
    data: {
      RadarrInstances: instances.map((i) => ({
        Name: i.Name,
        Url: ARR_URL,
        ApiKey: 'radarr-key',
        Libraries: i.Libraries ?? '',
      })),
    },
  });
  expect(res.ok(), `seed failed: ${res.status()}`).toBeTruthy();
}

test('Libraries assignment survives the Configuration round trip', async () => {
  await seedRadarr([
    { Name: 'R1', Libraries: MOVIES_LIB },
    { Name: 'R2', Libraries: '' },
  ]);
  const res = await ctx.get(p('Configuration'));
  expect(res.ok()).toBeTruthy();
  const cfg = (await res.json()) as { RadarrInstances: Array<{ Name: string; Libraries: string }> };
  const r1 = cfg.RadarrInstances.find((i) => i.Name === 'R1');
  expect(r1?.Libraries).toBe(MOVIES_LIB);
});

test('Manual override scopes the compare to the assigned library', async () => {
  // Two instances so scoping applies. R1 owns Movies; R2 exists only to make the count > 1.
  await seedRadarr([
    { Name: 'R1', Libraries: MOVIES_LIB },
    { Name: 'R2', Libraries: SHOWS_LIB },
  ]);
  const res = await ctx.get(p('ArrIntegration/Compare/Radarr?index=0'));
  expect(res.ok(), `compare failed: ${res.status()}`).toBeTruthy();
  const body = (await res.json()) as CompareResult;
  // Movies library is in scope, so the mock's Inception (has file, no Jellyfin match) is reported.
  expect(body.InArrOnly.join(' ')).toContain('Inception');
});

test('Radarr override naming a TV library yields an empty scope', async () => {
  // A Radarr instance assigned only a tvshows library resolves to no movie library after the
  // collection-type filter, so nothing is compared and every bucket is empty.
  await seedRadarr([
    { Name: 'R1', Libraries: SHOWS_LIB },
    { Name: 'R2', Libraries: MOVIES_LIB },
  ]);
  const res = await ctx.get(p('ArrIntegration/Compare/Radarr?index=0'));
  expect(res.ok(), `compare failed: ${res.status()}`).toBeTruthy();
  const body = (await res.json()) as CompareResult;
  expect(body.InJellyfinOnly).toHaveLength(0);
  expect(body.InBoth).toHaveLength(0);
  await assertPluginActive(ctx);
});

test('Auto-match resolves the library from the instance root folders', async () => {
  // No override on either instance: the mock reports root folder /movies, whose last segment
  // matches the Movies library. The compare must still return the Radarr buckets.
  await seedRadarr([
    { Name: 'R1', Libraries: '' },
    { Name: 'R2', Libraries: '' },
  ]);
  const res = await ctx.get(p('ArrIntegration/Compare/Radarr?index=0'));
  expect(res.ok(), `compare failed: ${res.status()}`).toBeTruthy();
  const body = (await res.json()) as CompareResult;
  expect(body.InArrOnly.join(' ')).toContain('Inception');
  await assertPluginActive(ctx);
});

test('A single instance ignores its override and compares all libraries', async () => {
  // One instance owns everything of its type; the override must not narrow the scope.
  await seedRadarr([{ Name: 'Solo', Libraries: SHOWS_LIB }]);
  const res = await ctx.get(p('ArrIntegration/Compare/Radarr?index=0'));
  expect(res.ok(), `compare failed: ${res.status()}`).toBeTruthy();
  const body = (await res.json()) as CompareResult;
  // Movies library is still compared despite the (ignored) TV override.
  expect(body.InArrOnly.join(' ')).toContain('Inception');
  await assertPluginActive(ctx);
});
