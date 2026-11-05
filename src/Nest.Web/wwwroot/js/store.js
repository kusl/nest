export const DB_NAME = 'nest';
export const DB_VERSION = 1;
export const STORE = 'entries';
export const LOCAL_CAP = 20000;
export const DRAFT_PREFIX = 'nest-draft:';

export const migrations = [
    db => db.createObjectStore(STORE, { keyPath: 'id' }),
];

export const SCHEMA = Object.freeze({ name: DB_NAME, version: DB_VERSION, store: STORE, migrations, cap: LOCAL_CAP });

export function upgrade(db, oldVersion, newVersion = DB_VERSION, steps = migrations) {
    for (let version = oldVersion; version < newVersion; version += 1) {
        steps[version](db);
    }
}

export function excessOf(count, cap = LOCAL_CAP) {
    return Math.max(0, count - cap);
}

function settle(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

function finished(transaction) {
    return new Promise((resolve, reject) => {
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        transaction.onabort = () => reject(transaction.error);
    });
}

export function deleteDatabase(factory, name = DB_NAME) {
    return new Promise((resolve, reject) => {
        if (!factory) {
            resolve('deleted');
            return;
        }
        const request = factory.deleteDatabase(name);
        request.onsuccess = () => resolve('deleted');
        request.onerror = () => reject(request.error);
        request.onblocked = () => resolve('blocked');
    });
}

export function openDatabase(factory, schema = SCHEMA, onClose = () => {}, allowReset = true) {
    return new Promise((resolve, reject) => {
        const request = factory.open(schema.name, schema.version);
        request.onupgradeneeded = event => upgrade(request.result, event.oldVersion, event.newVersion ?? schema.version, schema.migrations);
        request.onsuccess = () => {
            const db = request.result;
            db.onversionchange = () => {
                db.close();
                onClose();
            };
            db.onclose = onClose;
            resolve(db);
        };
        request.onerror = event => {
            if (allowReset && request.error?.name === 'VersionError') {
                event.preventDefault();
                deleteDatabase(factory, schema.name)
                    .then(() => openDatabase(factory, schema, onClose, false))
                    .then(resolve, reject);
                return;
            }
            reject(request.error);
        };
    });
}

export function draftKeys(storage) {
    const keys = [];
    try {
        for (let index = 0; index < storage.length; index += 1) {
            const key = storage.key(index);
            if (key?.startsWith(DRAFT_PREFIX)) {
                keys.push(key);
            }
        }
    } catch {
    }
    return keys;
}

export function readDraft(storage, key) {
    try {
        return storage?.getItem(DRAFT_PREFIX + key) ?? '';
    } catch {
        return '';
    }
}

export function writeDraft(storage, key, text) {
    try {
        if (text) {
            storage?.setItem(DRAFT_PREFIX + key, text);
        } else {
            storage?.removeItem(DRAFT_PREFIX + key);
        }
    } catch {
    }
}

export class EntryStore {
    constructor(factory, schema = SCHEMA) {
        this.factory = factory ?? null;
        this.schema = schema;
        this.db = null;
        this.opening = null;
    }

    database() {
        if (this.db) {
            return Promise.resolve(this.db);
        }
        if (!this.factory) {
            return Promise.resolve(null);
        }
        this.opening ??= openDatabase(this.factory, this.schema, () => {
            this.db = null;
        })
            .then(db => {
                this.db = db;
                return db;
            }, () => {
                this.factory = null;
                return null;
            })
            .finally(() => {
                this.opening = null;
            });
        return this.opening;
    }

    async all() {
        const db = await this.database();
        if (!db) {
            return [];
        }
        return settle(db.transaction(this.schema.store, 'readonly').objectStore(this.schema.store).getAll());
    }

    async save(records) {
        const db = await this.database();
        if (!db || records.length === 0) {
            return [];
        }
        const transaction = db.transaction(this.schema.store, 'readwrite');
        const done = finished(transaction);
        const store = transaction.objectStore(this.schema.store);
        for (const record of records) {
            store.put(record);
        }
        let evicted = [];
        const excess = excessOf(await settle(store.count()), this.schema.cap);
        if (excess > 0) {
            evicted = await settle(store.getAllKeys(null, excess));
            store.delete(IDBKeyRange.upperBound(evicted.at(-1)));
        }
        await done;
        return evicted;
    }
}
