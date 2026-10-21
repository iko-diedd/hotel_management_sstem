// theme init
(function () {
  var KEY = 'hk.theme', root = document.documentElement, theme = 'dark';
  try { var saved = localStorage.getItem(KEY); if (saved === 'light' || saved === 'dark') theme = saved; } catch (e) { }
  root.dataset.theme = theme;
  window.HKTheme = {
    get: function () { return root.dataset.theme; },
    toggle: function () {
      theme = theme === 'dark' ? 'light' : 'dark'; root.dataset.theme = theme;
      try { localStorage.setItem(KEY, theme); } catch (e) { }
      return theme;
    }
  };
})();
