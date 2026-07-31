import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
import test from "node:test";

const require = createRequire(new URL("../src/frontend/package.json", import.meta.url));
const { JSDOM } = require("jsdom");

const apps = {
  data: "../src/backend/Kotlet.Api/Mcp/DataUiApp.html",
  recipes: "../src/backend/Kotlet.Api/Recipes/RecipeUiApp.html",
  planDraft: "../src/backend/Kotlet.Api/MealPlanner/MealPlanUiApp.html",
  planDay: "../src/backend/Kotlet.Api/MealPlanner/MealPlannerUiApp.html",
};
const html = Object.fromEntries(Object.entries(apps)
  .map(([name, path]) => [name, readFileSync(new URL(path, import.meta.url), "utf8")]));

/** Loads an app, optionally announcing a host locale, then delivers a tool result. */
function open(app, { serverLocale, hostLocale, structuredContent, meta } = {}) {
  const dom = new JSDOM(html[app], {
    runScripts: "dangerously",
    url: "https://widget.test/",
    beforeParse(window) {
      window.matchMedia = () => ({ matches: false });
    },
  });
  const notify = (method, params) => dom.window.dispatchEvent(
    new dom.window.MessageEvent("message", { data: { jsonrpc: "2.0", method, params } }));
  if (hostLocale) notify("ui/notifications/host-context-changed", { hostContext: { locale: hostLocale } });
  if (structuredContent !== undefined) {
    notify("ui/notifications/tool-result", {
      structuredContent,
      _meta: Object.assign(serverLocale ? { "kotlet/locale": serverLocale } : {}, meta),
    });
  }
  return dom;
}

const text = (dom, selector) => dom.window.document.querySelector(selector).textContent.replace(/\s+/g, " ").trim();
const body = (dom) => dom.window.document.body.textContent.replace(/\s+/g, " ").trim();

const shoppingList = (count) => Array.from({ length: count }, (_, index) => ({
  ingredientName: `Item ${index}`, measurementUnit: "g", quantity: 100,
  totalPrice: 2, isPurchased: false, category: "Dairy",
}));
const pantry = (count) => Array.from({ length: count }, (_, index) => ({
  ingredientName: `Item ${index}`, measurementUnit: "g", quantity: 100,
  expirationDate: null, storageLocation: "Cabinet",
}));
const recipeList = {
  apiOrigin: "https://kotlet.test", totalCount: 5, page: 1, pageSize: 2,
  recipes: [{
    id: "11111111-1111-1111-1111-111111111111", title: "Zupa", mealType: "dinner", servings: 4,
    ingredientCount: 5, imageUrl: null, isAiAssisted: false, updatedAtUtc: "2026-01-01T00:00:00Z",
  }],
};
const planDraft = {
  preview: {
    weekStart: "2026-02-02",
    meals: [{ date: "2026-02-02", slot: "second-breakfast", type: "recipe", title: "Zupa", servings: 2, ingredients: ["Woda"] }],
  },
  saveRequest: { weekStart: "2026-02-02", meals: [{ date: "2026-02-02", slot: "second-breakfast", recipeId: "x" }] },
};
const planDay = {
  date: "2026-02-02", mealCount: 1,
  slots: [{
    slot: "dinner", label: "Lunch",
    items: [{
      id: "22222222-2222-2222-2222-222222222222", type: "recipe", displayName: "Zupa", note: null,
      servings: 2, guests: 1, ingredientQuantity: null, ingredientUnit: null,
      participants: [{ displayName: "Asik", isCurrentUser: true, portionPercent: 100 }],
    }],
  }],
};

test("every app ships the same keys in both languages", () => {
  for (const app of Object.keys(apps)) {
    const dom = open(app);
    const languages = dom.window.eval("Object.keys(STRINGS)");
    assert.deepEqual([...languages].sort(), ["en", "pl"], app);
    // "field." entries name payload properties. English derives those from the humanized property
    // name, so only the languages that need a table carry them — every other key must be in both.
    const keys = (code) => [...dom.window.eval(`Object.keys(STRINGS.${code})`)]
      .filter((key) => !key.startsWith("field.")).sort();
    assert.deepEqual(keys("pl"), keys("en"), `${app} dictionaries drifted apart`);
    dom.window.close();
  }
});

test("apps render English when no locale is announced", () => {
  const shopping = open("data", { structuredContent: shoppingList(1) });
  assert.equal(text(shopping, "#title"), "Shopping list");
  assert.equal(shopping.window.document.documentElement.lang, "en");
  shopping.window.close();

  const recipes = open("recipes", { structuredContent: recipeList });
  assert.equal(text(recipes, "#list-eyebrow"), "Kotlet · Recipes");
  assert.equal(text(recipes, "#list-footer"), "Showing 1 of 5 recipes.");
  recipes.window.close();
});

test("the server locale localizes the shared data app", () => {
  const shopping = open("data", { serverLocale: "pl", structuredContent: shoppingList(2) });
  assert.equal(shopping.window.document.documentElement.lang, "pl");
  assert.equal(text(shopping, "#title"), "Lista zakupów");
  assert.equal(text(shopping, "#summary"), "kupiono 0 z 2");
  // Group headings translate the FoodCategory the API returned.
  assert.equal(text(shopping, ".section h2"), "Nabiał");
  assert.ok(body(shopping).includes("szacowana suma"));
  shopping.window.close();

  const meal = open("data", {
    serverLocale: "pl",
    structuredContent: {
      status: "Success",
      item: {
        id: "33333333-3333-3333-3333-333333333333", slot: "breakfast", type: "ingredient",
        displayName: "Twaróg", participants: [], guests: 0, servings: 1,
      },
    },
  });
  assert.equal(text(meal, "#title"), "Posiłek zapisany");
  assert.equal(text(meal, ".operation-hero h2"), "Dodano do planu posiłków");
  assert.equal(text(meal, "#summary"), "Śniadanie");
  assert.equal(text(meal, "details.technical summary"), "Szczegóły techniczne");
  meal.window.close();
});

test("Polish plural forms follow the one/few/many split", () => {
  for (const [count, expected] of [[1, "1 pozycja"], [3, "3 pozycje"], [5, "5 pozycji"]]) {
    const dom = open("data", { serverLocale: "pl", structuredContent: pantry(count) });
    assert.equal(text(dom, "#summary"), expected);
    dom.window.close();
  }
});

test("the host locale is used when the server negotiated none, and loses to it otherwise", () => {
  const host = open("data", { hostLocale: "pl-PL", structuredContent: shoppingList(1) });
  assert.equal(text(host, "#title"), "Lista zakupów");
  host.window.close();

  const server = open("data", { hostLocale: "pl-PL", serverLocale: "en", structuredContent: shoppingList(1) });
  assert.equal(text(server, "#title"), "Shopping list");
  server.window.close();
});

test("an unsupported locale falls back to English", () => {
  const dom = open("data", { serverLocale: "fr-FR", structuredContent: shoppingList(1) });
  assert.equal(dom.window.document.documentElement.lang, "en");
  assert.equal(text(dom, "#title"), "Shopping list");
  dom.window.close();
});

test("the recipe app localizes cards, counts and meal types", () => {
  const dom = open("recipes", { serverLocale: "pl", structuredContent: recipeList });
  assert.equal(text(dom, "#list-eyebrow"), "Kotlet · Przepisy");
  assert.equal(text(dom, "#back"), "← Przepisy");
  assert.equal(text(dom, ".recipe-card .meta"), "Obiad");
  assert.equal(text(dom, ".card-actions button"), "Zobacz przepis");
  assert.equal(text(dom, "#list-footer"), "Pokazano 1 z 5 przepisów.");
  assert.ok(body(dom).includes("4 porcje · 5 składników"));
  dom.window.close();
});

test("the weekly draft app localizes its chrome and save button", () => {
  const dom = open("planDraft", { serverLocale: "pl", structuredContent: planDraft });
  assert.equal(text(dom, "#eyebrow"), "Tygodniowy plan Kotlet");
  assert.equal(text(dom, "#title"), "Szkic planu posiłków");
  assert.equal(text(dom, "#draft-badge"), "Szkic · niezapisany");
  assert.equal(text(dom, "#save"), "Dodaj do Kotleta");
  assert.equal(text(dom, ".meal .slot"), "II śniadanie");
  assert.equal(text(dom, ".meal .kind"), "Przepis");
  assert.ok(text(dom, "#subtitle").startsWith("1 posiłek · tydzień od "));
  dom.window.close();
});

test("the read-only day app localizes slots, columns and the footer", () => {
  const dom = open("planDay", { serverLocale: "pl", structuredContent: planDay });
  assert.equal(text(dom, "#eyebrow"), "Kotlet · Planer posiłków");
  assert.equal(text(dom, "#readonly-tag"), "Tylko do odczytu");
  // The slot heading is translated by key, not by the English label the server sent.
  assert.equal(text(dom, ".slot-heading h2"), "Obiad");
  assert.equal(text(dom, ".slot-count"), "1 posiłek");
  assert.equal(text(dom, ".portion-table th"), "Osoba");
  assert.equal(text(dom, ".chip.is-guest"), "Gość");
  assert.equal(text(dom, ".meal-summary dt"), "Liczba osób");
  assert.equal(text(dom, "#plan-footer"), "Zaplanowano: 1 posiłek.");
  assert.equal(dom.window.document.getElementById("prev-day").getAttribute("aria-label"), "Poprzedni dzień");
  dom.window.close();
});

test("a slot the app does not know keeps the label the server sent", () => {
  const dom = open("planDay", {
    serverLocale: "pl",
    structuredContent: {
      date: "2026-02-02", mealCount: 0,
      slots: [{ slot: "brunch", label: "Brunch", items: [] }],
    },
  });
  assert.equal(text(dom, ".slot-heading h2"), "Brunch");
  assert.equal(text(dom, ".empty-slot"), "Brak pozycji.");
  dom.window.close();
});
