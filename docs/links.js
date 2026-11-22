(() => {
  const owner = window.location.hostname.match(/^([\w-]+)\.github\.io$/i);
  const path = window.location.pathname.split('/').filter(Boolean);
  const repo = path[0];
  if (!owner || !repo || !/^[\w.-]+$/.test(repo) || repo === 'index.html') return;

  document.querySelectorAll('[data-repo-link]').forEach((link) => {
    link.href = `https://github.com/${owner[1]}/${repo}`;
    link.textContent = 'View on GitHub ↗';
  });
})();
