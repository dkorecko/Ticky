/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./**/*.{razor,cshtml,html}",
    "./wwwroot/information.json",
    // Explicitly exclude email templates
    "!./wwwroot/emails/**/*"
  ]
}