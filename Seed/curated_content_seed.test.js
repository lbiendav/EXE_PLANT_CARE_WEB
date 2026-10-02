"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const { documents } = require("./curated_content_seed");

test("curated catalog contains complete, stable plant and article records", () => {
  const plants = documents.filter(([collection]) => collection === "sample_plants");
  const articles = documents.filter(([collection]) => collection === "articles");
  const documentKeys = documents.map(([collection, id]) => `${collection}/${id}`);

  assert.equal(plants.length, 16);
  assert.equal(articles.length, 14);
  assert.equal(new Set(documentKeys).size, documentKeys.length);

  for (const [, id, plant] of plants) {
    assert.ok(id && plant.name && plant.scientificName && plant.description && plant.imageUrl);
    assert.ok(plant.care?.light && plant.care?.water && plant.care?.soil && plant.care?.fertilizer);
    assert.ok(plant.diseases?.every(disease => disease.issue && disease.cause && disease.treatment));
  }

  for (const [, id, article] of articles) {
    assert.ok(id && article.title && article.content.length >= 10 && article.coverImage);
    assert.ok(article.tags?.length);
    assert.equal(article.views, 0);
  }
});
