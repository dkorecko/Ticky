const html = document.getElementById('html');
const THEME_KEY = 'Ticky_Theme';

let savedTheme = localStorage.getItem(THEME_KEY);

if (!savedTheme) {
    savedTheme = 'light';
    localStorage.setItem(THEME_KEY, savedTheme);
}

html.classList.add(savedTheme);

function switchTheme() {
    const currentTheme = html.classList.contains('dark') ? 'dark' : 'light';
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';

    html.classList.remove(currentTheme);
    html.classList.add(newTheme);

    localStorage.setItem(THEME_KEY, newTheme);
}
