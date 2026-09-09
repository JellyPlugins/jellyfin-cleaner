/**
 * Per-instance library allocation, driven through the UI. With two Radarr instances the Settings
 * tab shows a per-instance library picker (movie libraries only), selections autosave, and the Arr
 * tab Compare button still renders a result. Complements arr-library-allocation.api.spec.ts, which
 * proves the scoping behaviour at the API level.
 */
import { test, expect, type APIRequestContext } from '@playwright/test';
import { openDashboard, switchTab } from './_ui-helpers.ts';
import { apiContext, loadAuth, p } from '../setup/api-client.ts';

const ARR_URL = process.env.MOCK_ARR_URL ?? 'http://mock-arr:9000';

let ctx: APIRequestContext;

test.beforeAll(async () => {
  ctx = await apiContext(loadAuth());
  // Two Radarr instances so the per-instance picker appears (a single instance owns everything
  // of its type and shows no picker). Two Sonarr instances for the same reason.
  const seed = await ctx.put(p('Configuration'), {
    headers: { 'Content-Type': 'application/json' },
    data: {
      RadarrInstances: [
        { Name: 'Radarr A', Url: ARR_URL, ApiKey: 'radarr-key', Libraries: 'Movies' },
        { Name: 'Radarr B', Url: ARR_URL, ApiKey: 'radarr-key', Libraries: '' },
      ],
      SonarrInstances: [
        { Name: 'Sonarr A', Url: ARR_URL, ApiKey: 'sonarr-key', Libraries: '' },
        { Name: 'Sonarr B', Url: ARR_URL, ApiKey: 'sonarr-key', Libraries: '' },
      ],
    },
  });
  expect(seed.ok(), `seed failed: ${seed.status()}`).toBeTruthy();
});

test.afterAll(async () => {
  // Restore a single reachable instance for files that assume one Radarr/Sonarr.
  await ctx.put(p('Configuration'), {
    headers: { 'Content-Type': 'application/json' },
    data: {
      RadarrInstances: [{ Name: 'Mock Radarr', Url: ARR_URL, ApiKey: 'radarr-key' }],
      SonarrInstances: [{ Name: 'Mock Sonarr', Url: ARR_URL, ApiKey: 'sonarr-key' }],
    },
  });
  await ctx.dispose();
});

test('Settings tab: a Radarr instance shows a library picker listing only movie libraries', async ({ page }) => {
  await openDashboard(page);
  await switchTab(page, 'settings');

  const wrapper = page.locator('#Radarr_0_libs');
  await expect(wrapper.locator('.library-multiselect-toggle')).toBeVisible({ timeout: 20_000 });

  await wrapper.locator('.library-multiselect-toggle').click();
  const items = wrapper.locator('.library-multiselect-item');
  await expect(items.first()).toBeVisible();

  // Radarr may only manage movie libraries: the E2E env has Movies (movies), Shows (tvshows),
  // Books (books). Only Movies must appear.
  const labels = (await items.locator('label').allInnerTexts()).map((t) => t.trim());
  expect(labels.join(' | ')).toContain('Movies');
  expect(labels.join(' | ')).not.toContain('Shows');
  expect(labels.join(' | ')).not.toContain('Books');
});

test('Settings tab: deselecting all libraries persists as automatic (empty) and autosaves', async ({ page }) => {
  await openDashboard(page);
  await switchTab(page, 'settings');

  const wrapper = page.locator('#Radarr_0_libs');
  await expect(wrapper.locator('.library-multiselect-toggle')).toBeVisible({ timeout: 20_000 });
  await wrapper.locator('.library-multiselect-toggle').click();

  // Uncheck every currently-checked box; the change handler autosaves each time.
  const checked = wrapper.locator('input[type="checkbox"]:checked');
  const count = await checked.count();
  for (let i = 0; i < count; i++) {
    // Re-query each iteration: unchecking mutates the :checked set.
    await wrapper.locator('input[type="checkbox"]:checked').first().uncheck();
  }

  await expect
    .poll(
      async () => {
        const res = await ctx.get(p('Configuration'));
        const cfg = (await res.json()) as { RadarrInstances: Array<{ Name: string; Libraries: string }> };
        return cfg.RadarrInstances.find((r) => r.Name === 'Radarr A')?.Libraries ?? null;
      },
      { timeout: 15_000 },
    )
    .toBe('');
});

test('Settings tab: selecting a library persists that assignment and autosaves', async ({ page }) => {
  await openDashboard(page);
  await switchTab(page, 'settings');

  const wrapper = page.locator('#Radarr_0_libs');
  await expect(wrapper.locator('.library-multiselect-toggle')).toBeVisible({ timeout: 20_000 });
  await wrapper.locator('.library-multiselect-toggle').click();

  // Check the Movies box (the only movie library in the E2E env).
  await wrapper.locator('input[type="checkbox"][value="Movies"]').check();

  await expect
    .poll(
      async () => {
        const res = await ctx.get(p('Configuration'));
        const cfg = (await res.json()) as { RadarrInstances: Array<{ Name: string; Libraries: string }> };
        return cfg.RadarrInstances.find((r) => r.Name === 'Radarr A')?.Libraries ?? null;
      },
      { timeout: 15_000 },
    )
    .toContain('Movies');
});

test('Arr tab: Compare renders a result with a per-instance library assignment set', async ({ page }) => {
  await openDashboard(page);
  await switchTab(page, 'arr');

  const compareBtn = page.locator('#btnCompareRadarr');
  await expect(compareBtn).toBeVisible({ timeout: 15_000 });

  const [resp] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/JellyfinHelper/ArrIntegration/Compare/Radarr'), {
      timeout: 20_000,
    }),
    compareBtn.click(),
  ]);
  expect(resp.ok(), `Compare failed: ${resp.status()}`).toBeTruthy();

  await expect(page.locator('#arrResult .arr-card, #arrResult .arr-section').first()).toBeVisible({
    timeout: 15_000,
  });
});
