import { describe, expect, it } from 'vitest';
import { buildListings, findListing, matches, newestFirst, oldestFirst, timestampOf } from '../../src/Nest.Web/wwwroot/js/merge.js';
import { fixture } from './fixtures.js';

const scenario = fixture('listings');

function shape(listing) {
    return {
        key: listing.key,
        keys: listing.keys,
        links: listing.links.map(link => link.url),
        fields: listing.fields,
        comments: listing.comments.map(comment => comment.id),
        updated: listing.updated,
        title: listing.title,
    };
}

describe('buildListings', () => {
    it('merges entries that share any key', () => {
        expect(buildListings(scenario.entries).map(shape)).toEqual(scenario.listings);
    });

    it('does not depend on entry order', () => {
        const reversed = [...scenario.entries].reverse();
        expect(buildListings(reversed).map(shape)).toEqual(scenario.listings);
        const shuffled = [...scenario.entries].sort((a, b) => (a.text ?? a.kind).localeCompare(b.text ?? b.kind));
        expect(buildListings(shuffled).map(shape)).toEqual(scenario.listings);
    });

    it('keeps field history and ownership', () => {
        const entries = scenario.entries.map(entry => (entry.kind === 'comment' ? { ...entry, mine: true } : entry));
        const [home] = buildListings(entries);
        expect(home.history.beds.map(item => item.value)).toEqual(['3', '4']);
        expect(home.mine).toBe(true);
        expect(home.ids).toHaveLength(8);
    });

    it('skips entries without keys', () => {
        expect(buildListings([null, { id: 'x', keys: [] }])).toEqual([]);
    });

    it('finds and filters listings', () => {
        const listings = buildListings(scenario.entries);
        expect(findListing(listings, 'address:13777-2893-state-rte-26').key).toBe('zillow:29747113');
        expect(findListing(listings, 'zillow:1')).toBeNull();
        expect(matches(listings[0], '')).toBe(true);
        expect(matches(listings[0], '2893 route')).toBe(true);
        expect(matches(listings[0], '13777')).toBe(true);
        expect(matches(listings[0], 'redfin.com')).toBe(true);
        expect(matches(listings[0], 'miami')).toBe(false);
    });
});

describe('ids', () => {
    it('sorts and reads timestamps', () => {
        const ids = [{ id: '0195aaaa-0000-7000-8000-000000000000' }, { id: '0195bbbb-0000-7000-8000-000000000000' }];
        expect([...ids].sort(newestFirst)[0].id).toBe('0195bbbb-0000-7000-8000-000000000000');
        expect([...ids].sort(oldestFirst)[0].id).toBe('0195aaaa-0000-7000-8000-000000000000');
        expect(timestampOf('016f5e66-e800-7000-8000-000000000001')).toBe(Date.UTC(2020, 0, 1));
    });
});
