const html = document.getElementById('html');
const THEME_KEY = 'Ticky_Theme';
const systemDarkQuery = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

// Returns the theme the user picked, or null when they never picked one.
// The previous theme switcher stored '' for light, so treat that as light too.
function getSavedTheme() {
    const saved = localStorage.getItem(THEME_KEY);

    if (saved === 'dark')
        return 'dark';

    if (saved === 'light' || saved === '')
        return 'light';

    return null;
}

function getSystemTheme() {
    return systemDarkQuery && systemDarkQuery.matches ? 'dark' : 'light';
}

function applyTheme(theme) {
    html.classList.remove('light', 'dark');
    html.classList.add(theme);
}

applyTheme(getSavedTheme() ?? getSystemTheme());

// Without a saved choice, keep following the system setting when it changes.
systemDarkQuery?.addEventListener('change', () => {
    if (getSavedTheme() === null)
        applyTheme(getSystemTheme());
});

function switchTheme() {
    const newTheme = html.classList.contains('dark') ? 'light' : 'dark';

    applyTheme(newTheme);
    localStorage.setItem(THEME_KEY, newTheme);
}
