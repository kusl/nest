import { lowerAscii } from './keys.js';

export const STATUS_OPTIONS = Object.freeze(['For sale', 'Coming soon', 'Pending', 'Sold', 'For rent', 'Off market']);
export const TYPE_OPTIONS = Object.freeze(['House', 'Condo', 'Townhouse', 'Multi-family', 'Manufactured', 'Land', 'Other']);

export const FIELDS = Object.freeze([
    { key: 'price', label: 'Price', kind: 'money', hint: '450000 or 450k' },
    { key: 'status', label: 'Status', kind: 'choice', options: STATUS_OPTIONS },
    { key: 'type', label: 'Type', kind: 'choice', options: TYPE_OPTIONS },
    { key: 'beds', label: 'Bedrooms', kind: 'count' },
    { key: 'baths', label: 'Bathrooms', kind: 'decimal', hint: '2.5' },
    { key: 'sqft', label: 'Square feet', kind: 'int', min: 1, max: 10000000 },
    { key: 'lot', label: 'Lot size', kind: 'text', max: 40, hint: '0.25 acres' },
    { key: 'year', label: 'Year built', kind: 'int', min: 1600, max: 2100 },
    { key: 'hoa', label: 'HOA per month', kind: 'money' },
    { key: 'taxes', label: 'Taxes per year', kind: 'money' },
    { key: 'parking', label: 'Parking', kind: 'text', max: 60 },
    { key: 'heating', label: 'Heating and cooling', kind: 'text', max: 60 },
    { key: 'mls', label: 'MLS number', kind: 'text', max: 40 },
    { key: 'street', label: 'Street address', kind: 'text', max: 120 },
    { key: 'unit', label: 'Unit', kind: 'text', max: 20 },
    { key: 'city', label: 'City', kind: 'text', max: 60 },
    { key: 'state', label: 'State', kind: 'state' },
    { key: 'zip', label: 'ZIP code', kind: 'zip' },
]);

export const ADDRESS_FIELDS = Object.freeze(['street', 'unit', 'city', 'state', 'zip']);

const BY_KEY = new Map(FIELDS.map(field => [field.key, field]));
const ORDER = new Map(FIELDS.map((field, index) => [field.key, index]));

const ERRORS = Object.freeze({
    money: 'Use a whole dollar amount like 450000, 450k or 1.2m.',
    count: 'Use a whole number from 0 to 99.',
    decimal: 'Use a number from 0 to 99 like 2 or 2.5.',
    int: 'Use a whole number.',
    choice: 'Pick one of the listed options.',
    state: 'Use the two-letter state code, like NY.',
    zip: 'Use a five-digit ZIP code.',
});

export function fieldOf(key) {
    return BY_KEY.get(key) ?? null;
}

export function fieldOrder(key) {
    return ORDER.get(key) ?? -1;
}

export function cleanText(input, max) {
    let text = '';
    for (const char of typeof input === 'string' ? input : '') {
        const code = char.charCodeAt(0);
        const space = (code >= 0x09 && code <= 0x0d) || code === 0x20 || code === 0xa0 || code === 0x1680 || (code >= 0x2000 && code <= 0x200a)
            || code === 0x202f || code === 0x205f || code === 0x3000;
        if (space) {
            if (text.length > 0 && !text.endsWith(' ')) {
                text += ' ';
            }
        } else if (code > 0x1f && (code < 0x7f || code > 0x9f)) {
            text += char;
        }
    }
    if (text.endsWith(' ')) {
        text = text.slice(0, -1);
    }
    if (text.length > max) {
        let cut = max;
        const code = text.charCodeAt(cut - 1);
        if (code >= 0xd800 && code <= 0xdbff) {
            cut -= 1;
        }
        text = text.slice(0, cut);
        if (text.endsWith(' ')) {
            text = text.slice(0, -1);
        }
    }
    return text;
}

function stripZeros(digits) {
    const trimmed = digits.replace(/^0+/, '');
    return trimmed.length > 0 ? trimmed : '0';
}

function money(text) {
    const match = /^([0-9]+)(?:\.([0-9]+))?([kKmM])?$/.exec(text.replace(/[ ,$_]/g, ''));
    if (!match) {
        return null;
    }
    const places = match[3] === undefined ? 0 : match[3].toLowerCase() === 'k' ? 3 : 6;
    const fraction = match[2] ?? '';
    if (places === 0 && /[1-9]/.test(fraction)) {
        return null;
    }
    if (places > 0 && fraction.length > places) {
        return null;
    }
    const value = stripZeros(match[1] + (places > 0 ? fraction.padEnd(places, '0') : ''));
    return value.length <= 11 ? value : null;
}

function count(text) {
    return /^[0-9]{1,2}$/.test(text) ? stripZeros(text) : null;
}

function decimal(text) {
    const match = /^([0-9]{1,2})(?:\.([0-9]{1,2}))?$/.exec(text);
    if (!match) {
        return null;
    }
    const whole = stripZeros(match[1]);
    const fraction = (match[2] ?? '').replace(/0+$/, '');
    return fraction.length > 0 ? `${whole}.${fraction}` : whole;
}

function integer(text, field) {
    const cleaned = text.replace(/[ ,_]/g, '');
    if (!/^[0-9]{1,8}$/.test(cleaned)) {
        return null;
    }
    const value = stripZeros(cleaned);
    const number = Number(value);
    return number >= field.min && number <= field.max ? value : null;
}

function choice(text, field) {
    const lower = lowerAscii(text);
    return field.options.find(option => lowerAscii(option) === lower) ?? null;
}

function state(text) {
    return /^[A-Za-z]{2}$/.test(text) ? text.toUpperCase() : null;
}

function zip(text) {
    const match = /^([0-9]{5})(?:-?[0-9]{4})?$/.exec(text);
    return match ? match[1] : null;
}

export function normalizeField(key, input) {
    const field = fieldOf(key);
    if (!field) {
        return { ok: false, error: 'Unknown field.' };
    }
    const text = cleanText(input, field.kind === 'text' ? field.max : 64);
    if (text.length === 0) {
        return { ok: true, value: '' };
    }
    let value;
    switch (field.kind) {
        case 'money':
            value = money(text);
            break;
        case 'count':
            value = count(text);
            break;
        case 'decimal':
            value = decimal(text);
            break;
        case 'int':
            value = integer(text, field);
            break;
        case 'choice':
            value = choice(text, field);
            break;
        case 'state':
            value = state(text);
            break;
        case 'zip':
            value = zip(text);
            break;
        default:
            value = text;
    }
    return value === null ? { ok: false, error: ERRORS[field.kind] ?? 'Check this value.' } : { ok: true, value };
}

function grouped(digits) {
    return digits.replace(/\B(?=([0-9]{3})+(?![0-9]))/g, ',');
}

export function displayValue(key, value) {
    if (typeof value !== 'string' || value.length === 0) {
        return '';
    }
    switch (fieldOf(key)?.kind) {
        case 'money':
            return `$${grouped(value)}`;
        case 'int':
            return key === 'year' ? value : grouped(value);
        default:
            return value;
    }
}

export function summaryOf(fields) {
    const parts = [];
    if (fields.price) {
        parts.push(displayValue('price', fields.price));
    }
    if (fields.beds) {
        parts.push(`${fields.beds} bd`);
    }
    if (fields.baths) {
        parts.push(`${fields.baths} ba`);
    }
    if (fields.sqft) {
        parts.push(`${displayValue('sqft', fields.sqft)} sq ft`);
    }
    if (fields.status) {
        parts.push(fields.status);
    }
    return parts.join(' · ');
}

export function placeOf(fields) {
    const city = [fields.city, fields.state].filter(Boolean).join(', ');
    return [city, fields.zip].filter(Boolean).join(' ');
}

export function streetOf(fields) {
    if (!fields.street) {
        return '';
    }
    return fields.unit ? `${fields.street}, unit ${fields.unit.replace(/^(?:unit|apt|apartment|suite|ste|#)\s*/i, '')}` : fields.street;
}

export function changedFields(current, inputs) {
    const changes = [];
    const errors = [];
    for (const field of FIELDS) {
        if (!Object.hasOwn(inputs, field.key)) {
            continue;
        }
        const result = normalizeField(field.key, inputs[field.key]);
        if (!result.ok) {
            errors.push({ key: field.key, error: result.error });
        } else if (result.value !== (current[field.key] ?? '')) {
            changes.push({ key: field.key, value: result.value });
        }
    }
    if (changes.some(change => ADDRESS_FIELDS.includes(change.key))) {
        for (const key of ADDRESS_FIELDS) {
            if (!changes.some(change => change.key === key) && Object.hasOwn(inputs, key)) {
                const result = normalizeField(key, inputs[key]);
                if (result.ok) {
                    changes.push({ key, value: result.value });
                }
            }
        }
        changes.sort((a, b) => fieldOrder(a.key) - fieldOrder(b.key));
    }
    return { changes, errors };
}
