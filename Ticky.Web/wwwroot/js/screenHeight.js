function setViewportHeight() {
    document.documentElement.style.setProperty('--vvh', `${window.visualViewport.height * 0.01}px`);
}

// Set the height on initial load and when the keyboard appears/disappears
window.visualViewport.addEventListener('resize', setViewportHeight);

// Call it once on page load to set the initial value
setViewportHeight();