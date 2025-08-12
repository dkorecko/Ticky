/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./**/*.{razor,cshtml,html}",
    // Explicitly exclude email templates
    "!./wwwroot/emails/**/*"
  ]
}