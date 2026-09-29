import "@testing-library/jest-dom/vitest";

// Node 22+ ships a native `localStorage`, and in this jsdom/Node
// combination it silently wins over jsdom's own implementation but is left
// unusable without a `--localstorage-file` path (methods like `.clear()`
// are missing). Rather than depend on Node flags or version, install a
// simple in-memory polyfill so tests get a working localStorage no matter
// which Node version runs them.
class MemoryStorage implements Storage {
  private store = new Map<string, string>();

  get length(): number {
    return this.store.size;
  }

  clear(): void {
    this.store.clear();
  }

  getItem(key: string): string | null {
    return this.store.has(key) ? this.store.get(key)! : null;
  }

  key(index: number): string | null {
    return Array.from(this.store.keys())[index] ?? null;
  }

  removeItem(key: string): void {
    this.store.delete(key);
  }

  setItem(key: string, value: string): void {
    this.store.set(key, String(value));
  }
}

const memoryStorage = new MemoryStorage();

for (const target of [globalThis, typeof window !== "undefined" ? window : undefined]) {
  if (target) {
    Object.defineProperty(target, "localStorage", {
      value: memoryStorage,
      configurable: true,
    });
  }
}
