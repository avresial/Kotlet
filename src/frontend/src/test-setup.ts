const createStorage = (): Storage => {
  const values = new Map<string, string>();

  return {
    get length() {
      return values.size;
    },
    clear: () => values.clear(),
    getItem: (key: string) => values.get(key) ?? null,
    key: (index: number) => [...values.keys()][index] ?? null,
    removeItem: (key: string) => values.delete(key),
    setItem: (key: string, value: string) => values.set(key, String(value)),
  };
};

const getStorage = (): Storage | undefined => {
  try {
    return globalThis.localStorage;
  } catch {
    return undefined;
  }
};

const storage = getStorage() ?? createStorage();

if (getStorage() !== storage) {
  Object.defineProperty(globalThis, 'localStorage', {
    configurable: true,
    value: storage,
  });
}

if (typeof window !== 'undefined') {
  let windowStorage: Storage | undefined;
  try {
    windowStorage = window.localStorage;
  } catch {
    windowStorage = undefined;
  }

  if (!windowStorage) {
    Object.defineProperty(window, 'localStorage', {
      configurable: true,
      value: storage,
    });
  }
}
