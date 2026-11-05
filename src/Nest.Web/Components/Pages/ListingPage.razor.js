import { compareKeys, guessAddress, keyLabel, keyUrl, parseLink, providerLabel, providerOf } from '../../js/keys.js';
import { ADDRESS_FIELDS, FIELDS, changedFields, displayValue, placeOf, summaryOf } from '../../js/fields.js';
import { findListing } from '../../js/merge.js';
import { readDraft, writeDraft } from '../../js/store.js';
import { RESULT_TEXT, STATUS_TEXT, isoOf, timeLabel } from './HomesPage.razor.js';

export const COMMENT_MAX = 4000;

export function anchorsOf(listing, key) {
    const keys = listing ? [...listing.keys] : [key];
    return keys.sort(compareKeys).slice(0, 8);
}

export function previousValue(history, key, current) {
    const values = history?.[key] ?? [];
    for (let index = values.length - 1; index >= 0; index -= 1) {
        if (values[index].value !== current && values[index].value !== '') {
            return values[index].value;
        }
    }
    return null;
}

export function addressSuggestion(listing) {
    if (!listing || listing.fields.street || listing.fields.zip) {
        return null;
    }
    for (const link of listing.links) {
        const guess = guessAddress(link.url);
        if (guess && (guess.street || guess.zip)) {
            return guess;
        }
    }
    return null;
}

export function countText(length, max) {
    return `${length} / ${max}`;
}

class ListingView {
    constructor(root, shared, win) {
        this.root = root;
        this.nest = shared;
        this.win = win;
        this.doc = root.ownerDocument;
        this.key = root.dataset.key;
        this.format = new win.Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });
        this.find = id => this.doc.getElementById(id);
        this.title = this.find('listing-title');
        this.place = this.find('listing-place');
        this.summary = this.find('listing-summary');
        this.status = this.find('sync-status');
        this.missing = this.find('listing-missing');
        this.missingText = this.find('listing-missing-text');
        this.claim = this.find('listing-claim');
        this.links = this.find('listing-links');
        this.linkForm = this.find('add-more-link');
        this.linkInput = this.find('more-link-input');
        this.linkStatus = this.find('more-link-status');
        this.facts = this.find('listing-facts');
        this.factsEmpty = this.find('facts-empty');
        this.factsForm = this.find('facts-form');
        this.factsStatus = this.find('facts-status');
        this.suggestion = this.find('facts-suggestion');
        this.comments = this.find('comments');
        this.commentsEmpty = this.find('comments-empty');
        this.commentsCount = this.find('comments-count');
        this.commentForm = this.find('comment-form');
        this.commentText = this.find('comment-text');
        this.commentCount = this.find('comment-count');
        this.commentStatus = this.find('comment-status');
        this.keys = this.find('listing-keys');
        this.listing = null;
        this.shown = new Map();
        this.dirty = false;
        this.busy = false;
        this.seen = null;
        this.handlers = [
            [this.linkForm, 'submit', event => this.submitLink(event)],
            [this.factsForm, 'submit', event => this.submitFacts(event)],
            [this.factsForm, 'input', () => {
                this.dirty = true;
            }],
            [this.commentForm, 'submit', event => this.submitComment(event)],
            [this.commentText, 'input', () => this.commentInput()],
            [this.commentText, 'keydown', event => this.commentKey(event)],
            [this.claim, 'click', () => this.claimHome()],
        ];
    }

    start() {
        for (const [element, type, handler] of this.handlers) {
            element?.addEventListener(type, handler);
        }
        if (this.commentText && !this.commentText.value) {
            this.commentText.value = readDraft(this.win.localStorage, this.key);
        }
        this.commentInput(false);
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
        if (this.claim) {
            this.claim.disabled = this.busy || !this.nest.live;
        }
        const listing = findListing(this.nest.listings(), this.key);
        if (!listing) {
            if (this.nest.ready && this.nest.live) {
                this.renderMissing();
            }
            return;
        }
        this.listing = listing;
        if (this.missing) {
            this.missing.hidden = true;
        }
        if (this.claim) {
            this.claim.hidden = true;
        }
        this.doc.title = `${listing.title} · Nest`;
        this.title.textContent = listing.title;
        this.setText(this.place, placeOf(listing.fields) === listing.title ? '' : placeOf(listing.fields));
        this.setText(this.summary, summaryOf(listing.fields));
        this.renderLinks(listing);
        this.renderFacts(listing);
        this.renderForm(listing);
        this.renderComments(listing);
        this.renderKeys(listing);
    }

    setText(element, text) {
        if (element) {
            element.textContent = text;
            element.hidden = text.length === 0;
        }
    }

    renderMissing() {
        if (this.missing) {
            this.missing.hidden = false;
            this.missingText.textContent = 'Nobody connected has this home right now, and this browser has never seen it.';
        }
        if (this.claim) {
            this.claim.hidden = keyUrl(this.key) === null;
        }
    }

    renderLinks(listing) {
        if (!this.links) {
            return;
        }
        const fragment = this.doc.createDocumentFragment();
        for (const link of listing.links) {
            const item = this.doc.createElement('li');
            const anchor = this.doc.createElement('a');
            anchor.href = link.url;
            anchor.target = '_blank';
            anchor.rel = 'noopener noreferrer';
            anchor.textContent = `Open on ${providerLabel(link.provider)}`;
            const where = this.doc.createElement('span');
            where.className = 'url';
            where.textContent = link.url.replace(/^https?:\/\//, '');
            item.append(anchor, where);
            fragment.append(item);
        }
        this.links.replaceChildren(fragment);
    }

    renderFacts(listing) {
        if (!this.facts) {
            return;
        }
        const fragment = this.doc.createDocumentFragment();
        for (const field of FIELDS) {
            const value = listing.fields[field.key];
            if (!value) {
                continue;
            }
            const row = this.doc.createElement('div');
            const term = this.doc.createElement('dt');
            term.textContent = field.label;
            const detail = this.doc.createElement('dd');
            detail.textContent = displayValue(field.key, value);
            const previous = previousValue(listing.history, field.key, value);
            if (previous !== null) {
                const was = this.doc.createElement('small');
                was.className = 'was';
                was.textContent = `was ${displayValue(field.key, previous)}`;
                detail.append(' ', was);
            }
            row.append(term, detail);
            fragment.append(row);
        }
        this.facts.replaceChildren(fragment);
        this.facts.hidden = this.facts.childElementCount === 0;
        if (this.factsEmpty) {
            this.factsEmpty.hidden = this.facts.childElementCount > 0;
        }
    }

    renderForm(listing) {
        if (!this.factsForm || this.dirty) {
            return;
        }
        const suggestion = addressSuggestion(listing);
        for (const field of FIELDS) {
            const input = this.factsForm.elements.namedItem(field.key);
            if (!input) {
                continue;
            }
            const suggested = suggestion && ADDRESS_FIELDS.includes(field.key) ? suggestion[field.key] ?? '' : '';
            input.value = listing.fields[field.key] ?? suggested;
        }
        if (this.suggestion) {
            this.suggestion.hidden = !suggestion;
            this.suggestion.textContent = suggestion
                ? suggestion.mixed
                    ? 'The address below comes from the link, which mixes the street and the city. Fix it, then save.'
                    : 'The address below comes from the link. Check it, then save.'
                : '';
        }
    }

    commentElement(comment, fresh) {
        const item = this.doc.createElement('li');
        item.className = 'comment';
        item.dataset.id = comment.id;
        item.classList.toggle('fresh', fresh);
        const head = this.doc.createElement('div');
        head.className = 'head';
        const time = this.doc.createElement('time');
        time.dateTime = isoOf(comment.id);
        time.textContent = timeLabel(comment.id, this.format);
        head.append(time);
        if (comment.mine) {
            const yours = this.doc.createElement('span');
            yours.className = 'yours';
            yours.textContent = 'You';
            head.append(yours);
        }
        const text = this.doc.createElement('p');
        text.className = 'text';
        text.textContent = comment.text;
        item.append(head, text);
        return item;
    }

    renderComments(listing) {
        if (!this.comments) {
            return;
        }
        const first = this.seen === null;
        const seen = this.seen ?? new Set();
        const next = new Map();
        const arrived = [];
        for (const comment of listing.comments) {
            let item = this.shown.get(comment.id);
            const mine = comment.mine === true;
            if (!item || item.classList.contains('mine') !== mine) {
                item = this.commentElement(comment, !first && !seen.has(comment.id));
                item.classList.toggle('mine', mine);
            }
            if (!first && !seen.has(comment.id) && !mine) {
                arrived.push(comment);
            }
            item.classList.toggle('archived', !this.nest.isLive(comment.id));
            next.set(comment.id, item);
            seen.add(comment.id);
        }
        this.comments.replaceChildren(...next.values());
        this.shown = next;
        this.seen = seen;
        if (this.commentsEmpty) {
            this.commentsEmpty.hidden = listing.comments.length > 0;
        }
        if (this.commentsCount) {
            this.commentsCount.textContent = listing.comments.length > 0 ? `(${listing.comments.length})` : '';
        }
        if (arrived.length > 0) {
            const announcer = this.doc.getElementById('announcer');
            if (announcer) {
                announcer.textContent = `New comment: ${arrived.at(-1).text.slice(0, 140)}`;
            }
        }
    }

    renderKeys(listing) {
        if (!this.keys) {
            return;
        }
        const fragment = this.doc.createDocumentFragment();
        for (const key of listing.keys) {
            const item = this.doc.createElement('li');
            const provider = this.doc.createElement('span');
            provider.className = 'provider';
            provider.textContent = providerLabel(providerOf(key));
            const value = this.doc.createElement('code');
            const label = keyLabel(key);
            value.textContent = providerOf(key) === 'address' ? label : label.slice(label.indexOf(' ') + 1);
            item.append(provider, ' ', value);
            fragment.append(item);
        }
        this.keys.replaceChildren(fragment);
    }

    commentInput(save = true) {
        if (!this.commentText) {
            return;
        }
        const length = this.commentText.value.length;
        if (this.commentCount) {
            this.commentCount.textContent = countText(length, COMMENT_MAX);
            this.commentCount.classList.toggle('full', length >= COMMENT_MAX);
        }
        if (save) {
            writeDraft(this.win.localStorage, this.key, this.commentText.value);
        }
    }

    commentKey(event) {
        if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
            event.preventDefault();
            this.commentForm?.requestSubmit();
        }
    }

    async send(draft, output) {
        this.busy = true;
        this.render();
        try {
            const outcome = await this.nest.post(draft);
            const ok = outcome.result === 'accepted' || outcome.result === 'exists';
            if (output) {
                output.textContent = ok ? '' : RESULT_TEXT[outcome.result] ?? RESULT_TEXT.invalid;
            }
            return ok ? outcome : null;
        } finally {
            this.busy = false;
            this.render();
        }
    }

    anchors() {
        return anchorsOf(this.listing, this.key);
    }

    async submitComment(event) {
        event.preventDefault();
        const text = this.commentText?.value ?? '';
        if (text.trim().length === 0) {
            this.commentStatus.textContent = 'Write something first.';
            return;
        }
        const outcome = await this.send({ kind: 'comment', anchors: this.anchors(), text }, this.commentStatus);
        if (outcome) {
            this.commentText.value = '';
            this.commentInput();
        }
    }

    async submitFacts(event) {
        event.preventDefault();
        const inputs = {};
        for (const field of FIELDS) {
            const input = this.factsForm.elements.namedItem(field.key);
            if (input) {
                inputs[field.key] = input.value;
            }
        }
        for (const element of this.factsForm.querySelectorAll('.error')) {
            element.textContent = '';
        }
        const { changes, errors } = changedFields(this.listing?.fields ?? {}, inputs);
        if (errors.length > 0) {
            for (const { key, error } of errors) {
                const output = this.find(`fact-${key}-error`);
                if (output) {
                    output.textContent = error;
                }
            }
            this.factsStatus.textContent = 'Fix the highlighted values.';
            this.factsForm.elements.namedItem(errors[0].key)?.focus();
            return;
        }
        if (changes.length === 0) {
            this.factsStatus.textContent = 'Nothing changed.';
            return;
        }
        const outcome = await this.send({ kind: 'fact', anchors: this.anchors(), fields: changes }, this.factsStatus);
        if (outcome) {
            this.dirty = false;
            this.factsStatus.textContent = 'Saved.';
            this.render();
        }
    }

    async submitLink(event) {
        event.preventDefault();
        const parsed = parseLink(this.linkInput?.value ?? '');
        if (!parsed) {
            this.linkStatus.textContent = 'Paste a full link that starts with https://';
            return;
        }
        if (this.listing?.keys.includes(parsed.key)) {
            this.linkStatus.textContent = 'This home already has that link.';
            return;
        }
        const outcome = await this.send({ kind: 'link', anchors: this.anchors(), url: parsed.url }, this.linkStatus);
        if (outcome) {
            this.linkInput.value = '';
            this.linkStatus.textContent = 'Link added.';
        }
    }

    async claimHome() {
        const url = keyUrl(this.key);
        if (url) {
            await this.send({ kind: 'link', anchors: [], url }, this.missingText);
        }
    }
}

export function mount(root, shared, win) {
    const view = new ListingView(root, shared, win);
    view.start();
    return { dispose: () => view.dispose() };
}
