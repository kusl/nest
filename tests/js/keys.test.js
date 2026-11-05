import { describe, expect, it } from 'vitest';
import {
    addressKey,
    compareKeys,
    extractUrl,
    fnv1a64,
    guessAddress,
    isKey,
    keyLabel,
    keyPath,
    keyUrl,
    normalizeKeys,
    parseLink,
    words,
} from '../../src/Nest.Web/wwwroot/js/keys.js';
import { fixture } from './fixtures.js';

describe('parseLink', () => {
    for (const vector of fixture('links')) {
        it(`reads ${JSON.stringify(vector.input).slice(0, 80)}`, () => {
            const parsed = parseLink(vector.input);
            expect(parsed?.key ?? null).toBe(vector.key);
            expect(parsed?.url ?? null).toBe(vector.url);
        });
    }

    it('returns valid keys only', () => {
        for (const vector of fixture('links').filter(item => item.key)) {
            expect(isKey(parseLink(vector.input).key)).toBe(true);
        }
    });
});

describe('addressKey', () => {
    for (const vector of fixture('addresses')) {
        it(`keys ${vector.street} ${vector.unit} ${vector.zip}`, () => {
            expect(addressKey(vector.street, vector.unit, vector.zip)).toBe(vector.key);
        });
    }
});

describe('keys', () => {
    it('validates each provider', () => {
        expect(isKey('zillow:29747113')).toBe(true);
        expect(isKey('zillow:029747113')).toBe(false);
        expect(isKey('redfin:1')).toBe(true);
        expect(isKey('realtor:m1-2')).toBe(true);
        expect(isKey('realtor:M1-2')).toBe(false);
        expect(isKey('address:13777-main')).toBe(true);
        expect(isKey('address:1377-main')).toBe(false);
        expect(isKey('url:0123456789abcdef')).toBe(true);
        expect(isKey('url:0123')).toBe(false);
        expect(isKey('other:1')).toBe(false);
        expect(isKey('__proto__:1')).toBe(false);
        expect(isKey(null)).toBe(false);
    });

    it('ranks providers before addresses and links', () => {
        const keys = ['url:0123456789abcdef', 'address:13777-main', 'redfin:2', 'zillow:9', 'realtor:m1-2'];
        expect(keys.sort(compareKeys)).toEqual(['zillow:9', 'redfin:2', 'realtor:m1-2', 'address:13777-main', 'url:0123456789abcdef']);
    });

    it('normalizes key lists', () => {
        expect(normalizeKeys(['zillow:1'], ['zillow:1', 'bad', 'redfin:2'])).toEqual(['redfin:2', 'zillow:1']);
        const many = Array.from({ length: 12 }, (_, index) => `redfin:${index + 1}`);
        const keys = normalizeKeys(['zillow:1'], many);
        expect(keys).toHaveLength(8);
        expect(keys).toContain('zillow:1');
    });

    it('labels, paths and urls', () => {
        expect(keyLabel('zillow:29747113')).toBe('Zillow 29747113');
        expect(keyLabel('realtor:m1-2')).toBe('Realtor.com M1-2');
        expect(keyLabel('address:13777-2893-state-rte-26')).toBe('2893 state rte 26, 13777');
        expect(keyLabel('url:0123456789abcdef')).toBe('Link 012345');
        expect(keyPath('zillow:1')).toBe('homes/zillow/1');
        expect(keyPath('bad')).toBeNull();
        expect(keyUrl('zillow:1')).toBe('https://www.zillow.com/homedetails/1_zpid/');
        expect(keyUrl('redfin:1')).toBe('https://www.redfin.com/home/1');
        expect(keyUrl('address:13777-main')).toBeNull();
        expect(parseLink(keyUrl('zillow:42')).key).toBe('zillow:42');
        expect(parseLink(keyUrl('redfin:42')).key).toBe('redfin:42');
    });

    it('hashes with FNV-1a', () => {
        expect(fnv1a64('')).toBe('cbf29ce484222325');
        expect(fnv1a64('a')).toBe('af63dc4c8601ec8c');
    });

    it('finds the url inside shared text', () => {
        expect(extractUrl('look: https://a.example/x?y=1, nice')).toBe('https://a.example/x?y=1');
        expect(extractUrl('none')).toBeNull();
        expect(extractUrl(42)).toBeNull();
        expect(extractUrl(`https://a.example/${'x'.repeat(3000)}`)).toBeNull();
    });

    it('splits words on anything but ASCII letters and digits', () => {
        expect(words('100 W. 57th-St')).toEqual(['100', 'w', '57th', 'st']);
        expect(words(null)).toEqual([]);
    });
});

describe('guessAddress', () => {
    it('reads a Zillow slug without separating street and city', () => {
        expect(guessAddress('https://www.zillow.com/homedetails/2893-State-Route-26-Gln-Glen-Aubrey-NY-13777/29747113_zpid/')).toEqual({
            street: '2893 State Route 26 Gln Glen Aubrey',
            unit: '',
            city: '',
            state: 'NY',
            zip: '13777',
            mixed: true,
        });
        expect(guessAddress('https://www.zillow.com/homedetails/29747113_zpid/')).toBeNull();
    });

    it('reads Redfin paths', () => {
        expect(guessAddress('https://www.redfin.com/NY/New-York/100-W-57th-St-10019/unit-12B/home/45021234')).toEqual({
            street: '100 W 57th St',
            unit: '12B',
            city: 'New York',
            state: 'NY',
            zip: '10019',
            mixed: false,
        });
        expect(guessAddress('https://www.redfin.com/home/1')).toBeNull();
    });

    it('reads Realtor.com paths', () => {
        expect(guessAddress('https://www.realtor.com/realestateandhomes-detail/2893-State-Route-26_Glen-Aubrey_NY_13777_M41234-56789')).toEqual({
            street: '2893 State Route 26',
            unit: '',
            city: 'Glen Aubrey',
            state: 'NY',
            zip: '13777',
            mixed: false,
        });
    });

    it('ignores other sites', () => {
        expect(guessAddress('https://example.com/a')).toBeNull();
        expect(guessAddress(null)).toBeNull();
    });
});
