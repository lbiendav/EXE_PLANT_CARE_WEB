"use strict";

// Copies only shared reference content. Never imports Auth, users or subcollections.
const fs = require("node:fs");
const path = require("node:path");
const { initializeApp, cert, deleteApp } = require("firebase-admin/app");
const { getFirestore, Timestamp } = require("firebase-admin/firestore");

const SOURCE_PROJECT = "home-plant-app-dav";
const TARGET_PROJECT = "homeplant-staging-dav";
const COLLECTIONS = ["sample_plants", "plant_templates", "articles"];
const MAX_PER_COLLECTION = 100;

function text(value, fallback = "") {
    if (value == null) return fallback;
    if (typeof value !== "string") throw new Error("Expected a string field");
    return value;
}

function requiredText(value) {
    const result = text(value);
    if (!result.trim()) throw new Error("Missing required name/title");
    return result;
}

function care(value = {}) {
    return Object.fromEntries(["light", "water", "soil", "fertilizer"]
        .map(key => [key, text(value?.[key])]));
}

function normalize(collection, id, data, fallbackTime) {
    const createdAt = data.createdAt instanceof Timestamp && data.createdAt.seconds > 0
        ? data.createdAt : fallbackTime;
    if (!(createdAt instanceof Timestamp)) throw new Error("Missing valid timestamp");

    if (collection === "articles") {
        return {
            title: requiredText(data.title),
            content: text(data.content),
            coverImage: text(data.coverImage),
            tags: Array.isArray(data.tags) ? data.tags.map(tag => text(tag)) : [],
            views: 0,
            createdAt,
        };
    }

    if (!["sample_plants", "plant_templates"].includes(collection)) {
        throw new Error("Collection is not in the seed allowlist");
    }
    const common = {
        name: requiredText(data.name),
        scientificName: text(data.scientificName),
        description: text(data.description),
        imageUrl: text(data.imageUrl ?? data.image),
        createdAt,
    };
    if (collection === "sample_plants") {
        return {
            ...common,
            care: care(data.care ?? data.careInstructions),
            diseases: (Array.isArray(data.diseases) ? data.diseases : []).map(d => ({
                issue: text(d.issue ?? d.name),
                cause: text(d.cause ?? d.symptoms),
                treatment: text(d.treatment),
            })),
        };
    }
    return {
        ...common,
        templateId: text(data.templateId, id),
        careInstructions: care(data.careInstructions ?? data.care),
        isFeatured: data.isFeatured === true,
    };
}

function validateProjects(sourceKey, targetKey) {
    if (sourceKey.project_id !== SOURCE_PROJECT || targetKey.project_id !== TARGET_PROJECT) {
        throw new Error("Credential project mismatch: refusing to proceed");
    }
}

async function makePlan(source, target) {
    const plan = [];
    // Validate every source document before performing any writes.
    for (const collection of COLLECTIONS) {
        const snapshot = await source.collection(collection).limit(MAX_PER_COLLECTION + 1).get();
        if (snapshot.size > MAX_PER_COLLECTION) {
            throw new Error(`${collection}: more than ${MAX_PER_COLLECTION} documents; review scope first`);
        }
        for (const doc of snapshot.docs) {
            const data = normalize(collection, doc.id, doc.data(), doc.createTime);
            const ref = target.collection(collection).doc(doc.id);
            const existing = await ref.get();
            plan.push({ collection, id: doc.id, data, ref, exists: existing.exists });
        }
    }
    return plan;
}

async function applyPlan(plan, apply) {
    const summary = {};
    for (const item of plan) {
        const counts = summary[item.collection] ??= { source: 0, created: 0, skipped: 0, pending: 0 };
        counts.source++;
        if (item.exists) { counts.skipped++; continue; }
        if (!apply) { counts.pending++; continue; }
        try {
            // Atomic create precondition protects against concurrent edits too.
            await item.ref.create(item.data);
            counts.created++;
        } catch (error) {
            if (error.code === 6 || error.code === "already-exists") counts.skipped++;
            else throw error;
        }
    }
    return summary;
}

async function main(args) {
    const allowed = new Set(["--dry-run", "--apply", `--confirm-project=${TARGET_PROJECT}`]);
    if (args.some(arg => !allowed.has(arg))) throw new Error("Unknown argument");
    const apply = args.includes("--apply");
    if (apply && args.includes("--dry-run")) throw new Error("Choose dry-run or apply, not both");
    if (apply && !args.includes(`--confirm-project=${TARGET_PROJECT}`)) {
        throw new Error(`Writes require --apply --confirm-project=${TARGET_PROJECT}`);
    }
    if (process.env.FIRESTORE_EMULATOR_HOST || process.env.FIREBASE_AUTH_EMULATOR_HOST) {
        throw new Error("Unset emulator variables before running this live-project seed");
    }
    const sourceKey = JSON.parse(fs.readFileSync(path.join(__dirname, "../Firebase/firebase-key.json"), "utf8"));
    const targetKey = JSON.parse(fs.readFileSync(path.join(__dirname, "../Firebase/firebase-staging-key.json"), "utf8"));
    validateProjects(sourceKey, targetKey);
    const sourceApp = initializeApp({ credential: cert(sourceKey), projectId: SOURCE_PROJECT }, "seed-source");
    let targetApp;
    try {
        targetApp = initializeApp({ credential: cert(targetKey), projectId: TARGET_PROJECT }, "seed-target");
        const plan = await makePlan(getFirestore(sourceApp), getFirestore(targetApp));
        console.log(JSON.stringify({
            mode: apply ? "apply" : "dry-run", source: SOURCE_PROJECT, target: TARGET_PROJECT,
            documents: plan.map(item => ({ collection: item.collection, id: item.id, action: item.exists ? "skip" : "create" })),
        }, null, 2));
        console.log(JSON.stringify({ summary: await applyPlan(plan, apply) }, null, 2));
    } finally {
        await Promise.all([deleteApp(sourceApp), ...(targetApp ? [deleteApp(targetApp)] : [])]);
    }
}

module.exports = { normalize, validateProjects, applyPlan, makePlan, main };
if (require.main === module) {
    main(process.argv.slice(2)).catch(error => {
        console.error(`Seed failed: ${error.message}`);
        process.exitCode = 1;
    });
}
