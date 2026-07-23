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
