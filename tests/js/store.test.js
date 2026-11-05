import { describe, expect, it } from 'vitest';
import { DRAFT_PREFIX, EntryStore, LOCAL_CAP, deleteDatabase, draftKeys, excessOf, migrations, readDraft, upgrade, writeDraft } from '../../src/Nest.Web/wwwroot/js/store.js';

class MemoryStorage {
    constructor() {
        this.map = new Map();
    }

    get length() {
        return this.map.size;
    }

    key(index) {
        return [...this.map.keys()][index] ?? null;
    }

    getItem(key) {
        return this.map.get(key) ?? null;
    }

    setItem(key, value) {
        this.map.set(key, String(value));
    }

    removeItem(key) {
        this.map.delete(key);
    }
}

describe('store', () => {
    it('caps local history', () => {
        expect(excessOf(LOCAL_CAP)).toBe(0);
        expect(excessOf(LOCAL_CAP + 5)).toBe(5);
    });

    it('runs every migration from the stored version', () => {
        const created = [];
        const db = { createObjectStore: (name, options) => created.push([name, options]) };
        upgrade(db, 0, migrations.length);
        expect(created).toEqual([['entries', { keyPath: 'id' }]]);
        created.length = 0;
        upgrade(db, 1, 1);
        expect(created).toEqual([]);
    });

    it('keeps comment drafts per home', () => {
        const storage = new MemoryStorage();
        writeDraft(storage, 'zillow:1', 'hello');
        storage.setItem('nest-theme', 'dark');
        expect(readDraft(storage, 'zillow:1')).toBe('hello');
        expect(draftKeys(storage)).toEqual([`${DRAFT_PREFIX}zillow:1`]);
        writeDraft(storage, 'zillow:1', '');
        expect(readDraft(storage, 'zillow:1')).toBe('');
        expect(readDraft(null, 'x')).toBe('');
    });

    it('works without IndexedDB', async () => {
        const store = new EntryStore(undefined);
        expect(await store.all()).toEqual([]);
        expect(await store.save([{ id: 'a' }])).toEqual([]);
        expect(await deleteDatabase(undefined)).toBe('deleted');
    });
});
