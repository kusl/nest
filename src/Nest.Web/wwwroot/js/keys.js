export const PROVIDERS = Object.freeze(['zillow', 'redfin', 'realtor', 'address', 'url']);
export const MAX_KEYS = 8;
export const MAX_URL = 2048;
export const MAX_ADDRESS_TOKEN = 100;

const LABELS = Object.freeze({
    zillow: 'Zillow',
    redfin: 'Redfin',
    realtor: 'Realtor.com',
    address: 'Address',
    url: 'Link',
});

const TOKEN_PATTERNS = Object.freeze({
    zillow: /^[1-9][0-9]{0,11}$/,
    redfin: /^[1-9][0-9]{0,11}$/,
    realtor: /^m[0-9]{1,12}-[0-9]{1,12}$/,
    address: /^[0-9]{5}(?:-[a-z0-9]+)+$/,
    url: /^[0-9a-f]{16}$/,
});

const URL_PATTERN = /[hH][tT][tT][pP][sS]?:\/\/[\x21\x23-\x26\x28-\x3B\x3D\x3F-\x7E]+/;
const PARTS_PATTERN = /^([hH][tT][tT][pP][sS]?):\/\/([^/?#]+)([^?#]*)/;
const HOST_PATTERN = /^[a-z0-9.-]+(?::[0-9]{1,5})?$/;
const TRAILING = '.,;:!?)]}';

export const STREET_WORDS = Object.freeze({
    alley: 'aly',
    avenue: 'ave',
    av: 'ave',
    boulevard: 'blvd',
    circle: 'cir',
    court: 'ct',
    cove: 'cv',
    crescent: 'cres',
    creek: 'crk',
    crossing: 'xing',
    drive: 'dr',
    expressway: 'expy',
    freeway: 'fwy',
    glen: 'gln',
    heights: 'hts',
    highway: 'hwy',
    hill: 'hl',
    hollow: 'holw',
    junction: 'jct',
    lane: 'ln',
    loop: 'loop',
    manor: 'mnr',
    mount: 'mt',
    mountain: 'mtn',
    parkway: 'pkwy',
    place: 'pl',
    plaza: 'plz',
    point: 'pt',
    ridge: 'rdg',
    road: 'rd',
    route: 'rte',
    rt: 'rte',
    square: 'sq',
    street: 'st',
    terrace: 'ter',
    trail: 'trl',
    turnpike: 'tpke',
    valley: 'vly',
    view: 'vw',
    village: 'vlg',
    north: 'n',
    south: 's',
    east: 'e',
    west: 'w',
    northeast: 'ne',
    northwest: 'nw',
    southeast: 'se',
    southwest: 'sw',
});

export const UNIT_WORDS = Object.freeze(['apt', 'apartment', 'unit', 'suite', 'ste']);

export function providerOf(key) {
    const colon = typeof key === 'string' ? key.indexOf(':') : -1;
    return colon > 0 ? key.slice(0, colon) : null;
}

export function tokenOf(key) {
    const colon = typeof key === 'string' ? key.indexOf(':') : -1;
    return colon > 0 ? key.slice(colon + 1) : null;
}

export function isKeyParts(provider, token) {
    const pattern = Object.hasOwn(TOKEN_PATTERNS, provider) ? TOKEN_PATTERNS[provider] : null;
    return pattern !== null
        && typeof token === 'string'
        && token.length <= MAX_ADDRESS_TOKEN
        && pattern.test(token);
}

export function isKey(key) {
    return isKeyParts(providerOf(key), tokenOf(key));
}

export function keyOf(provider, token) {
    return isKeyParts(provider, token) ? `${provider}:${token}` : null;
}

export function rankOf(key) {
    const rank = PROVIDERS.indexOf(providerOf(key));
    return rank < 0 ? PROVIDERS.length : rank;
}

export function compareKeys(a, b) {
    const rank = rankOf(a) - rankOf(b);
    if (rank !== 0) {
        return rank;
    }
    return a < b ? -1 : a > b ? 1 : 0;
}

export function providerLabel(provider) {
    return LABELS[provider] ?? 'Link';
}

export function keyLabel(key) {
    const provider = providerOf(key);
    const token = tokenOf(key) ?? '';
    switch (provider) {
        case 'zillow':
        case 'redfin':
            return `${providerLabel(provider)} ${token}`;
        case 'realtor':
            return `Realtor.com ${token.toUpperCase()}`;
        case 'address':
            return `${token.slice(6).replaceAll('-', ' ')}, ${token.slice(0, 5)}`;
        default:
            return `Link ${token.slice(0, 6)}`;
    }
}

export function keyPath(key) {
    return isKey(key) ? `homes/${providerOf(key)}/${tokenOf(key)}` : null;
}

export function keyUrl(key) {
    const token = tokenOf(key);
    switch (providerOf(key)) {
        case 'zillow':
            return `https://www.zillow.com/homedetails/${token}_zpid/`;
        case 'redfin':
            return `https://www.redfin.com/home/${token}`;
        default:
            return null;
    }
}

export function normalizeKeys(derived, anchors) {
    const keys = [];
    for (const key of [...(derived ?? []), ...(anchors ?? [])]) {
        if (keys.length >= MAX_KEYS) {
            break;
        }
        if (isKey(key) && !keys.includes(key)) {
            keys.push(key);
        }
    }
    return keys.sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
}

export function fnv1a64(text) {
    let hash = 0xcbf29ce484222325n;
    for (const byte of new TextEncoder().encode(text)) {
        hash ^= BigInt(byte);
        hash = (hash * 0x100000001b3n) & 0xffffffffffffffffn;
    }
    return hash.toString(16).padStart(16, '0');
}

export function extractUrl(text) {
    if (typeof text !== 'string') {
        return null;
    }
    const match = URL_PATTERN.exec(text);
    if (!match) {
        return null;
    }
    let url = match[0];
    while (url.length > 0 && TRAILING.includes(url.at(-1))) {
        url = url.slice(0, -1);
    }
    return url.length <= MAX_URL ? url : null;
}

export function lowerAscii(text) {
    return text.replace(/[A-Z]/g, char => String.fromCharCode(char.charCodeAt(0) + 32));
}

function isSite(site, domain) {
    return site === domain || site.endsWith(`.${domain}`);
}

function stripZeros(digits) {
    const trimmed = digits.replace(/^0+/, '');
    return trimmed.length > 0 ? trimmed : '0';
}

export function parseLink(text) {
    const raw = extractUrl(text);
    const parts = raw ? PARTS_PATTERN.exec(raw) : null;
    if (!parts) {
        return null;
    }
    const scheme = parts[1].length === 5 ? 'https' : 'http';
    const host = lowerAscii(parts[2]);
    const path = parts[3].length > 0 ? parts[3] : '/';
    if (!HOST_PATTERN.test(host)) {
        return null;
    }
    const site = host.replace(/:[0-9]+$/, '').replace(/^www\./, '');
    if (!site.includes('.')) {
        return null;
    }

    let provider = null;
    let token = null;
    if (isSite(site, 'zillow.com')) {
        const match = /([0-9]+)_[zZ][pP][iI][dD]/.exec(path);
        provider = match ? 'zillow' : null;
        token = match ? stripZeros(match[1]) : null;
    } else if (isSite(site, 'redfin.com')) {
        const match = /\/home\/([0-9]+)/.exec(path);
        provider = match ? 'redfin' : null;
        token = match ? stripZeros(match[1]) : null;
    } else if (isSite(site, 'realtor.com')) {
        const match = /_[mM]([0-9]+)-([0-9]+)/.exec(path);
        provider = match ? 'realtor' : null;
        token = match ? `m${match[1]}-${match[2]}` : null;
    }

    const key = provider ? keyOf(provider, token) : null;
    if (key) {
        return { key, provider, url: `https://${host}${path}` };
    }
    const canonical = `${site}${path.replace(/\/+$/, '')}`;
    return { key: `url:${fnv1a64(canonical)}`, provider: 'url', url: `${scheme}://${host}${path}` };
}

export function words(text) {
    let current = '';
    const result = [];
    for (const char of typeof text === 'string' ? text : '') {
        const code = char.charCodeAt(0);
        if (code >= 65 && code <= 90) {
            current += String.fromCharCode(code + 32);
        } else if ((code >= 97 && code <= 122) || (code >= 48 && code <= 57)) {
            current += char;
        } else if (current.length > 0) {
            result.push(current);
            current = '';
        }
    }
    if (current.length > 0) {
        result.push(current);
    }
    return result;
}

function unitMarked(text) {
    return typeof text === 'string' ? text.replaceAll('#', ' unit ') : '';
}

export function addressKey(street, unit, zip) {
    if (typeof zip !== 'string' || !/^[0-9]{5}$/.test(zip)) {
        return null;
    }
    const tokens = words(unitMarked(street));
    const split = tokens.findIndex((token, index) => index > 0 && UNIT_WORDS.includes(token));
    const streetTokens = (split > 0 ? tokens.slice(0, split) : tokens).map(token => STREET_WORDS[token] ?? token);
    if (streetTokens.length === 0) {
        return null;
    }
    const unitInput = words(unitMarked(unit));
    const unitSource = unitInput.length > 0 ? unitInput : split > 0 ? tokens.slice(split) : [];
    const unitTokens = unitSource.filter(token => !UNIT_WORDS.includes(token));
    const parts = [zip, ...streetTokens];
    if (unitTokens.length > 0) {
        parts.push('unit', ...unitTokens);
    }
    return keyOf('address', parts.join('-'));
}

function decode(text) {
    try {
        return decodeURIComponent(text);
    } catch {
        return text;
    }
}

function spaced(text) {
    return decode(text).replaceAll('-', ' ').replace(/\s+/g, ' ').trim();
}

function splitZip(tokens) {
    const zip = tokens.length > 1 && /^[0-9]{5}$/.test(tokens.at(-1)) ? tokens.at(-1) : '';
    return { zip, rest: zip ? tokens.slice(0, -1) : tokens };
}

export function guessAddress(url) {
    const parts = typeof url === 'string' ? PARTS_PATTERN.exec(url) : null;
    if (!parts) {
        return null;
    }
    const host = parts[2].toLowerCase();
    const segments = parts[3].split('/').filter(segment => segment.length > 0);

    if (host.endsWith('zillow.com')) {
        const index = segments.indexOf('homedetails');
        if (index < 0 || index + 2 >= segments.length) {
            return null;
        }
        const { zip, rest } = splitZip(decode(segments[index + 1]).split('-').filter(Boolean));
        const state = zip && rest.length > 1 && /^[A-Za-z]{2}$/.test(rest.at(-1)) ? rest.at(-1).toUpperCase() : '';
        const street = (state ? rest.slice(0, -1) : rest).join(' ');
        return street || zip ? { street, unit: '', city: '', state, zip, mixed: true } : null;
    }

    if (host.endsWith('redfin.com')) {
        const index = segments.indexOf('home');
        if (index < 3) {
            return null;
        }
        const { zip, rest } = splitZip(decode(segments[2]).split('-').filter(Boolean));
        const unitSegment = index > 3 ? segments[3] : '';
        const unit = /^unit-/i.test(unitSegment) ? spaced(unitSegment.slice(5)) : '';
        const state = /^[A-Za-z]{2}$/.test(segments[0]) ? segments[0].toUpperCase() : '';
        return { street: rest.join(' '), unit, city: spaced(segments[1]), state, zip, mixed: false };
    }

    if (host.endsWith('realtor.com')) {
        const detail = segments.find(segment => /_M[0-9]+-[0-9]+/i.test(segment));
        const fields = detail ? detail.split('_') : [];
        if (fields.length < 5) {
            return null;
        }
        const zip = /^[0-9]{5}$/.test(fields[3]) ? fields[3] : '';
        const state = /^[A-Za-z]{2}$/.test(fields[2]) ? fields[2].toUpperCase() : '';
        return { street: spaced(fields[0]), unit: '', city: spaced(fields[1]), state, zip, mixed: false };
    }

    return null;
}
