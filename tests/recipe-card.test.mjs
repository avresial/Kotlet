import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
import test from "node:test";

const require = createRequire(new URL("../src/frontend/package.json", import.meta.url));
const { JSDOM } = require("jsdom");
const source = readFileSync(
  new URL("../src/frontend/src/app/shared/ui/recipe-card/recipe-card.js", import.meta.url),
  "utf8",
);

function open(recipe) {
  const dom = new JSDOM(
    `<kotlet-recipe-card data-recipe='${JSON.stringify(recipe)}'></kotlet-recipe-card>`,
    {
      runScripts: "dangerously",
      url: "https://widget.test/",
      beforeParse(window) { window.eval(source); },
    },
  );
  dom.window.customElements.upgrade(dom.window.document.querySelector("kotlet-recipe-card"));
  return dom;
}

test("shared recipe card renders normalized data and emits its id", () => {
  const dom = open({
    id: "recipe-1",
    title: "Tomato soup",
    mealTypeLabel: "Dinner",
    servings: 4,
    ingredientCount: 5,
    description: "A warm **soup**.",
    viewLabel: "View recipe",
  });
  const card = dom.window.document.querySelector("kotlet-recipe-card");
  const button = card.querySelector("button");
  let viewed;
  card.addEventListener("recipe-view", event => { viewed = event.detail; });

  assert.equal(card.querySelector("h2").textContent, "Tomato soup");
  assert.equal(card.querySelectorAll(".meta")[0].textContent, "Dinner");
  assert.match(card.querySelector(".card-summary").textContent, /A warm soup/);
  button.click();
  assert.equal(viewed.id, "recipe-1");
  assert.equal(viewed.button, button);
  dom.window.close();
});

test("shared recipe card escapes untrusted text", () => {
  const dom = open({ id: "recipe-2", title: "<script>alert(1)</script>" });
  const card = dom.window.document.querySelector("kotlet-recipe-card");

  assert.equal(card.querySelector("script"), null);
  assert.equal(card.querySelector("h2").textContent, "<script>alert(1)</script>");
  dom.window.close();
});
