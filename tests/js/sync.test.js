import { describe, expect, it, vi } from 'vitest';
import { Nest, chunk, chunkBySize, missingIds, needsResync, offerCandidates, wire } from '../../src/Nest.Web/wwwroot/js/sync.js';

const id = n => `0195${n.toString(16).padStart(4, '0')}-0000-7000-8000-${n.toString(16).padStart(12, '0')}`;
const comment = (n, text = 'hi') => ({ id: id(n), kind: 'comment', keys: ['zillow:1'], text });

class MemoryStore {
    constructor(records = []) {
        this.records = new Map(records.map(record => [record.id, record]));
    }

    async all() {
        return [...this.records.values()];
    }

    async save(records) {
        for (const record of records) {
            this.records.set(record.id, record);
        }
        return [];
    }
}

function fakeWindow() {
    return {
        setTimeout: () => 1,
        clearTimeout: () => {},
        setInterval: () => 1,
        clearInterval: () => {},
        addEventListener: () => {},
    };
}

function fakeServer(entries, capacity = 1000) {
    const calls = [];
    const known = new Map(entries.map(entry => [entry.id, entry]));
    return {
        calls,
        known,
        invokeMethodAsync: vi.fn(async (method, ...args) => {
            calls.push([method, ...args]);
            if (method === 'Fetch') {
                return args[0].map(value => known.get(value)).filter(Boolean);
            }
            if (method === 'Offer') {
                return args[0].length;
            }
            if (method === 'Post') {
                const entry = comment(900, args[0].text);
                return { result: 'accepted', entry, key: 'zillow:1' };
            }
            if (method === 'Ping') {
                return true;
            }
            return undefined;
        }),
        capacity,
    };
}

describe('helpers', () => {
    it('detects version gaps', () => {
        expect(needsResync(3, { previous: 3 })).toBe(false);
        expect(needsResync(3, { previous: 2 })).toBe(true);
    });

    it('wires entries without local fields', () => {
        expect(wire({ ...comment(1), mine: true })).toEqual(comment(1));
        expect(wire({ id: id(1), kind: 'link', keys: ['zillow:1'], url: 'u' })).toEqual({ id: id(1), kind: 'link', keys: ['zillow:1'], url: 'u' });
        expect(wire({ id: id(1), kind: 'fact', keys: ['zillow:1'], fields: [] })).toEqual({ id: id(1), kind: 'fact', keys: ['zillow:1'], fields: [] });
    });

    it('finds missing ids', () => {
        expect(missingIds(['a', 'b'], new Map([['a', {}]]))).toEqual(['b']);
    });

    it('offers only what the server lacks, newest first, within its capacity', () => {
        const local = [comment(1), comment(2), comment(3), comment(4)];
        expect(offerCandidates(local, new Set([id(2)]), 10).map(entry => entry.id)).toEqual([id(4), id(3), id(1)]);
        expect(offerCandidates(local, new Set([id(2), id(3)]), 2).map(entry => entry.id)).toEqual([id(4)]);
        expect(offerCandidates(local, new Set(), 2).map(entry => entry.id)).toEqual([id(4), id(3)]);
    });

    it('chunks by size and count', () => {
        const items = Array.from({ length: 10 }, (_, index) => comment(index, 'x'.repeat(400)));
        const chunks = chunkBySize(items, 1000);
        expect(chunks.flat()).toEqual(items);
        expect(chunks.every(part => part.length >= 1)).toBe(true);
        expect(chunkBySize([comment(1, 'x'.repeat(2000))], 1000)).toEqual([]);
        expect(chunk([1, 2, 3], 2)).toEqual([[1, 2], [3]]);
    });
});

describe('Nest', () => {
    it('fetches what it lacks and offers what the server lacks', async () => {
        const server = fakeServer([comment(2), comment(3)]);
        const shared = new Nest(fakeWindow(), new MemoryStore([comment(1)]));
        await shared.connect(server);
        expect(server.calls[0]).toEqual(['Join']);
        await shared.apply([{ kind: 'snapshot', version: 1, previous: 1, capacity: 1000, ids: [id(3), id(2)], added: [], removed: [] }], server);
        await shared.reconciling;
        expect(server.calls.find(call => call[0] === 'Fetch')[1]).toEqual([id(3), id(2)]);
        expect(server.calls.find(call => call[0] === 'Offer')[1].map(entry => entry.id)).toEqual([id(1)]);
        expect([...shared.entries.keys()].sort()).toEqual([id(1), id(2), id(3)]);
        expect(shared.status).toBe('live');
        expect(shared.ready).toBe(true);
        expect(shared.isLive(id(1))).toBe(false);
        expect(shared.listings()).toHaveLength(1);
    });

    it('applies deltas and resyncs on a gap', async () => {
        const server = fakeServer([]);
        const shared = new Nest(fakeWindow(), new MemoryStore());
        await shared.connect(server);
        await shared.apply([{ kind: 'snapshot', version: 1, previous: 1, capacity: 1000, ids: [], added: [], removed: [] }], server);
        await shared.apply([{ kind: 'delta', version: 2, previous: 1, capacity: 0, ids: [], added: [comment(5)], removed: [] }], server);
        expect(shared.isLive(id(5))).toBe(true);
        await shared.apply([{ kind: 'delta', version: 3, previous: 2, capacity: 0, ids: [], added: [], removed: [id(5)] }], server);
        expect(shared.isLive(id(5))).toBe(false);
        expect(shared.entries.has(id(5))).toBe(true);
        const joins = server.calls.filter(call => call[0] === 'Join').length;
        await shared.apply([{ kind: 'delta', version: 9, previous: 7, capacity: 0, ids: [], added: [], removed: [] }], server);
        expect(server.calls.filter(call => call[0] === 'Join').length).toBe(joins + 1);
    });

    it('ignores updates from a detached island', async () => {
        const server = fakeServer([]);
        const shared = new Nest(fakeWindow(), new MemoryStore());
        await shared.connect(server);
        shared.disconnect(server);
        await shared.apply([{ kind: 'snapshot', version: 1, previous: 1, capacity: 1000, ids: [id(1)], added: [], removed: [] }], server);
        expect(shared.status).toBe('local');
        expect(shared.server.size).toBe(0);
    });

    it('posts only while live and marks its own entries', async () => {
        const server = fakeServer([]);
        const store = new MemoryStore();
        const shared = new Nest(fakeWindow(), store);
        expect((await shared.post({ kind: 'comment', anchors: ['zillow:1'], text: 'x' })).result).toBe('offline');
        await shared.connect(server);
        await shared.apply([{ kind: 'snapshot', version: 1, previous: 1, capacity: 1000, ids: [], added: [], removed: [] }], server);
        const outcome = await shared.post({ kind: 'comment', anchors: ['zillow:1'], text: 'mine' });
        expect(outcome.result).toBe('accepted');
        expect(shared.entries.get(id(900)).mine).toBe(true);
        expect(store.records.get(id(900)).mine).toBe(true);
        expect(shared.isLive(id(900))).toBe(true);
    });

    it('notifies listeners once per burst', async () => {
        const shared = new Nest(fakeWindow(), new MemoryStore([comment(1)]));
        const listener = vi.fn();
        shared.listen(listener);
        await shared.loaded;
        await Promise.resolve();
        expect(listener).toHaveBeenCalledTimes(1);
        await shared.clear();
        await Promise.resolve();
        expect(shared.entries.size).toBe(0);
    });
});
