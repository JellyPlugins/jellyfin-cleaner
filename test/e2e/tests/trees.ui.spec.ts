/**
 * Codecs + Health collapsible "trees": clicking a breakdown row opens a file
 * tree; folder toggles expand/collapse; Expand All / Collapse All work. All
 * client-side (no API call on expand), driven by stable classes/data-attrs.
 */
import { test, expect } from '@playwright/test';
import { openDashboard, switchTab } from './_ui-helpers.ts';

test('Codecs tab: clicking a breakdown row opens a file tree that expands/collapses', async ({ page }) => {
  await openDashboard(page);
  await switchTab(page, 'codecs');

  // Requires scan data; wait for at least one clickable codec row.
  const row = page.locator('.codec-row.codec-clickable').first();
  await expect(row).toBeVisible({ timeout: 20_000 });

  const chart = await row.getAttribute('data-chart');
  await row.click();

  // The matching detail panel becomes visible with a rendered tree.
  const panel = page.locator(`#codecDetail_${chart}`);
  await expect(panel).toHaveClass(/file-tree-panel-visible/);
  await expect(panel.locator('.tree-view, .file-tree-section').first()).toBeVisible();

  // Expand a folder node if present.
  const toggle = panel.locator('[data-tree-toggle]').first();
  if (await toggle.count()) {
    const node = toggle.locator('xpath=ancestor::*[contains(@class,"tree-node")][1]');
    await toggle.click();
    await expect(node).toHaveClass(/tree-expanded/);

    // Expand All / Collapse All buttons.
    const expandAll = panel.locator('[data-tree-action="expand"]');
    const collapseAll = panel.locator('[data-tree-action="collapse"]');
    if (await expandAll.count()) {
      await expandAll.click();
      await expect(panel.locator('.tree-node.tree-expanded').first()).toBeVisible();
      await collapseAll.click();
    }
  }

  // Clicking the row again closes the panel (toggle).
  await row.click();
  await expect(panel).not.toHaveClass(/file-tree-panel-visible/);
});

test('Health tab: clicking a health item opens its detail tree', async ({ page }) => {
  await openDashboard(page);
  await switchTab(page, 'health');

  const item = page.locator('.health-item.health-clickable').first();
  await expect(item).toBeVisible({ timeout: 20_000 });
  await item.click();

  const panel = page.locator('#healthDetailPanel');
  await expect(panel).toHaveClass(/file-tree-panel-visible/);
});

test('Codecs tab: clicking a book format shows the book file tree, not an empty state', async ({ page }) => {
  // Regression guard for the book-format drill-down: under Jellyfin 12 the file
  // tree renderer had no "books" section and excluded books from its file total,
  // so clicking a book format (CBZ/EPUB/PDF) rendered "No files found." even
  // though the chart above listed those formats. This clicks the bookFormats row
  // and asserts a real books section with at least one file node appears.
  await openDashboard(page);
  await switchTab(page, 'codecs');

  const bookRow = page.locator('.codec-row.codec-clickable[data-chart="bookFormats"]').first();
  // The bookFormats chart only renders when a Book library exists in the fixture.
  if ((await bookRow.count()) === 0) {
    test.skip(true, 'no book-format breakdown present in this fixture');
    return;
  }
  await expect(bookRow).toBeVisible({ timeout: 20_000 });
  await bookRow.click();

  const panel = page.locator('#codecDetail_bookFormats');
  await expect(panel).toHaveClass(/file-tree-panel-visible/);
  // The books section must render (badge-books) and must NOT be the empty state.
  await expect(panel.locator('.file-tree-section .badge-books')).toBeVisible();
  await expect(panel.locator('.file-tree-empty')).toHaveCount(0);
  await expect(panel.locator('.tree-leaf, .tree-leaf-file-name').first()).toBeVisible();
});

test('Settings tab: the Excluded Libraries multi-select lists libraries', async ({ page }) => {
  // Regression guard for the empty "Excluded Libraries" dropdown: the frontend
  // read data.libraries (camelCase) but Jellyfin 12 serializes the response as
  // data.Libraries (PascalCase), so the list came back empty and the panel showed
  // "No data". This asserts the multi-select is populated with real entries.
  await openDashboard(page);
  await switchTab(page, 'settings');

  const wrapper = page.locator('#cfgExcludedWrapper');
  await expect(wrapper.locator('.library-multiselect-toggle')).toBeVisible({ timeout: 20_000 });

  // Open the dropdown panel and assert it contains selectable library entries
  // rather than the "No data" fallback.
  await wrapper.locator('.library-multiselect-toggle').click();
  await expect(wrapper.locator('.library-multiselect-item').first()).toBeVisible();
  expect(await wrapper.locator('.library-multiselect-item').count()).toBeGreaterThan(0);
});
