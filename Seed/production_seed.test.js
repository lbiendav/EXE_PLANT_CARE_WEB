"use strict";
const test = require("node:test");
const assert = require("node:assert/strict");
const { validateProjects, main } = require("./production_seed");

test("production seed accepts only the legacy source and Production target", () => {
    assert.doesNotThrow(() => validateProjects(
        { project_id: "home-plant-app-dav" },
        { project_id: "homeplant-production" }));
    assert.throws(() => validateProjects(
        { project_id: "homeplant-staging-dav" },
        { project_id: "homeplant-production" }), /mismatch/);
    assert.throws(() => validateProjects(
        { project_id: "home-plant-app-dav" },
        { project_id: "homeplant-staging-dav" }), /mismatch/);
});

test("production writes require an exact project confirmation", async () => {
    await assert.rejects(main(["--apply"]), /Writes require/);
    await assert.rejects(main(["--apply", "--dry-run"]), /Choose/);
    await assert.rejects(main(["--target=production"]), /Unknown argument/);
});
