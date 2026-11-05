import { EntryStore } from './store.js';
import { buildListings, newestFirst } from './merge.js';

export const OFFER_BYTES = 24000;
export const FETCH_CHUNK = 50;
export const PING_MS = 25000;
export const FALLBACK_MS = 3000;
export const RECONNECTED_EVENT = 'nest:reconnected';

export function needsResync(version, update) {
    return update.previous !== version;
}

export function wire(entry) {
    const result = { id: entry.id, kind: entry.kind, keys: entry.keys };
    if (typeof entry.url === 'string') {
        result.url = entry.url;
    }
    if (Array.isArray(entry.fields)) {
        result.fields = entry.fields;
    }
    if (typeof entry.text === 'string') {
        result.text = entry.text;
    }
    return result;
}

export function missingIds(ids, have) {
    return ids.filter(id => !have.has(id));
}

export function offerCandidates(local, serverIds, capacity) {
    let oldest = null;
    if (serverIds.size >= capacity) {
        for (const id of serverIds) {
            if (oldest === null || id < oldest) {
                oldest = id;
            }
        }
    }
    return local
        .filter(entry => !serverIds.has(entry.id) && (oldest === null || entry.id > oldest))
        .sort(newestFirst)
        .slice(0, capacity)
        .map(wire);
}

export function chunkBySize(items, maxBytes = OFFER_BYTES) {
    const encoder = new TextEncoder();
    const chunks = [];
    let chunk = [];
    let size = 0;
    for (const item of items) {
        const bytes = encoder.encode(JSON.stringify(item)).length + 1;
        if (bytes > maxBytes) {
            continue;
        }
        if (chunk.length > 0 && size + bytes > maxBytes) {
            chunks.push(chunk);
            chunk = [];
            size = 0;
        }
        chunk.push(item);
        size += bytes;
    }
    if (chunk.length > 0) {
        chunks.push(chunk);
    }
    return chunks;
}

export function chunk(items, size) {
    const chunks = [];
    for (let index = 0; index < items.length; index += size) {
        chunks.push(items.slice(index, index + size));
    }
    return chunks;
}

export class Nest {
    constructor(win, store = new EntryStore(win.indexedDB)) {
        this.win = win;
        this.store = store;
        this.entries = new Map();
        this.server = new Set();
        this.capacity = 0;
        this.version = -1;
        this.dotnet = null;
        this.status = 'local';
        this.ready = false;
        this.revision = 0;
        this.cached = null;
        this.cachedRevision = -1;
        this.listeners = new Set();
        this.joining = null;
        this.reconciling = null;
        this.timer = 0;
        this.fallback = win.setTimeout(() => this.markReady(), FALLBACK_MS);
        this.emitting = false;
        this.onReconnected = () => this.join();
        win.addEventListener(RECONNECTED_EVENT, this.onReconnected);
        this.loaded = this.load();
    }

    async load() {
        try {
            for (const record of await this.store.all()) {
                this.entries.set(record.id, record);
            }
        } catch {
        }
        this.changed();
    }

    listen(listener) {
        this.listeners.add(listener);
        return () => this.listeners.delete(listener);
    }

    changed() {
        this.revision += 1;
        if (this.emitting) {
            return;
        }
        this.emitting = true;
        queueMicrotask(() => {
            this.emitting = false;
            for (const listener of [...this.listeners]) {
                try {
                    listener(this);
                } catch {
                }
            }
        });
    }

    markReady() {
        this.win.clearTimeout(this.fallback);
        if (!this.ready) {
            this.ready = true;
            this.changed();
        }
    }

    setStatus(status) {
        if (this.status !== status) {
            this.status = status;
            this.changed();
        }
    }

    listings() {
        if (this.cachedRevision !== this.revision) {
            this.cached = buildListings(this.entries.values());
            this.cachedRevision = this.revision;
        }
        return this.cached;
    }

    isLive(id) {
        return this.server.has(id);
    }

    get live() {
        return this.status === 'live' && this.dotnet !== null;
    }

    async connect(dotnet) {
        this.dotnet = dotnet;
        this.setStatus('connecting');
        this.win.clearInterval(this.timer);
        this.timer = this.win.setInterval(() => this.ping(), PING_MS);
        await this.loaded;
        await this.join();
    }

    disconnect(dotnet) {
        if (this.dotnet !== dotnet) {
            return;
        }
        this.dotnet = null;
        this.version = -1;
        this.win.clearInterval(this.timer);
        this.setStatus('local');
    }

    join() {
        const dotnet = this.dotnet;
        if (!dotnet) {
            return Promise.resolve();
        }
        this.version = -1;
        this.joining ??= dotnet.invokeMethodAsync('Join')
            .catch(() => this.setStatus('offline'))
            .finally(() => {
                this.joining = null;
            });
        return this.joining;
    }

    async ping() {
        const dotnet = this.dotnet;
        if (!dotnet || this.joining) {
            return;
        }
        try {
            if (!(await dotnet.invokeMethodAsync('Ping'))) {
                await this.join();
            }
        } catch {
            this.setStatus('offline');
        }
    }

    async apply(updates, dotnet) {
        if (dotnet !== this.dotnet) {
            return;
        }
        for (const update of updates) {
            if (update.kind === 'snapshot') {
                this.snapshot(update);
            } else if (update.kind === 'delta' && this.version >= 0) {
                if (needsResync(this.version, update)) {
                    this.join();
                    return;
                }
                await this.delta(update);
            }
        }
    }

    snapshot(update) {
        this.version = update.version;
        this.capacity = update.capacity;
        this.server = new Set(update.ids);
        this.setStatus('live');
        this.changed();
        this.reconciling = this.reconcile(this.dotnet).finally(() => {
            this.reconciling = null;
        });
    }

    async reconcile(dotnet) {
        try {
            for (const ids of chunk(missingIds([...this.server], this.entries), FETCH_CHUNK)) {
                if (dotnet !== this.dotnet) {
                    return;
                }
                const fetched = await dotnet.invokeMethodAsync('Fetch', ids);
                await this.add(fetched ?? []);
            }
            const offers = offerCandidates([...this.entries.values()], this.server, this.capacity);
            for (const offered of chunkBySize(offers)) {
                if (dotnet !== this.dotnet) {
                    return;
                }
                await dotnet.invokeMethodAsync('Offer', offered);
            }
        } catch {
        } finally {
            this.markReady();
        }
    }

    async delta(update) {
        this.version = update.version;
        for (const id of update.removed) {
            this.server.delete(id);
        }
        for (const entry of update.added) {
            this.server.add(entry.id);
        }
        await this.add(update.added);
        this.changed();
    }

    async add(entries, mine = false) {
        const records = [];
        for (const entry of entries) {
            if (!entry || typeof entry.id !== 'string') {
                continue;
            }
            const known = this.entries.get(entry.id);
            if (known && !(mine && !known.mine)) {
                continue;
            }
            const record = wire(entry);
            if (mine || known?.mine) {
                record.mine = true;
            }
            this.entries.set(record.id, record);
            records.push(record);
        }
        if (records.length === 0) {
            return;
        }
        this.changed();
        try {
            for (const id of await this.store.save(records)) {
                this.entries.delete(id);
            }
        } catch {
        }
    }

    async post(draft) {
        const dotnet = this.dotnet;
        if (!dotnet || this.status !== 'live') {
            return { result: 'offline', entry: null, key: null };
        }
        try {
            const outcome = await dotnet.invokeMethodAsync('Post', draft);
            if (outcome?.entry) {
                this.server.add(outcome.entry.id);
                await this.add([outcome.entry], true);
            }
            return outcome ?? { result: 'invalid', entry: null, key: null };
        } catch {
            return { result: 'offline', entry: null, key: null };
        }
    }

    async clear() {
        this.entries.clear();
        this.server.clear();
        this.changed();
    }
}

let instance = null;

export function nest(win) {
    instance ??= new Nest(win);
    return instance;
}

export function current() {
    return instance;
}
