import { describe, expect, it } from 'vitest';
import { RESULT_TEXT, STATUS_TEXT, commentsLabel, isoOf, previewText, readMineOnly, timeLabel, visibleListings, writeMineOnly } from '../../src/Nest.Web/Components/Pages/HomesPage.razor.js';
import { addressSuggestion, anchorsOf, countText, previousValue } from '../../src/Nest.Web/Components/Pages/ListingPage.razor.js';
import { parseLink } from '../../src/Nest.Web/wwwroot/js/keys.js';
import { buildListings } from '../../src/Nest.Web/wwwroot/js/merge.js';
import { fixture } from './fixtures.js';

const listings = buildListings(fixture('listings').entries);
const ID = '016f5e66-e800-7000-8000-000000000001';

describe('homes view helpers', () => {
    it('describes a pasted link', () => {
        expect(previewText(null, null)).toBe('');
        expect(previewText(parseLink('https://www.zillow.com/homedetails/1_zpid/'), null)).toBe('Zillow home 1.');
        expect(previewText(parseLink('https://example.com/a'), listings[1])).toBe(`A link from another site. Already here: ${listings[1].title}.`);
    });

    it('filters by text and ownership', () => {
        expect(visibleListings(listings, '', false)).toHaveLength(2);
        expect(visibleListings(listings, 'miami', false)).toHaveLength(0);
        expect(visibleListings(listings, '2893', false)).toHaveLength(1);
        expect(visibleListings(listings, '', true)).toHaveLength(0);
    });

    it('formats counts and times', () => {
        expect(commentsLabel(1)).toBe('1 comment');
        expect(commentsLabel(0)).toBe('0 comments');
        expect(isoOf(ID)).toBe('2020-01-01T00:00:00.000Z');
        expect(timeLabel(ID, null)).toBe('2020-01-01 00:00');
        expect(timeLabel(ID, { format: () => 'formatted' })).toBe('formatted');
        expect(timeLabel(ID, { format: () => { throw new Error('x'); } })).toBe('2020-01-01 00:00');
        expect(STATUS_TEXT.live).toBe('Live');
        expect(RESULT_TEXT.offline).toMatch(/Not connected/);
    });

    it('remembers the ownership filter', () => {
        const map = new Map();
        const storage = { getItem: key => map.get(key) ?? null, setItem: (key, value) => map.set(key, value), removeItem: key => map.delete(key) };
        writeMineOnly(storage, true);
        expect(readMineOnly(storage)).toBe(true);
        writeMineOnly(storage, false);
        expect(readMineOnly(storage)).toBe(false);
        expect(readMineOnly(null)).toBe(false);
    });
});

describe('listing view helpers', () => {
    it('anchors new entries to the known keys', () => {
        expect(anchorsOf(listings[0], 'zillow:29747113')).toEqual(listings[0].keys);
        expect(anchorsOf(null, 'zillow:5')).toEqual(['zillow:5']);
    });

    it('shows the previous value of a field', () => {
        expect(previousValue(listings[0].history, 'beds', '4')).toBe('3');
        expect(previousValue(listings[0].history, 'street', '2893 State Route 26')).toBeNull();
        expect(previousValue(undefined, 'beds', '4')).toBeNull();
    });

    it('suggests an address from a link only when none is known', () => {
        expect(addressSuggestion(listings[0])).toBeNull();
        const [fresh] = buildListings([{ id: ID, kind: 'link', keys: ['realtor:m1-2'], url: 'https://www.realtor.com/realestateandhomes-detail/1-Main-St_Glen-Aubrey_NY_13777_M1-2' }]);
        expect(addressSuggestion(fresh)).toMatchObject({ street: '1 Main St', city: 'Glen Aubrey', state: 'NY', zip: '13777' });
        expect(addressSuggestion(null)).toBeNull();
    });

    it('counts characters', () => {
        expect(countText(3, 4000)).toBe('3 / 4000');
    });
});
