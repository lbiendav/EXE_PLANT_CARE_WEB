"use strict";

// Destructive only to identities and documents created by this exact run.
// The application payment mode remains Disabled and this harness never calls a
// payment endpoint or sends verification/reset email.
const { randomBytes } = require("node:crypto");
const { spawn } = require("node:child_process");
const { initializeApp, cert, deleteApp } = require("firebase-admin/app");
const { getAuth } = require("firebase-admin/auth");
const { getFirestore, Timestamp } = require("firebase-admin/firestore");
const key = require("../Firebase/firebase-key.json");

if (process.argv.length !== 3 || process.argv[2] !== "--run-production" ||
    process.env.QA_PRODUCTION_CONFIRM !== "home-plant-app-dav" ||
    key.project_id !== "home-plant-app-dav") {
    console.error("Refusing to run without the exact production confirmation and project.");
    process.exit(1);
}

const stamp = `${Date.now()}-${process.pid}`;
const marker = `[TEST] PROD QA ${stamp}`;
const password = `${randomBytes(18).toString("base64url")}aA1!`;
const accounts = ["user", "admin"].map(role => {
    const uid = `homeplant-prod-qa-${role}-${stamp}`;
    // ASP.NET's EmailAddressAttribute rejects the reserved .invalid TLD in the
    // production runtime before the request reaches Firebase. example.com is
    // reserved for documentation and these addresses are never emailed.
    return { role, uid, email: `${uid}@example.com`, password };
});
const app = initializeApp({ credential: cert(key), projectId: key.project_id }, `production-e2e-${stamp}`);
const auth = getAuth(app);
const db = getFirestore(app);
const createdAuthUids = [];
const initialQaImages = new Set();

async function recursiveDelete(reference) {
    try { await db.recursiveDelete(reference); }
    catch (error) {
        if (error?.code !== 5 && !String(error?.message).includes("NOT_FOUND")) throw error;
    }
}

async function deleteQuery(query) {
    const snapshot = await query.get();
    for (const document of snapshot.docs) await recursiveDelete(document.ref);
}

async function cleanup() {
    for (const account of accounts) {
        const plants = await db.collection("users").doc(account.uid).collection("user_plants").get();
        for (const plant of plants.docs) await recursiveDelete(db.collection("plants").doc(plant.id));
        await recursiveDelete(db.collection("users").doc(account.uid));
        await recursiveDelete(db.collection("subscriptions").doc(account.uid));
        await deleteQuery(db.collection("ai_diagnoses").where("userId", "==", account.uid));
        await deleteQuery(db.collection("subscription_orders").where("userId", "==", account.uid));
    }

    for (const title of [marker, `${marker} edited`])
        await deleteQuery(db.collection("articles").where("title", "==", title));
    for (const name of [marker, `${marker} edited`])
        await deleteQuery(db.collection("sample_plants").where("name", "==", name));
    await deleteQuery(db.collection("plant_templates").where("name", "==", marker));
    await deleteQuery(db.collection("community_posts").where("content", "==", marker));
    await deleteQuery(db.collection("qa_threads").where("title", "==", marker));

    const qaImages = await db.collection("uploaded_images").where("fileName", "==", "homeplant-qa.png").get();
    for (const image of qaImages.docs)
        if (!initialQaImages.has(image.id)) await recursiveDelete(image.ref);

    for (const uid of createdAuthUids.reverse()) {
        try { await auth.deleteUser(uid); }
        catch (error) { if (error?.code !== "auth/user-not-found") throw error; }
    }
}

async function runChild() {
    const child = spawn(process.execPath, [require.resolve("./web_flow_test.js"), "--run-production"], {
        cwd: require("node:path").resolve(__dirname, ".."),
        stdio: "inherit",
        env: {
            ...process.env,
            QA_BASE_URL: "https://homeplant-production.onrender.com",
            QA_PRODUCTION_CONFIRM: "home-plant-app-dav",
            QA_MARKER: marker,
            QA_ACCOUNTS_JSON: JSON.stringify({ project: key.project_id, accounts })
        }
    });
    return await new Promise((resolve, reject) => {
        child.once("error", reject);
        child.once("exit", code => resolve(code ?? 1));
    });
}

(async () => {
    let exitCode = 1;
    try {
        const existingImages = await db.collection("uploaded_images").where("fileName", "==", "homeplant-qa.png").get();
        existingImages.docs.forEach(document => initialQaImages.add(document.id));
        for (const account of accounts) {
            await auth.createUser({
                uid: account.uid,
                email: account.email,
                password: account.password,
                displayName: `[TEST] Production ${account.role}`,
                emailVerified: true,
                disabled: false
            });
            createdAuthUids.push(account.uid);
            await db.collection("users").doc(account.uid).create({
                uid: account.uid,
                displayName: `[TEST] Production ${account.role}`,
                email: account.email,
                phone: "",
                avatarUrl: "",
                role: account.role,
                isLocked: false,
                createdAt: Timestamp.now(),
                emailCareReminders: false
            });
        }
        exitCode = await runChild();
    } finally {
        try { await cleanup(); }
        finally { await deleteApp(app); }
    }
    process.exitCode = exitCode;
})().catch(async error => {
    console.error(`Production E2E harness failed: ${error.message}`);
    try { await cleanup(); } catch (cleanupError) { console.error(`Cleanup failed: ${cleanupError.message}`); }
    try { await deleteApp(app); } catch { }
    process.exitCode = 1;
});
