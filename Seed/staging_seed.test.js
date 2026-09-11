"use strict";
const test = require("node:test");
const assert = require("node:assert/strict");
const { Timestamp } = require("firebase-admin/firestore");
const { normalize, validateProjects, applyPlan, main } = require("./staging_seed");
const time = Timestamp.fromMillis(1700000000000);

test("only approved fields survive; care and timestamp defaults match C#", () => {
    const result = normalize("sample_plants", "p1", {
        name: "Test plant", image: "https://example.com/plant.png", ownerEmail: "private@example.com",
        userId: "private-user", care: { water: "Weekly", privateNote: "secret" },
        diseases: [{ name: "Test issue", symptoms: "Test cause", treatment: "Test treatment", userId: "private-user" }],
    }, time);
    assert.equal(result.imageUrl, "https://example.com/plant.png");
    assert.equal(result.createdAt, time);
    assert.deepEqual(result.care, { light: "", water: "Weekly", soil: "", fertilizer: "" });
    assert.deepEqual(result.diseases, [{ issue: "Test issue", cause: "Test cause", treatment: "Test treatment" }]);
    assert.equal(result.ownerEmail, undefined);
    assert.equal(result.userId, undefined);
});

test("templates prefer careInstructions and omit unsupported fields", () => {
    const result = normalize("plant_templates", "p1", {
        name: "Test", care: { water: "" }, careInstructions: { water: "Weekly" }, diseases: [], createdAt: time,
    }, Timestamp.now());
    assert.equal(result.templateId, "p1");
    assert.equal(result.careInstructions.water, "Weekly");
    assert.equal(result.care, undefined);
    assert.equal(result.diseases, undefined);
    assert.equal(result.createdAt, time);
});

test("articles reset counters and repair epoch timestamps", () => {
    const result = normalize("articles", "a1", {
        title: "Test", views: 999, createdAt: Timestamp.fromMillis(0), authorEmail: "private@example.com",
    }, time);
    assert.equal(result.views, 0);
    assert.equal(result.createdAt, time);
    assert.equal(result.authorEmail, undefined);
    assert.deepEqual(result.tags, []);
});

test("reject unsupported collections and wrong credential projects", () => {
    assert.throws(() => normalize("users", "u1", {}, time), /allowlist/);
    assert.throws(() => validateProjects({ project_id: "homeplant-staging-dav" }, { project_id: "home-plant-app-dav" }), /mismatch/);
    assert.doesNotThrow(() => validateProjects({ project_id: "home-plant-app-dav" }, { project_id: "homeplant-staging-dav" }));
});

test("dry-run never writes; existing docs are not overwritten", async () => {
    let writes = 0;
    const ref = { create: async () => { writes++; } };
    const plan = [{ collection: "articles", exists: false, ref, data: {} }, { collection: "articles", exists: true, ref, data: {} }];
    assert.deepEqual(await applyPlan(plan, false), { articles: { source: 2, created: 0, skipped: 1, pending: 1 } });
    assert.equal(writes, 0);
    assert.deepEqual(await applyPlan(plan, true), { articles: { source: 2, created: 1, skipped: 1, pending: 0 } });
    assert.equal(writes, 1);
});

test("concurrent create is skipped but unexpected write failures propagate", async () => {
    const item = { collection: "articles", exists: false, ref: { create: async () => { throw Object.assign(new Error("exists"), { code: 6 }); } } };
    assert.equal((await applyPlan([item], true)).articles.skipped, 1);
    item.ref.create = async () => { throw new Error("permission denied"); };
    await assert.rejects(applyPlan([item], true), /permission denied/);
});

test("write confirmation and unknown-flag guards run before any credential reads", async () => {
    await assert.rejects(main(["--apply"]), /Writes require/);
    await assert.rejects(main(["--apply", "--dry-run"]), /Choose/);
    await assert.rejects(main(["--target=production"]), /Unknown argument/);
});
