import { describe, expect, it } from 'vitest';
import { MAX_RETRIES, THEME_KEY, ViewHost, reconnectDelay, storedTheme, toggledTheme, viewId } from '../../src/Nest.Web/Components/App.razor.js';

describe('reconnectDelay', () => {
    it('retries at once, then backs off with jitter up to thirty seconds', () => {
        expect(reconnectDelay(0)).toBe(0);
        expect(reconnectDelay(1, () => 0)).toBe(500);
        expect(reconnectDelay(1, () => 1)).toBe(1000);
        expect(reconnectDelay(20, () => 1)).toBe(30000);
        expect(reconnectDelay(20, () => 0)).toBe(15000);
    });

    it('keeps trying for more than five minutes', () => {
        let total = 0;
        for (let attempt = 0; attempt < MAX_RETRIES; attempt += 1) {
            total += reconnectDelay(attempt, () => 0);
        }
        expect(total).toBeGreaterThan(5 * 60 * 1000);
    });
});

describe('theme', () => {
    it('reads only known values', () => {
        expect(storedTheme({ getItem: key => (key === THEME_KEY ? 'dark' : null) })).toBe('dark');
        expect(storedTheme({ getItem: () => 'blue' })).toBeNull();
        expect(storedTheme({ getItem: () => { throw new Error('denied'); } })).toBeNull();
        expect(storedTheme(null)).toBeNull();
    });

    it('toggles from the effective theme', () => {
        expect(toggledTheme(null, true)).toBe('light');
        expect(toggledTheme(null, false)).toBe('dark');
        expect(toggledTheme('dark', false)).toBe('light');
    });
});

describe('ViewHost', () => {
    function host(root) {
        const mounted = [];
        const disposed = [];
        const win = { document: { querySelector: () => root.current } };
        const views = new ViewHost(
            win,
            async path => ({ mount: element => {
                mounted.push([path, element.dataset.key]);
                return { dispose: () => disposed.push(element.dataset.key) };
            } }),
            async () => ({ nest: () => ({}) }));
        return { views, mounted, disposed };
    }

    it('mounts the view for the page and replaces it on navigation', async () => {
        const root = { current: { dataset: { view: 'homes' } } };
        const { views, mounted, disposed } = host(root);
        await views.mount();
        await views.mount();
        expect(mounted).toEqual([['./Pages/HomesPage.razor.js', undefined]]);
        root.current = { dataset: { view: 'listing', key: 'zillow:1' } };
        await views.mount();
        expect(mounted.at(-1)).toEqual(['./Pages/ListingPage.razor.js', 'zillow:1']);
        expect(disposed).toEqual([undefined]);
        root.current = null;
        await views.mount();
        expect(disposed).toEqual([undefined, 'zillow:1']);
    });

    it('names views by kind and key', () => {
        expect(viewId(null)).toBeNull();
        expect(viewId({ dataset: { view: 'listing', key: 'zillow:1' } })).toBe('listing|zillow:1');
    });
});
