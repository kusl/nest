import { compareKeys, keyLabel, parseLink } from './keys.js';
import { placeOf, streetOf } from './fields.js';

export function newestFirst(a, b) {
    return a.id === b.id ? 0 : a.id < b.id ? 1 : -1;
}

export function oldestFirst(a, b) {
    return -newestFirst(a, b);
}

export function timestampOf(id) {
    return Number.parseInt(id.slice(0, 8) + id.slice(9, 13), 16);
}

class KeySets {
    constructor() {
        this.parent = new Map();
    }

    find(key) {
        let root = key;
        while (this.parent.has(root) && this.parent.get(root) !== root) {
            root = this.parent.get(root);
        }
        let node = key;
        while (node !== root) {
            const next = this.parent.get(node);
            this.parent.set(node, root);
            node = next;
        }
        if (!this.parent.has(root)) {
            this.parent.set(root, root);
        }
        return root;
    }

    union(a, b) {
        const rootA = this.find(a);
        const rootB = this.find(b);
        if (rootA === rootB) {
            return;
        }
        if (compareKeys(rootA, rootB) <= 0) {
            this.parent.set(rootB, rootA);
        } else {
            this.parent.set(rootA, rootB);
        }
    }
}

export function titleOf(listing) {
    return streetOf(listing.fields) || placeOf(listing.fields) || keyLabel(listing.key);
}

export function resolve(entries) {
    const sorted = [...entries].sort(oldestFirst);
    const keys = new Set();
    const links = new Map();
    const fields = {};
    const history = {};
    const comments = [];
    for (const entry of sorted) {
        for (const key of entry.keys) {
            keys.add(key);
        }
        if (entry.kind === 'link' && typeof entry.url === 'string') {
            const parsed = parseLink(entry.url);
            if (parsed) {
                links.set(parsed.key, { key: parsed.key, provider: parsed.provider, url: entry.url, id: entry.id });
            }
        } else if (entry.kind === 'fact' && Array.isArray(entry.fields)) {
            for (const { key, value } of entry.fields) {
                (history[key] ??= []).push({ value, id: entry.id });
                fields[key] = value;
            }
        } else if (entry.kind === 'comment') {
            comments.push(entry);
        }
    }
    for (const key of Object.keys(fields)) {
        if (fields[key] === '') {
            delete fields[key];
        }
    }
    const sortedKeys = [...keys].sort(compareKeys);
    const listing = {
        key: sortedKeys[0],
        keys: sortedKeys,
        links: [...links.values()].sort((a, b) => compareKeys(a.key, b.key)),
        fields,
        history,
        comments,
        ids: sorted.map(entry => entry.id),
        created: sorted[0].id,
        updated: sorted.at(-1).id,
        mine: sorted.some(entry => entry.mine === true),
    };
    listing.title = titleOf(listing);
    return listing;
}

export function buildListings(entries) {
    const sets = new KeySets();
    const usable = [];
    for (const entry of entries) {
        if (!entry || !Array.isArray(entry.keys) || entry.keys.length === 0) {
            continue;
        }
        usable.push(entry);
        for (const key of entry.keys.slice(1)) {
            sets.union(entry.keys[0], key);
        }
    }
    const groups = new Map();
    for (const entry of usable) {
        const root = sets.find(entry.keys[0]);
        const group = groups.get(root);
        if (group) {
            group.push(entry);
        } else {
            groups.set(root, [entry]);
        }
    }
    return [...groups.values()]
        .map(resolve)
        .sort((a, b) => (a.updated === b.updated ? compareKeys(a.key, b.key) : a.updated < b.updated ? 1 : -1));
}

export function findListing(listings, key) {
    return listings.find(listing => listing.keys.includes(key)) ?? null;
}

export function matches(listing, query) {
    const needle = query.trim().toLowerCase();
    if (needle.length === 0) {
        return true;
    }
    const haystack = [listing.title, placeOf(listing.fields), listing.fields.mls ?? '', ...listing.keys, ...listing.links.map(link => link.url)]
        .join(' ')
        .toLowerCase();
    return needle.split(/\s+/).every(part => haystack.includes(part));
}
