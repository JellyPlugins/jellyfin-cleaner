/** * Dashboard tab navigation: the 8 tabs render and switch, with no uncaught JS * errors. */
import { test, expect } from '@playwright/test';
import { openDashboard, switchTab, trackConsoleErrors } from './_ui-helpers.ts';

// data-tab values (NOT the same as labels): arr = "ArrIntegration".
const ALWAYS_TABS = ['overview', 'codecs', 'health', 'trends', 'settings', 'arr', 'logs'];

test('all core tabs switch and activate without JS errors', async ({ page }) => {
  const errors = trackConsoleErrors(page);
  await openDashboard(page);

  for (const tab of ALWAYS_TABS) {
    await switchTab(page, tab);
    // The active button + panel share the tab id.
    await expect(page.locator(`.tab-btn[data-tab="${tab}"]`)).toHaveClass(/active/);
  }

  // NB: we intentionally do NOT switch to the Recommendations tab here - it has dedicated coverage in recommendations.ui.spec.ts.
  const scriptErrors = errors.filter((e) => !/Failed to load resource.*\b403\b/i.test(e));
  expect(scriptErrors, `uncaught JS errors: ${scriptErrors.join('\n')}`).toHaveLength(0);
});

test('overview renders stat cards after scan', async ({ page }) => {
  await openDashboard(page);
  await switchTab(page, 'overview');
  // After the global-setup scan, the overview should have content (stat cards
  // or a library table). Wait for either to appear.
  await expect(
    page.locator('#overviewContent .stat-card, #overviewContent .library-table').first(),
  ).toBeVisible({ timeout: 20_000 });
});

test('a browser refresh does not leave the stats admin-error banner stuck', async ({ page }) => {
  // On refresh the stats request can fire before Jellyfin's ApiClient token is
  // ready, briefly yielding 401/403. The plugin retries transient auth failures,
  // so an admin must never be left looking at "Failed to load statistics. Make
  // sure you are an administrator." Reload a few times and assert the banner is
  // not stuck and the overview still populates.
  await openDashboard(page);

  const errBanner = page.locator('#overviewContent .error-msg', { hasText: /administrator/i });

  for (let i = 0; i < 3; i++) {
    await page.reload({ waitUntil: 'load' });
    await expect(page.locator('.tab-bar')).toBeVisible({ timeout: 15_000 });
    // Once loading settles the transient banner must be gone (retry resolves it).
    await expect(errBanner).toBeHidden({ timeout: 20_000 });
  }

  // The overview must ultimately show real content, proving stats loaded.
  await expect(
    page.locator('#overviewContent .stat-card, #overviewContent .library-table').first(),
  ).toBeVisible({ timeout: 20_000 });
});
