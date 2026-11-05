export const THEME_KEY = 'nest-theme';
export const MAX_RETRIES = 30;
export const RECONNECTED_EVENT = 'nest:reconnected';

export const VIEWS = Object.freeze({
    homes: './Pages/HomesPage.razor.js',
    listing: './Pages/ListingPage.razor.js',
});

export function storedTheme(storage) {
    try {
        const value = storage?.getItem(THEME_KEY);
        return value === 'light' || value === 'dark' ? value : null;
    } catch {
        return null;
    }
}

export function toggledTheme(current, prefersDark) {
    const effective = current ?? (prefersDark ? 'dark' : 'light');
    return effective === 'dark' ? 'light' : 'dark';
}

export function reconnectDelay(attempt, random = Math.random) {
    if (attempt <= 0) {
        return 0;
    }
    const ceiling = Math.min(30000, 1000 * 2 ** (attempt - 1));
    return Math.round(ceiling / 2 + random() * ceiling / 2);
}

export function viewId(root) {
    return root ? `${root.dataset.view}|${root.dataset.key ?? ''}` : null;
}

export class ViewHost {
    constructor(win, load = path => import(path), shared = () => import('../js/sync.js')) {
        this.win = win;
        this.load = load;
        this.shared = shared;
        this.current = null;
        this.generation = 0;
    }

    async mount() {
        const root = this.win.document.querySelector('[data-view]');
        const id = viewId(root);
        if (this.current && this.current.id === id && this.current.root === root) {
            return;
        }
        this.generation += 1;
        const generation = this.generation;
        this.current?.view.dispose();
        this.current = null;
        const path = root ? VIEWS[root.dataset.view] : undefined;
        if (!path) {
            return;
        }
        try {
            const [{ nest }, module] = await Promise.all([this.shared(), this.load(path)]);
            if (generation !== this.generation) {
                return;
            }
            this.current = { id, root, view: module.mount(root, nest(this.win), this.win) };
        } catch {
        }
    }
}

export function boot(win) {
    const doc = win.document;
    const darkQuery = win.matchMedia('(prefers-color-scheme: dark)');

    const applyTheme = theme => {
        if (theme) {
            doc.documentElement.dataset.theme = theme;
        } else {
            delete doc.documentElement.dataset.theme;
        }
        const dark = theme ? theme === 'dark' : darkQuery.matches;
        for (const button of doc.querySelectorAll('[data-theme-toggle]')) {
            button.setAttribute('aria-pressed', String(dark));
        }
    };

    const applyStoredTheme = () => applyTheme(storedTheme(win.localStorage));

    const toggleTheme = () => {
        const next = toggledTheme(doc.documentElement.dataset.theme ?? null, darkQuery.matches);
        try {
            win.localStorage.setItem(THEME_KEY, next);
        } catch {
        }
        applyTheme(next);
    };

    const retryWhenVisible = () => {
        if (doc.visibilityState === 'visible') {
            retry();
        }
    };

    const retry = async () => {
        doc.removeEventListener('visibilitychange', retryWhenVisible);
        try {
            if (await win.Blazor.reconnect()) {
                return;
            }
            if (await win.Blazor.resumeCircuit()) {
                return;
            }
            win.location.reload();
        } catch {
            doc.addEventListener('visibilitychange', retryWhenVisible);
        }
    };

    const resume = async () => {
        try {
            if (await win.Blazor.resumeCircuit()) {
                return;
            }
        } catch {
        }
        win.location.reload();
    };

    const clearLocal = async button => {
        const output = doc.getElementById(button.dataset.clearLocal);
        button.disabled = true;
        try {
            const [{ DB_NAME, deleteDatabase, draftKeys }, { current }, { MINE_KEY }] = await Promise.all([
                import('../js/store.js'),
                import('../js/sync.js'),
                import('./Pages/HomesPage.razor.js'),
            ]);
            await current()?.clear();
            const outcome = await deleteDatabase(win.indexedDB, DB_NAME);
            for (const key of [...draftKeys(win.localStorage), MINE_KEY]) {
                try {
                    win.localStorage.removeItem(key);
                } catch {
                }
            }
            if (output) {
                output.textContent = outcome === 'blocked'
                    ? 'Close any other Nest tabs to finish clearing.'
                    : 'This browser\'s copy is gone. Other browsers keep theirs.';
            }
        } catch {
            if (output) {
                output.textContent = 'Could not clear this browser\'s copy.';
            }
        } finally {
            button.disabled = false;
        }
    };

    const actions = {
        themeToggle: toggleTheme,
        reconnectRetry: retry,
        reconnectResume: resume,
        reconnectReload: () => win.location.reload(),
        clearLocal,
    };

    doc.addEventListener('click', event => {
        const button = event.target instanceof win.Element ? event.target.closest('button') : null;
        if (!button) {
            return;
        }
        for (const [name, action] of Object.entries(actions)) {
            if (name in button.dataset) {
                action(button);
                return;
            }
        }
    });

    doc.getElementById('components-reconnect-modal')?.addEventListener('components-reconnect-state-changed', event => {
        const state = event.detail?.state;
        if (state === 'rejected') {
            win.location.reload();
        } else if (state === 'hide') {
            win.dispatchEvent(new win.CustomEvent(RECONNECTED_EVENT));
        } else if (state === 'failed') {
            doc.addEventListener('visibilitychange', retryWhenVisible);
        }
    });

    darkQuery.addEventListener('change', applyStoredTheme);
    applyStoredTheme();

    const views = new ViewHost(win);
    views.mount();

    const afterNavigation = () => {
        applyStoredTheme();
        const announcer = doc.getElementById('announcer');
        if (announcer) {
            announcer.textContent = '';
        }
        views.mount();
    };

    return win.Blazor.start({
        circuit: {
            reconnectionOptions: {
                maxRetries: MAX_RETRIES,
                retryIntervalMilliseconds: attempt => reconnectDelay(attempt),
            },
        },
    }).then(() => win.Blazor.addEventListener('enhancedload', afterNavigation));
}

if (typeof window !== 'undefined' && window.Blazor) {
    boot(window);
}
