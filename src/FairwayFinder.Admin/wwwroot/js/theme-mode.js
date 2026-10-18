// Radzen swaps the theme stylesheet without marking the page, so the fairway theme
// keys its light/dark tokens off <html data-mode>. The cookie lets App.razor render
// the right theme server-side on the next load instead of flashing dark first.
window.ffThemeMode = {
    get: () => document.documentElement.dataset.mode || "dark",
    set: (mode) => {
        document.documentElement.dataset.mode = mode;
        document.cookie = `ff-mode=${mode}; path=/; max-age=31536000; samesite=lax`;
    }
};
