{
    let theme = null;
    try {
        theme = localStorage.getItem('nest-theme');
    } catch {
    }
    if (theme === 'light' || theme === 'dark') {
        document.documentElement.dataset.theme = theme;
    }
}
