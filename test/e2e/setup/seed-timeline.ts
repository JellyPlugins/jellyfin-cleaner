/**
 * Seeds the plugin's growth timeline cache with a backdated, multi-year daily series.
 *
 * Every fake media file the harness generates is written at scan time, so a timeline
 * built from file creation dates collapses to a single day and the Trends chart has
 * nothing to zoom or pan across. GrowthTimelineController serves the cached JSON verbatim
 * when forceRefresh is false (the UI default), and ComputeTimelineAsync only runs from the
 * weekly task or an explicit forceRefresh=true, so writing this file directly gives the
 * chart a realistic span.
 *
 * The series is deliberately shaped to be exactly what TimelineAggregator.IsDayBased
 * accepts: granularity "daily", every date at midnight UTC, strictly ascending. It is a
 * shared helper because api specs that call forceRefresh=true overwrite the cache with a
 * real single-day compute, so the UI chart spec re-seeds before it runs.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));

/** Absolute host path of the plugin DataPath (/config/data inside the container). */
export function timelineDataDir(): string {
  return join(__dirname, '..', 'runtime', 'config', 'data');
}

/**
 * Writes jellyfin-helper-growth-timeline.json into the plugin DataPath. Idempotent: the
 * file is fully rewritten each call. Returns the number of points written.
 */
export function seedGrowthTimeline(): number {
  const points: Array<{ date: string; cumulativeSize: number; cumulativeFileCount: number }> = [];

  const now = new Date();
  // Stop a few days before today so the seeded history is entirely in the past and the
  // newest point never lands on the same day the test runs (which would depend on wall time).
  const endMs = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()) - 3 * 24 * 60 * 60 * 1000;

  let cumulativeSize = 0;
  let cumulativeFileCount = 0;
  const cursor = new Date(Date.UTC(2016, 0, 1));
  let i = 0;
  while (cursor.getTime() <= endMs) {
    // A gentle daily ramp with frequent plateaus (most days add nothing) so the dedup and
    // interpolation paths the renderer runs are actually exercised over a dense daily series.
    if (i % 4 === 0) {
      cumulativeSize += (2 + (i % 5)) * 1024 * 1024 * 1024; // 2..6 GB on active days
      cumulativeFileCount += 3 + (i % 7);
    }
    points.push({
      date: new Date(Date.UTC(cursor.getUTCFullYear(), cursor.getUTCMonth(), cursor.getUTCDate())).toISOString(),
      cumulativeSize,
      cumulativeFileCount,
    });
    cursor.setUTCDate(cursor.getUTCDate() + 1);
    i++;
  }

  // Collapse consecutive-equal points (keeping the first and last) to mirror the server's
  // own DeduplicateConsecutivePoints, so the seed looks like a real persisted timeline.
  const deduped = points.filter(
    (pt, idx) =>
      idx === 0 ||
      idx === points.length - 1 ||
      pt.cumulativeSize !== points[idx - 1].cumulativeSize ||
      pt.cumulativeFileCount !== points[idx - 1].cumulativeFileCount,
  );

  const timeline = {
    granularity: 'daily',
    earliestFileDate: deduped[0].date,
    computedAt: now.toISOString(),
    firstScanTimestamp: deduped[0].date,
    totalDirectoriesScanned: 3,
    dataPoints: deduped,
  };

  const dir = timelineDataDir();
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, 'jellyfin-helper-growth-timeline.json'), JSON.stringify(timeline, null, 2));
  return deduped.length;
}
