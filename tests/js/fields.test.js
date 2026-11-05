import { describe, expect, it } from 'vitest';
import {
    FIELDS,
    changedFields,
    cleanText,
    displayValue,
    fieldOf,
    normalizeField,
    placeOf,
    streetOf,
    summaryOf,
} from '../../src/Nest.Web/wwwroot/js/fields.js';
import { fixture } from './fixtures.js';

describe('normalizeField', () => {
    for (const vector of fixture('fields')) {
        it(`${vector.key} ${JSON.stringify(vector.input)}`, () => {
            const result = normalizeField(vector.key, vector.input);
            expect(result.ok).toBe(vector.ok);
            if (vector.ok) {
                expect(result.value).toBe(vector.value);
                expect(normalizeField(vector.key, result.value).value).toBe(result.value);
            } else {
                expect(result.error).toBeTruthy();
            }
        });
    }
});

describe('fields', () => {
    it('has unique keys', () => {
        expect(new Set(FIELDS.map(field => field.key)).size).toBe(FIELDS.length);
        expect(fieldOf('nope')).toBeNull();
    });

    it('cleans text', () => {
        expect(cleanText(' a \n\n b ', 10)).toBe('a b');
        expect(cleanText('abcdef', 3)).toBe('abc');
        expect(cleanText('ab cd', 3)).toBe('ab');
        expect(cleanText(null, 3)).toBe('');
    });

    it('formats values', () => {
        expect(displayValue('price', '1250000')).toBe('$1,250,000');
        expect(displayValue('sqft', '1800')).toBe('1,800');
        expect(displayValue('year', '1925')).toBe('1925');
        expect(displayValue('beds', '3')).toBe('3');
        expect(displayValue('beds', '')).toBe('');
        expect(summaryOf({ price: '450000', beds: '3', baths: '2.5', sqft: '1800', status: 'Pending' })).toBe('$450,000 · 3 bd · 2.5 ba · 1,800 sq ft · Pending');
        expect(summaryOf({})).toBe('');
        expect(placeOf({ city: 'Glen Aubrey', state: 'NY', zip: '13777' })).toBe('Glen Aubrey, NY 13777');
        expect(placeOf({ zip: '13777' })).toBe('13777');
        expect(streetOf({ street: '1 Main St', unit: 'Apt 4' })).toBe('1 Main St, unit 4');
        expect(streetOf({ unit: '4' })).toBe('');
    });

    it('sends only what changed, with the whole address when any of it changed', () => {
        const current = { beds: '3', street: '1 Main St', zip: '13777' };
        expect(changedFields(current, { beds: '3', baths: '' }).changes).toEqual([]);
        expect(changedFields(current, { beds: '4' }).changes).toEqual([{ key: 'beds', value: '4' }]);
        expect(changedFields(current, { beds: '', street: '1 Main St', zip: '13777', city: 'Glen Aubrey', unit: '', state: '' }).changes).toEqual([
            { key: 'beds', value: '' },
            { key: 'street', value: '1 Main St' },
            { key: 'unit', value: '' },
            { key: 'city', value: 'Glen Aubrey' },
            { key: 'state', value: '' },
            { key: 'zip', value: '13777' },
        ]);
        const result = changedFields(current, { price: 'lots' });
        expect(result.changes).toEqual([]);
        expect(result.errors.map(error => error.key)).toEqual(['price']);
    });
});
