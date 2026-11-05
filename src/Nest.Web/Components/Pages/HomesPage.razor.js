import { addressKey, keyPath, parseLink, providerLabel } from '../../js/keys.js';
import { ADDRESS_FIELDS, normalizeField, placeOf, summaryOf } from '../../js/fields.js';
import { findListing, matches, timestampOf } from '../../js/merge.js';

export const PAGE_SIZE = 100;
export const MINE_KEY = 'nest-mine-only';

export const STATUS_TEXT = Object.freeze({
    local: 'Showing what this browser has. Connecting…',
    connecting: 'Connecting…',
    live: 'Live',
    offline: 'Offline. Showing what this browser has.',
});

export const RESULT_TEXT = Object.freeze({
    accepted: '',
    exists: '',
    empty: 'Fill in the street address and ZIP code.',
    invalid: 'That doesn\'t look like a listing link or an address.',
    tooFast: 'Slow down. Try again in a moment.',
    repeat: 'That was just added.',
    offline: 'Not connected to the server yet. Try again in a moment.',
});

export function timeLabel(id, format) {
    const at = new Date(timestampOf(id));
    try {
        return format ? format.format(at) : at.toISOString().slice(0, 16).replace('T', ' ');
    } catch {
        return at.toISOString().slice(0, 16).replace('T', ' ');
    }
}

export function isoOf(id) {
    return new Date(timestampOf(id)).toISOString();
}

export function previewText(parsed, local) {
    if (!parsed) {
        return '';
    }
    const what = parsed.provider === 'url' ? 'A link from another site' : `${providerLabel(parsed.provider)} home ${parsed.key.slice(parsed.key.indexOf(':') + 1).toUpperCase()}`;
    return local ? `${what}. Already here: ${local.title}.` : `${what}.`;
}

export function visibleListings(listings, query, mineOnly) {
    return listings.filter(listing => (!mineOnly || listing.mine) && matches(listing, query));
}

export function commentsLabel(count) {
    return count === 1 ? '1 comment' : `${count} comments`;
}

export function readMineOnly(storage) {
    try {
        return storage?.getItem(MINE_KEY) === 'on';
    } catch {
        return false;
    }
}

export function writeMineOnly(storage, on) {
    try {
        if (on) {
            storage?.setItem(MINE_KEY, 'on');
        } else {
            storage?.removeItem(MINE_KEY);
        }
    } catch {
    }
}

export function navigate(win, path) {
    if (typeof win.Blazor?.navigateTo === 'function') {
        win.Blazor.navigateTo(path);
    } else {
        win.location.assign(path);
    }
}

export function cardElement(doc, listing, live, format) {
    const item = doc.createElement('li');
    item.className = 'home';
    item.classList.toggle('archived', !live);
    item.classList.toggle('mine', listing.mine);
    item.dataset.key = listing.key;
    const link = doc.createElement('a');
    link.href = keyPath(listing.key);
    const title = doc.createElement('strong');
    title.className = 'title';
    title.textContent = listing.title;
    link.append(title);
    const place = placeOf(listing.fields);
    if (place && place !== listing.title) {
        const span = doc.createElement('span');
        span.className = 'place';
        span.textContent = place;
        link.append(span);
    }
    const summary = summaryOf(listing.fields);
    if (summary) {
        const span = doc.createElement('span');
        span.className = 'summary';
        span.textContent = summary;
        link.append(span);
    }
    const meta = doc.createElement('span');
    meta.className = 'meta';
    const sources = [...new Set(listing.links.map(entry => providerLabel(entry.provider)))];
    for (const text of [sources.join(' · '), commentsLabel(listing.comments.length)]) {
        if (text) {
            const span = doc.createElement('span');
            span.textContent = text;
            meta.append(span);
        }
    }
    const time = doc.createElement('time');
    time.dateTime = isoOf(listing.updated);
    time.textContent = timeLabel(listing.updated, format);
    meta.append(time);
    link.append(meta);
    item.append(link);
    return item;
}

class HomesView {
    constructor(root, shared, win) {
        this.root = root;
        this.nest = shared;
        this.win = win;
        this.doc = root.ownerDocument;
        this.format = new win.Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });
        this.limit = PAGE_SIZE;
        this.find = id => this.doc.getElementById(id);
        this.linkForm = this.find('add-link');
        this.linkInput = this.find('link-input');
        this.linkPreview = this.find('link-preview');
        this.addressForm = this.find('add-address');
        this.addressStatus = this.find('address-status');
        this.filter = this.find('homes-filter');
        this.mineOnly = this.find('homes-mine');
        this.list = this.find('homes-list');
        this.empty = this.find('homes-empty');
        this.more = this.find('homes-more');
        this.status = this.find('sync-status');
        this.busy = false;
        this.handlers = [
            [this.linkForm, 'submit', event => this.submitLink(event)],
            [this.linkInput, 'input', () => this.preview()],
            [this.addressForm, 'submit', event => this.submitAddress(event)],
            [this.filter, 'input', () => this.renderList(true)],
            [this.mineOnly, 'change', () => this.toggleMine()],
            [this.more, 'click', () => this.showMore()],
        ];
    }

    start() {
        for (const [element, type, handler] of this.handlers) {
            element?.addEventListener(type, handler);
        }
        if (this.mineOnly) {
            this.mineOnly.checked = readMineOnly(this.win.localStorage);
        }
        this.unlisten = this.nest.listen(() => this.render());
        this.render();
    }

    dispose() {
        this.unlisten?.();
        for (const [element, type, handler] of this.handlers) {
            element?.removeEventListener(type, handler);
        }
    }

    render() {
        if (this.status) {
            this.status.dataset.status = this.nest.status;
            this.status.textContent = STATUS_TEXT[this.nest.status] ?? '';
        }
        for (const button of this.root.querySelectorAll('form button[type="submit"]')) {
            button.disabled = this.busy || !this.nest.live;
        }
        if (this.nest.ready && (this.nest.live || this.nest.entries.size > 0)) {
            this.renderList(false);
        }
        this.preview();
    }

    renderList(resetLimit) {
        if (!this.list) {
            return;
        }
        if (resetLimit) {
            this.limit = PAGE_SIZE;
        }
        const listings = visibleListings(this.nest.listings(), this.filter?.value ?? '', this.mineOnly?.checked ?? false);
        const fragment = this.doc.createDocumentFragment();
        for (const listing of listings.slice(0, this.limit)) {
            fragment.append(cardElement(this.doc, listing, listing.ids.some(id => this.nest.isLive(id)), this.format));
        }
        this.list.replaceChildren(fragment);
        if (this.more) {
            this.more.hidden = listings.length <= this.limit;
        }
        if (this.empty) {
            this.empty.hidden = listings.length > 0;
            this.empty.textContent = this.nest.listings().length > 0 ? 'No homes match.' : 'No homes yet. Paste a link to start.';
        }
    }

    showMore() {
        this.limit += PAGE_SIZE;
        this.renderList(false);
    }

    toggleMine() {
        writeMineOnly(this.win.localStorage, this.mineOnly.checked);
        this.renderList(true);
    }

    preview() {
        if (!this.linkPreview || !this.linkInput) {
            return;
        }
        const value = this.linkInput.value.trim();
        const parsed = parseLink(value);
        const local = parsed ? findListing(this.nest.listings(), parsed.key) : null;
        this.linkPreview.textContent = value && !parsed ? 'Paste a full link that starts with https://' : previewText(parsed, local);
    }

    async submitLink(event) {
        event.preventDefault();
        const parsed = parseLink(this.linkInput?.value ?? '');
        if (!parsed) {
            this.linkPreview.textContent = RESULT_TEXT.invalid;
            return;
        }
        const local = findListing(this.nest.listings(), parsed.key);
        if (local) {
            navigate(this.win, keyPath(local.key));
            return;
        }
        await this.send({ kind: 'link', anchors: [], url: parsed.url }, this.linkPreview);
    }

    async submitAddress(event) {
        event.preventDefault();
        const form = this.addressForm;
        const values = {};
        for (const key of ADDRESS_FIELDS) {
            const result = normalizeField(key, form.elements.namedItem(key)?.value ?? '');
            if (!result.ok) {
                this.addressStatus.textContent = result.error;
                return;
            }
            values[key] = result.value;
        }
        const key = addressKey(values.street, values.unit, values.zip);
        if (!key) {
            this.addressStatus.textContent = RESULT_TEXT.empty;
            return;
        }
        const local = findListing(this.nest.listings(), key);
        if (local) {
            navigate(this.win, keyPath(local.key));
            return;
        }
        const fields = ADDRESS_FIELDS.filter(name => values[name]).map(name => ({ key: name, value: values[name] }));
        await this.send({ kind: 'fact', anchors: [], fields }, this.addressStatus);
    }

    async send(draft, output) {
        this.busy = true;
        this.render();
        try {
            const outcome = await this.nest.post(draft);
            if ((outcome.result === 'accepted' || outcome.result === 'exists') && outcome.key) {
                output.textContent = '';
                navigate(this.win, keyPath(outcome.key));
                return;
            }
            output.textContent = RESULT_TEXT[outcome.result] ?? RESULT_TEXT.invalid;
        } finally {
            this.busy = false;
            this.render();
        }
    }
}

export function mount(root, shared, win) {
    const view = new HomesView(root, shared, win);
    view.start();
    return { dispose: () => view.dispose() };
}
