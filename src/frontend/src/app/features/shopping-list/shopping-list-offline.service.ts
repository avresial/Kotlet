import { Injectable } from '@angular/core';
import { CurrentUser } from '../../core/auth/auth.models';
import { Ingredient } from '../ingredients/ingredient.models';
import { PreparedMeal } from '../prepared-meals/prepared-meal.models';
import { ShoppingListItem, ShoppingListUpdate } from './shopping-list.models';

const databaseName = 'kotlet-shopping-list';
const databaseVersion = 1;
const storeName = 'lists';
const snapshotTtlMs = 7 * 24 * 60 * 60 * 1000;

export interface ShoppingListSnapshot {
  items: ShoppingListItem[];
  ingredients: Ingredient[];
  preparedMeals: PreparedMeal[];
  savedAt: number;
}

export interface ShoppingListMutation extends ShoppingListUpdate {
  operationId: string;
  itemId: string;
  confirmed: ShoppingListItem | null;
  queuedAt: number;
  status: 'pending' | 'failed';
  error?: string;
}

export interface ShoppingListOfflineState {
  snapshot: ShoppingListSnapshot | null;
  mutations: ShoppingListMutation[];
}

interface CacheRecord extends ShoppingListOfflineState {
  schemaVersion: number;
  key: string;
}

export function shoppingListCacheKey(user: Pick<CurrentUser, 'id' | 'activeHouseId'> | null): string | null {
  return user?.activeHouseId ? `${user.id}:${user.activeHouseId}` : null;
}

@Injectable({ providedIn: 'root' })
export class ShoppingListOfflineService {
  private writes: Promise<void> = Promise.resolve();

  load(key: string): Promise<ShoppingListOfflineState> {
    return this.read(key).then(record => {
      if (!record) return { snapshot: null, mutations: [] };
      const snapshot = record.snapshot && Date.now() - record.snapshot.savedAt <= snapshotTtlMs
        ? record.snapshot
        : null;
      return { snapshot, mutations: record.mutations ?? [] };
    });
  }

  saveSnapshot(key: string, snapshot: Omit<ShoppingListSnapshot, 'savedAt'>): Promise<void> {
    return this.write(key, record => ({
      ...record,
      snapshot: { ...snapshot, savedAt: Date.now() },
    }));
  }

  queueMutation(key: string, mutation: ShoppingListMutation): Promise<void> {
    return this.write(key, record => ({
      ...record,
      mutations: [...record.mutations.filter(value => value.itemId !== mutation.itemId), mutation],
    }));
  }

  removeMutation(key: string, operationId: string): Promise<void> {
    return this.write(key, record => ({
      ...record,
      mutations: record.mutations.filter(value => value.operationId !== operationId),
    }));
  }

  markMutationFailed(key: string, operationId: string, error: string): Promise<void> {
    return this.write(key, record => ({
      ...record,
      mutations: record.mutations.map(mutation => mutation.operationId === operationId
        ? { ...mutation, status: 'failed' as const, error } : mutation),
    }));
  }

  clearUser(userId: string): Promise<void> {
    return this.enqueue(async () => {
      const db = await this.open();
      try {
        await new Promise<void>((resolve, reject) => {
          const transaction = db.transaction(storeName, 'readwrite');
          const request = transaction.objectStore(storeName).getAllKeys();
          request.onsuccess = () => {
            for (const key of request.result) {
              if (String(key).startsWith(`${userId}:`)) transaction.objectStore(storeName).delete(key);
            }
          };
          request.onerror = () => reject(request.error ?? new Error('Unable to inspect shopping-list cache.'));
          transaction.oncomplete = () => resolve();
          transaction.onerror = () => reject(transaction.error ?? new Error('Unable to clear shopping-list cache.'));
          transaction.onabort = () => reject(transaction.error ?? new Error('Unable to clear shopping-list cache.'));
        });
      } finally {
        db.close();
      }
    });
  }

  private read(key: string): Promise<CacheRecord | null> {
    return this.enqueue(async () => {
      const db = await this.open();
      try {
        return await new Promise<CacheRecord | null>((resolve, reject) => {
          const transaction = db.transaction(storeName, 'readonly');
          const request = transaction.objectStore(storeName).get(key);
          request.onsuccess = () => resolve((request.result as CacheRecord | undefined) ?? null);
          request.onerror = () => reject(request.error ?? new Error('Unable to read shopping-list cache.'));
        });
      } finally {
        db.close();
      }
    });
  }

  private write(key: string, update: (record: CacheRecord) => CacheRecord): Promise<void> {
    return this.enqueue(async () => {
      const db = await this.open();
      try {
        await new Promise<void>((resolve, reject) => {
          const transaction = db.transaction(storeName, 'readwrite');
          const store = transaction.objectStore(storeName);
          const request = store.get(key);
          request.onsuccess = () => {
            const current = request.result as CacheRecord | undefined;
            store.put(update(current ?? { key, schemaVersion: databaseVersion, snapshot: null, mutations: [] }));
          };
          request.onerror = () => reject(request.error ?? new Error('Unable to update shopping-list cache.'));
          transaction.oncomplete = () => resolve();
          transaction.onerror = () => reject(transaction.error ?? new Error('Unable to update shopping-list cache.'));
          transaction.onabort = () => reject(transaction.error ?? new Error('Unable to update shopping-list cache.'));
        });
      } finally {
        db.close();
      }
    });
  }

  private enqueue<T>(operation: () => Promise<T>): Promise<T> {
    const result = this.writes.then(operation);
    this.writes = result.then(() => undefined, () => undefined);
    return result;
  }

  private open(): Promise<IDBDatabase> {
    const factory = globalThis.indexedDB;
    if (!factory) return Promise.reject(new Error('IndexedDB is unavailable.'));
    return new Promise((resolve, reject) => {
      const request = factory.open(databaseName, databaseVersion);
      request.onupgradeneeded = () => {
        if (!request.result.objectStoreNames.contains(storeName)) request.result.createObjectStore(storeName, { keyPath: 'key' });
      };
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error ?? new Error('Unable to open shopping-list cache.'));
      request.onblocked = () => reject(new Error('Shopping-list cache is blocked by another tab.'));
    });
  }
}
