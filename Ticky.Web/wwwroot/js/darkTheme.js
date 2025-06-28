let currentItem = localStorage.getItem('Ticky_Theme')
let targetTheme = ''

if (currentItem !== null) {
    targetTheme = currentItem
} else {
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
        targetTheme = 'dark'
    }
}

document.querySelector('html').dataset.theme = targetTheme

function switchTheme() {
    document.querySelector('html').dataset.theme = document.querySelector('html').dataset.theme === 'dark' ? '' : 'dark';

    localStorage.setItem('Ticky_Theme', document.querySelector('html').dataset.theme);
}