import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
import test from "node:test";

const require = createRequire(new URL("../src/frontend/package.json", import.meta.url));
const { JSDOM } = require("jsdom");
const html = readFileSync(
  new URL("../src/backend/Kotlet.Api/Mcp/DataUiApp.html", import.meta.url),
  "utf8",
);

const cases = [
  ["Shopping list", [{ ingredientName: "Milk", measurementUnit: "ml", quantity: 500, totalPrice: 4.2, isPurchased: false, category: "Dairy" }]],
  ["Pantry", [{ ingredientName: "Rice", measurementUnit: "g", quantity: 800, expirationDate: "2027-01-01", storageLocation: "Cabinet" }]],
  ["Prepared meals", [{ name: "Curry", servings: 2, caloriesPerServing: 350, price: 12.5, isArchived: false, addons: [], preparationInstructions: "Heat." }]],
  ["Meal plan", [{ date: "2026-07-23", meals: { dinner: [{ displayName: "Curry", type: "prepared-meal", servings: 2, guests: 0 }] } }]],
  ["Ingredient matches", [{ inputName: "Tomto", matchedName: "Tomato", matchedLanguage: "en", measurementUnit: "g", exactMatch: false, similarity: 0.83 }]],
  ["Duplicate check", { exists: true, matches: [{ recipeId: "00000000-0000-0000-0000-000000000001", title: "Soup", sourceUrl: "https://example.com", matchType: "exactTitle" }] }],
  ["Meal saved", { status: "Success", item: { id: "00000000-0000-0000-0000-000000000002", slot: "breakfast", type: "ingredient", ingredientId: "00000000-0000-0000-0000-000000000003", displayName: "Cottage cheese with cherry tomatoes", note: "A quick breakfast", sortOrder: 0, participants: [{ userId: "00000000-0000-0000-0000-000000000004", displayName: "Asik", isCurrentUser: false, portionPercent: 100 }], guests: 0, servings: 1, servingsOverridden: false } }],
];

test("shared MCP app renders every custom data shape", () => {
  for (const [expectedTitle, structuredContent] of cases) {
    const dom = new JSDOM(html, {
      runScripts: "dangerously",
      url: "https://widget.test/",
      beforeParse(window) {
        window.matchMedia = () => ({ matches: false });
      },
    });
    dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
      data: {
        jsonrpc: "2.0",
        method: "ui/notifications/tool-result",
        params: { structuredContent },
      },
    }));
    assert.equal(dom.window.document.getElementById("title").textContent, expectedTitle);
    assert.ok(dom.window.document.getElementById("content").textContent.trim());
    dom.window.close();
  }
});

test("meal operation result prioritizes useful content and hides identifiers", () => {
  const structuredContent = cases.at(-1)[1];
  const dom = new JSDOM(html, {
    runScripts: "dangerously",
    url: "https://widget.test/",
    beforeParse(window) {
      window.matchMedia = () => ({ matches: false });
    },
  });
  dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
    data: {
      jsonrpc: "2.0",
      method: "ui/notifications/tool-result",
      params: { structuredContent },
    },
  }));

  assert.equal(dom.window.document.querySelector(".meal-title").textContent, "Cottage cheese with cherry tomatoes");
  assert.equal(dom.window.document.querySelector(".operation-hero h2").textContent, "Added to your meal plan");
  assert.equal(dom.window.document.querySelector(".person-main strong").textContent, "Asik");
  assert.equal(dom.window.document.querySelector(".portion strong").textContent, "100%");
  assert.equal(dom.window.document.querySelector("details.technical").open, false);
  dom.window.close();
});

test("resource links render as compact link cards instead of field lists", () => {
  const dom = new JSDOM(html, {
    runScripts: "dangerously",
    url: "https://widget.test/",
    beforeParse(window) {
      window.matchMedia = () => ({ matches: false });
    },
  });
  dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
    data: {
      jsonrpc: "2.0",
      method: "ui/notifications/tool-result",
      params: {
        content: [{
          type: "resource_link",
          uri: "kotlet://meal-plans/days/2026-08-02",
          name: "Meal plan for 2026-08-02",
          title: "Meal plan for 2026-08-02",
          description: "Planned slots: breakfast, dinner, supper.",
          mimeType: "application/json",
        }],
      },
    },
  }));

  const document = dom.window.document;
  assert.equal(document.getElementById("title").textContent, "Tool result");
  assert.equal(document.getElementById("summary").textContent, "1 item");
  assert.equal(document.querySelector(".link-title").textContent, "Meal plan for 2026-08-02");
  // The name repeats the title here, so it is dropped rather than shown twice.
  assert.equal(document.querySelector(".link-name"), null);
  assert.equal(document.querySelector("a.link-uri").getAttribute("href"), "kotlet://meal-plans/days/2026-08-02");
  assert.equal(document.querySelector(".link-body").textContent, "Planned slots: breakfast, dinner, supper.");
  assert.deepEqual(
    [...document.querySelectorAll(".link-types .tag")].map((tag) => tag.textContent),
    ["Resource link", "application/json"],
  );
  assert.equal(document.querySelector("dl"), null);
  dom.window.close();
});

test("a resource link keeps a name that differs from its title", () => {
  const dom = new JSDOM(html, {
    runScripts: "dangerously",
    url: "https://widget.test/",
    beforeParse(window) {
      window.matchMedia = () => ({ matches: false });
    },
  });
  dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
    data: {
      jsonrpc: "2.0",
      method: "ui/notifications/tool-result",
      params: {
        content: [{ type: "resource_link", uri: "kotlet://recipes/1", name: "recipe-1", title: "Fish and chips" }],
      },
    },
  }));

  assert.equal(dom.window.document.querySelector(".link-title").textContent, "Fish and chips");
  assert.equal(dom.window.document.querySelector(".link-name").textContent, "recipe-1");
  assert.equal(dom.window.document.querySelector(".link-body"), null);
  dom.window.close();
});

test("a collection wrapper becomes sections instead of one nested field list", () => {
  const dom = new JSDOM(html, {
    runScripts: "dangerously",
    url: "https://widget.test/",
    beforeParse(window) {
      window.matchMedia = () => ({ matches: false });
    },
  });
  dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
    data: {
      jsonrpc: "2.0",
      method: "ui/notifications/tool-result",
      params: {
        structuredContent: {
          totalCount: 2,
          recipes: [
            {
              id: "6e6d892a-6195-4ba1-8aa3-bfacfa60f78b",
              title: "Ryba z frytkami",
              servings: 1,
              ingredients: [],
              resourceUri: "kotlet://recipes/6e6d892a-6195-4ba1-8aa3-bfacfa60f78b",
            },
            {
              id: "e4871bd9-4163-482d-8ad2-c8920a6c9101",
              title: "Makaron orientalny z krewetkami",
              servings: 1,
              ingredients: [{ id: "6685919b-b9f9-453f-b35d-038c01bc175c", name: "Pasta", quantity: 100, unit: "g" }],
              resourceUri: "kotlet://recipes/e4871bd9-4163-482d-8ad2-c8920a6c9101",
            },
          ],
        },
      },
    },
  }));

  const document = dom.window.document;
  assert.equal(document.querySelector(".stat span").textContent, "Total Count");
  assert.equal(document.querySelector(".section h2").textContent, "Recipes · 2 items");
  // Nested ingredients force cards; the card heading is the title and identifiers move out of the way.
  assert.deepEqual(
    [...document.querySelectorAll(".card h2")].map((heading) => heading.textContent),
    ["Ryba z frytkami", "Makaron orientalny z krewetkami"],
  );
  assert.equal(document.querySelectorAll(".card dt")[0].textContent, "Servings");
  assert.ok(![...document.querySelectorAll(".card > dl dt")].some((term) => term.textContent === "Id"));
  assert.equal(document.querySelector("details.technical dd").textContent, "6e6d892a-6195-4ba1-8aa3-bfacfa60f78b");
  // The ingredient list spans the full row and drops the nested identifier.
  assert.equal(document.querySelector(".nested-item dt").textContent, "Name");
  assert.ok(document.querySelector("dd.stacked .nested-list"));
  dom.window.close();
});

test("shared MCP app renders complete UI data from result metadata", () => {
  const dom = new JSDOM(html, {
    runScripts: "dangerously",
    url: "https://widget.test/",
    beforeParse(window) {
      window.matchMedia = () => ({ matches: false });
    },
  });
  dom.window.dispatchEvent(new dom.window.MessageEvent("message", {
    data: {
      jsonrpc: "2.0",
      method: "ui/notifications/tool-result",
      params: {
        structuredContent: { matches: [{ inputName: "Tomto", matchedName: "Tomato" }] },
        _meta: {
          "kotlet/uiData": [{
            inputName: "Tomto",
            matchedName: "Tomato",
            matchedLanguage: "en",
            measurementUnit: "g",
            exactMatch: false,
            similarity: 0.83,
          }],
        },
      },
    },
  }));

  assert.equal(dom.window.document.getElementById("title").textContent, "Ingredient matches");
  assert.match(dom.window.document.getElementById("content").textContent, /83%/);
  dom.window.close();
});
