// Bound independent file operations while retaining deterministic result order.
export async function mapConcurrent(items, concurrency, transform) {
  if (!Number.isInteger(concurrency) || concurrency < 1)
    throw new RangeError('Concurrency must be a positive integer.');
  const results = new Array(items.length);
  let next = 0;
  await Promise.all(Array.from({ length: Math.min(concurrency, items.length) }, async () => {
    while (next < items.length) {
      const index = next++;
      results[index] = await transform(items[index], index);
    }
  }));
  return results;
}
