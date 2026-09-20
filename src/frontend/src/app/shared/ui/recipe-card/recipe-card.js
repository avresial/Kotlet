(() => {
  const elementName = 'kotlet-recipe-card';
  const placeholder = '<div class="recipe-placeholder" aria-hidden="true"><svg viewBox="0 0 24 24" width="36" height="36" stroke="currentColor" stroke-width="1.6" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M6 10v7a3 3 0 0 0 3 3h6a3 3 0 0 0 3-3v-7"/><path d="M4 10h16"/><path d="M10 6h4"/><path d="M12 3v3"/><path d="M8 10V8a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg></div>';
  const style = `
    kotlet-recipe-card { display: block; min-width: 0; }
    kotlet-recipe-card .recipe-card {
      position: relative; display: flex; flex-direction: column; overflow: hidden;
      border: 1px solid var(--app-border); border-radius: 1rem; background: var(--app-surface);
      box-shadow: 0 .7rem 2rem rgb(70 52 31 / 5%);
    }
    kotlet-recipe-card .recipe-media-container {
      position: relative; width: 100%; height: 8.5rem; background: var(--app-inset);
      overflow: hidden; display: flex; align-items: center; justify-content: center;
    }
    kotlet-recipe-card .recipe-image { display: block; width: 100%; height: 100%; object-fit: cover; }
    kotlet-recipe-card .recipe-placeholder {
      display: flex; align-items: center; justify-content: center; width: 100%; height: 100%;
      background: var(--app-inset); color: var(--app-text-muted); opacity: .8;
    }
    kotlet-recipe-card .recipe-placeholder svg { width: 2.75rem; height: 2.75rem; }
    kotlet-recipe-card .card-body { flex: 1; padding: 14px 16px; }
    kotlet-recipe-card .card-body h2 { margin: 0 0 6px; color: var(--app-text); font: 500 1.15rem Georgia, serif; word-break: break-word; }
    kotlet-recipe-card .card-body .meta { margin: 0 0 6px; color: var(--app-text-muted); font-size: .85rem; line-height: 1.3; }
    kotlet-recipe-card .card-summary {
      display: -webkit-box; margin: 4px 0 0; overflow: hidden; color: var(--app-text-soft);
      font-size: .85rem; line-height: 1.4; -webkit-box-orient: vertical; -webkit-line-clamp: 2;
    }
    kotlet-recipe-card .card-actions { padding: 0 16px 14px; }
    kotlet-recipe-card .button {
      display: inline-flex; align-items: center; justify-content: center; width: 100%; min-height: 2.5rem;
      box-sizing: border-box; padding: 8px 16px; border: 1px solid transparent; border-radius: 6px;
      color: #fff; background: #963827; font: inherit; font-size: .9rem; font-weight: 500; cursor: pointer;
    }
    kotlet-recipe-card .button:hover { background: #842f20; }
    kotlet-recipe-card .button:disabled { opacity: .5; cursor: not-allowed; }
    kotlet-recipe-card .ai-badge {
      display: inline-block; padding: .1rem .45rem; border-radius: 999px; vertical-align: middle;
      color: var(--app-ai); background: var(--app-ai-bg); font-size: .65rem; font-weight: 700; letter-spacing: .05em;
    }
    @media (max-width: 520px) {
      kotlet-recipe-card .recipe-media-container { height: 7.5rem; }
      kotlet-recipe-card .card-body { padding: 12px 14px; }
      kotlet-recipe-card .card-body h2 { font-size: 1.05rem; }
      kotlet-recipe-card .card-actions { padding: 0 14px 12px; }
    }
  `;

  const escapeHtml = value => String(value ?? '')
    .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;').replaceAll("'", '&#39;');

  const humanize = value => String(value ?? '')
    .replaceAll('-', ' ').replace(/^./, character => character.toUpperCase());

  const summary = value => String(value ?? '').replace(/[*_`#]/g, '').split(/\r?\n/)[0].trim();

  const plural = (count, singular, pluralForm = `${singular}s`) =>
    `${count} ${count === 1 ? singular : pluralForm}`;

  const ensureStyles = () => {
    if (document.getElementById('kotlet-shared-recipe-card-style')) return;
    const element = document.createElement('style');
    element.id = 'kotlet-shared-recipe-card-style';
    element.textContent = style;
    document.head.appendChild(element);
  };

  class RecipeCardElement extends HTMLElement {
    static get observedAttributes() { return ['data-recipe']; }

    connectedCallback() {
      ensureStyles();
      this.render();
    }

    attributeChangedCallback() {
      if (this.isConnected) this.render();
    }

    render() {
      let recipe;
      try {
        recipe = JSON.parse(this.getAttribute('data-recipe') || '{}');
      } catch {
        recipe = {};
      }

      const ingredientCount = Number(recipe.ingredientCount) || 0;
      const servings = Number(recipe.servings) || 0;
      const mealType = recipe.mealTypeLabel || humanize(recipe.mealType);
      const summaryText = summary(recipe.description);
      const image = recipe.imageUrl
        ? `<div class="recipe-media-container"><img class="recipe-image" src="${escapeHtml(recipe.imageUrl)}" alt="${escapeHtml(recipe.title)}" /></div>`
        : `<div class="recipe-media-container">${placeholder}</div>`;
      const stats = [
        recipe.servingsLabel || (servings > 0 ? plural(servings, 'serving') : ''),
        recipe.ingredientCountLabel || (ingredientCount > 0 ? plural(ingredientCount, 'ingredient') : ''),
      ].filter(Boolean).join(' · ');

      this.innerHTML =
        '<article class="recipe-card" role="listitem">' +
        image +
        '<div class="card-body">' +
        `<h2>${escapeHtml(recipe.title)}${recipe.isAiAssisted ? ' <span class="ai-badge" title="AI">AI</span>' : ''}</h2>` +
        (mealType ? `<p class="meta">${escapeHtml(mealType)}</p>` : '') +
        (stats ? `<p class="meta">${escapeHtml(stats)}</p>` : '') +
        (summaryText ? `<p class="card-summary">${escapeHtml(summaryText)}</p>` : '') +
        '</div>' +
        `<div class="card-actions"><button type="button" class="button">${escapeHtml(recipe.viewLabel || 'View recipe')}</button></div>` +
        '</article>';

      const imageElement = this.querySelector('.recipe-image');
      imageElement?.addEventListener('error', () => {
        const container = imageElement.parentElement;
        if (container) container.innerHTML = placeholder;
      }, { once: true });

      this.querySelector('button')?.addEventListener('click', event => {
        this.dispatchEvent(new CustomEvent('recipe-view', {
          bubbles: true,
          detail: { id: recipe.id, recipe, button: event.currentTarget },
        }));
      });
    }
  }

  if (!customElements.get(elementName)) customElements.define(elementName, RecipeCardElement);
})();
