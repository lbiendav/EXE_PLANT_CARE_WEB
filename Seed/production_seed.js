"use strict";

// Copies only shared, public reference content into Production. Authentication,
// users, subscriptions, payments and all user subcollections are out of scope.
const fs = require("node:fs");
const path = require("node:path");
const { initializeApp, cert, deleteApp } = require("firebase-admin/app");
const { getFirestore } = require("firebase-admin/firestore");
const { normalize, applyPlan } = require("./staging_seed");

const SOURCE_PROJECT = "home-plant-app-dav";
const TARGET_PROJECT = "homeplant-production";
const COLLECTIONS = ["sample_plants", "plant_templates", "articles"];
const MAX_PER_COLLECTION = 100;

function validateProjects(sourceKey, targetKey) {
    if (sourceKey.project_id !== SOURCE_PROJECT || targetKey.project_id !== TARGET_PROJECT) {
        throw new Error("Credential project mismatch: refusing to proceed");
    }
}

async function makePlan(source, target) {
    const plan = [];
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

async function main(args) {
    const confirm = `--confirm-project=${TARGET_PROJECT}`;
    const allowed = new Set(["--dry-run", "--apply", confirm]);
    if (args.some(arg => !allowed.has(arg))) throw new Error("Unknown argument");
    const apply = args.includes("--apply");
    if (apply && args.includes("--dry-run")) throw new Error("Choose dry-run or apply, not both");
    if (apply && !args.includes(confirm)) throw new Error(`Writes require --apply ${confirm}`);
    if (process.env.FIRESTORE_EMULATOR_HOST || process.env.FIREBASE_AUTH_EMULATOR_HOST) {
        throw new Error("Unset emulator variables before running this live-project seed");
    }

    const sourcePath = path.join(__dirname, "../Firebase/firebase-key.json");
    const targetPath = process.env.PRODUCTION_FIREBASE_KEY_PATH;
    if (!targetPath) throw new Error("Set PRODUCTION_FIREBASE_KEY_PATH to the Production service-account JSON file");
    const sourceKey = JSON.parse(fs.readFileSync(sourcePath, "utf8"));
    const targetKey = JSON.parse(fs.readFileSync(path.resolve(targetPath), "utf8"));
    validateProjects(sourceKey, targetKey);

    const sourceApp = initializeApp({ credential: cert(sourceKey), projectId: SOURCE_PROJECT }, "production-seed-source");
    let targetApp;
    try {
        targetApp = initializeApp({ credential: cert(targetKey), projectId: TARGET_PROJECT }, "production-seed-target");
        const plan = await makePlan(getFirestore(sourceApp), getFirestore(targetApp));
        console.log(JSON.stringify({
            mode: apply ? "apply" : "dry-run",
            source: SOURCE_PROJECT,
            target: TARGET_PROJECT,
            documents: plan.map(item => ({ collection: item.collection, id: item.id, action: item.exists ? "skip" : "create" })),
        }, null, 2));
        console.log(JSON.stringify({ summary: await applyPlan(plan, apply) }, null, 2));
    } finally {
        await Promise.all([deleteApp(sourceApp), ...(targetApp ? [deleteApp(targetApp)] : [])]);
    }
}

module.exports = { validateProjects, makePlan, main };
if (require.main === module) {
    main(process.argv.slice(2)).catch(error => {
        console.error(`Production seed failed: ${error.message}`);
        process.exitCode = 1;
    });
}
