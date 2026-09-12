"use strict";
// Runs only against staging (or loopback with staging credentials). No real email.
if (process.argv.length !== 3 || process.argv[2] !== "--run") {
    console.log("This test changes staging data. Run explicitly: node Seed/web_flow_test.js --run");
    process.exit(process.argv.length > 2 ? 1 : 0);
}
const assert = require("node:assert/strict");
const { initializeApp, cert, deleteApp } = require("firebase-admin/app");
const { getFirestore, Timestamp } = require("firebase-admin/firestore");
const credentials = require("../.env.test-accounts.json");
const key = require("../Firebase/firebase-staging-key.json");
const base = process.env.QA_BASE_URL || "https://homeplant-staging.onrender.com";
if (credentials.project !== "homeplant-staging-dav" || key.project_id !== credentials.project ||
    !["https://homeplant-staging.onrender.com", "http://localhost:18082"].includes(base)) throw Error("Unsafe QA target");
const app = initializeApp({ credential: cert(key), projectId: key.project_id });
const db = getFirestore(app);
const failures = [];
let passed = 0;
const marker = "[TEST] QA " + Date.now();

class Session {
    cookies = new Map();
    async request(path, fields) {
        const headers = { Cookie: [...this.cookies].map(([k,v]) => `${k}=${v}`).join("; ") };
        const options = { headers, redirect: "manual", signal: AbortSignal.timeout(55000) };
        if (fields) { options.method = "POST"; headers["Content-Type"] = "application/x-www-form-urlencoded"; options.body = new URLSearchParams(fields); }
        const r = await fetch(base + path, options);
        for (const cookie of r.headers.getSetCookie()) { const pair = cookie.split(";")[0]; const i=pair.indexOf("=");this.cookies.set(pair.slice(0,i),pair.slice(i+1)); }
        return { status:r.status, location:r.headers.get("location"), body:await r.text() };
    }
    async post(path, fields, formPath = path) {
        const form = await this.request(formPath);
        const token = form.body.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
        return this.request(path, { ...fields, ...(token ? { __RequestVerificationToken:token[1] } : {}) });
    }
    async upload(path, fields, fileField) {
        const form=await this.request(path);
        const token=form.body.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
        const body=new FormData();for(const [k,v] of Object.entries(fields))body.set(k,v);
        if(token)body.set("__RequestVerificationToken",token[1]);
        body.set(fileField,new Blob([Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=","base64")],{type:"image/png"}),"homeplant-qa.png");
        const r=await fetch(base+path,{method:"POST",body,headers:{Cookie:[...this.cookies].map(([k,v])=>`${k}=${v}`).join("; ")},redirect:"manual",signal:AbortSignal.timeout(55000)});
        return {status:r.status,location:r.headers.get("location"),body:await r.text()};
    }
}
async function check(name, action) { try { await action(); passed++;console.log("PASS " + name); } catch(e) { failures.push(name);console.log("FAIL " + name + ": " + e.message); } }
function status(r, code) { assert.equal(r.status, code, `HTTP ${r.status} expected ${code}`); }
async function login(session, account) { const r=await session.post("/Account/Login",{Email:account.email,Password:account.password});status(r,302);assert.ok(["/Home","/Home/Index"].includes(r.location)); }
const userAccount=credentials.accounts.find(a=>a.role==="user");
const adminAccount=credentials.accounts.find(a=>a.role==="admin");
for(const account of [userAccount,adminAccount]) {
    if(!account || account.uid!==`homeplant-qa-${account.role}-20260911` ||
        account.email!==`homeplant.qa.${account.role}.20260911@example.invalid`) throw Error("Only designated QA accounts may be used");
}
const user=new Session(),admin=new Session(),anonymous=new Session();

async function run() {
    await check("anonymous cannot open garden",async()=>status(await anonymous.request("/Plant/Index"),302));
    await check("user login",()=>login(user,userAccount));
    await check("admin login",()=>login(admin,adminAccount));
    await check("user cannot open admin dashboard",async()=>status(await user.request("/Admin/Dashboard"),302));
    await check("missing article gives 404",async()=>status(await anonymous.request("/Article/Details/qa-nonexistent"),404));
    await check("POST without antiforgery token rejected",async()=>status(await user.request("/Plant/Create",{Nickname:marker+" csrf",PlantSampleId:"PLANT_MASTER_999"}),400));
    for(const p of ["/Home/Index","/Library/Index","/Article/Index","/Plant/Index","/Plant/Create","/Profile/Index","/Profile/Edit","/Profile/ChangePassword","/Notifications/Index","/Notifications/UnreadCount"])
        await check("user GET "+p,async()=>status(await user.request(p),200));
    for(const p of ["Dashboard","Users","SamplePlants","PlantTemplates","CommunityPosts","QaThreads","AiDiagnoses","CreateSamplePlant"])
        await check("admin GET "+p,async()=>status(await admin.request("/Admin/"+p),200));
    await check("edit own profile",async()=>{
        status(await user.post("/Profile/Edit",{FullName:"[TEST] HomePlant user edited",Phone:"",AvatarUrl:"",Id:adminAccount.uid}),302);
        assert.equal((await db.collection("users").doc(userAccount.uid).get()).data().displayName,"[TEST] HomePlant user edited");
        assert.equal((await db.collection("users").doc(adminAccount.uid).get()).data().role,"admin");
    });
    await check("invalid template rejected",async()=>{
        const r=await user.post("/Plant/Create",{Nickname:marker+" invalid",PlantSampleId:"qa-missing-template",CurrentStatus:"Khỏe mạnh"});
        status(r,200);assert.ok(r.body.includes("validation"));
    });
    const speciesSnapshot=await db.collection("sample_plants").limit(2).get();
    if(speciesSnapshot.empty)throw Error("At least one sample plant is required");
    const createSpecies=speciesSnapshot.docs[0];
    let plantId, plantCreatedAt, plantImageUrl, reminderId;
    await check("create garden plant with uploaded image",async()=>{
        status(await user.upload("/Plant/Create",{Nickname:marker,PlantSampleId:createSpecies.id,CurrentStatus:"Khỏe mạnh",WateringFrequency:"7",WateringFrequencyUnit:"Days",FertilizingFrequency:"30",FertilizingFrequencyUnit:"Days",RepottingFrequency:"365",RepottingFrequencyUnit:"Days"},"Photo"),302);
        const s=await db.collection("users").doc(userAccount.uid).collection("user_plants").where("customName","==",marker).get();
        assert.equal(s.size,1);plantId=s.docs[0].id;plantCreatedAt=s.docs[0].data().createdAt;
        plantImageUrl=s.docs[0].data().imageUrl;
        assert.equal(s.docs[0].data().wateringFrequency,7);assert.ok(s.docs[0].data().nextWateringAt);
        assert.match(plantImageUrl,/^(https:\/\/|\/Image\/)/,"Upload did not persist the plant image URL");
        if(plantImageUrl.startsWith("/"))status(await user.request(plantImageUrl),200);
    });
    if(plantId){
        await check("garden plant details include schedule and history",async()=>{
            const r=await user.request("/Plant/Details/"+plantId);status(r,200);
            assert.ok(r.body.includes("Lịch sử chăm sóc"));
            assert.ok(r.body.includes("data-countdown-at"));
        });
        await check("garden plant edit form",async()=>status(await user.request("/Plant/Edit/"+plantId),200));
        await check("another account cannot read plant",async()=>status(await admin.request("/Plant/Details/"+plantId),404));
        await check("another account cannot edit plant",async()=>status(await admin.request("/Plant/Edit/"+plantId),404));
        await check("edit garden plant and preserve creation time",async()=>{
            const editTemplate=speciesSnapshot.docs.find(doc=>doc.id!==createSpecies.id) || createSpecies;
            status(await user.post("/Plant/Edit/"+plantId,{Nickname:marker+" edited",PlantSampleId:editTemplate.id,CurrentStatus:"Cần chú ý"}),302);
            const data=(await db.collection("users").doc(userAccount.uid).collection("user_plants").doc(plantId).get()).data();
            assert.equal(data.customName,marker+" edited");assert.equal(data.templateId,editTemplate.id);assert.equal(data.status,"warning");assert.ok(data.createdAt.isEqual(plantCreatedAt));
        });
        await check("update all schedules from details with seconds and minutes",async()=>{
            status(await user.post("/Plant/UpdateSchedule/"+plantId,{"Schedule.WateringFrequency":"30","Schedule.WateringFrequencyUnit":"Seconds","Schedule.FertilizingFrequency":"2","Schedule.FertilizingFrequencyUnit":"Minutes","Schedule.RepottingFrequency":"4","Schedule.RepottingFrequencyUnit":"Hours"}),302);
            const data=(await db.collection("users").doc(userAccount.uid).collection("user_plants").doc(plantId).get()).data();
            assert.equal(data.wateringFrequencyUnit,"Seconds");assert.equal(data.fertilizingFrequencyUnit,"Minutes");assert.equal(data.repottingFrequencyUnit,"Hours");
            assert.ok(data.nextWateringAt&&data.nextFertilizingAt&&data.nextRepottingAt);
            const garden=await user.request("/Plant/Index");status(garden,200);
            for(const label of ["Tưới nước","Bón phân","Thay chậu","Tưới gần nhất","Bón phân gần nhất","Thay chậu gần nhất"])assert.ok(garden.body.includes(label),"Garden is missing "+label);
        });
        await check("overdue schedule creates one notification",async()=>{
            const plantRef=db.collection("users").doc(userAccount.uid).collection("user_plants").doc(plantId);
            await plantRef.update({nextWateringAt:Timestamp.fromMillis(Date.now()-60000)});
            const response=await user.request("/Notifications/UnreadCount");status(response,200);
            const reminders=await db.collection("users").doc(userAccount.uid).collection("notifications").where("plantId","==",plantId).get();
            const watering=reminders.docs.filter(doc=>doc.data().careType==="Watering");
            assert.equal(watering.length,1);assert.equal(watering[0].data().isRead,false);reminderId=watering[0].id;
        });
        await check("care log form",async()=>status(await user.request("/CareLog/Create?plantId="+plantId),200));
        await check("add care log and enforce current user",async()=>{
            status(await user.post("/CareLog/Create?plantId="+plantId,{ActionType:"Watering",Note:marker,ImageUrl:"",UserId:adminAccount.uid}),302);
            const s=await db.collection("plants").doc(plantId).collection("careLogs").get();
            assert.equal(s.size,1);assert.equal(s.docs[0].data().UserId,userAccount.uid);
            const plant=(await db.collection("users").doc(userAccount.uid).collection("user_plants").doc(plantId).get()).data();
            assert.ok(plant.lastWatered,"Watering did not update the plant summary");
            assert.ok(plant.nextWateringAt.toMillis()>plant.lastWatered.toMillis(),"Watering did not advance the reminder");
            assert.equal((await db.collection("users").doc(userAccount.uid).collection("notifications").doc(reminderId).get()).data().isRead,true,"Care action did not close its reminder");
        });
        await check("care log list",async()=>status(await user.request("/CareLog/Index?plantId="+plantId),200));
        await check("other account cannot read care logs",async()=>status(await admin.request("/CareLog/Index?plantId="+plantId),404));
        await check("invalid care action rejected",async()=>status(await user.post("/CareLog/Create?plantId="+plantId,{ActionType:"not-a-real-action",Note:marker}),200));
        await check("GET cannot delete plant",async()=>status(await user.request("/Plant/Delete/"+plantId),405));
        await check("delete care log",async()=>{
            const logs=await db.collection("plants").doc(plantId).collection("careLogs").get();
            assert.equal(logs.size,1);const id=logs.docs[0].id;
            status(await user.post("/CareLog/Delete?plantId="+plantId+"&id="+id,{},"/CareLog/Index?plantId="+plantId),302);
            assert.equal((await logs.docs[0].ref.get()).exists,false);
        });
        await check("delete garden plant",async()=>{
            status(await user.post("/CareLog/Create?plantId="+plantId,{ActionType:"Observation",Note:marker+" cascade"}),302);
            status(await admin.post("/Plant/Delete/"+plantId,{},"/Profile/Edit"),404);
            assert.equal((await db.collection("plants").doc(plantId).collection("careLogs").get()).size,1,"Other user removed care logs");
            status(await user.post("/Plant/Delete/"+plantId,{},"/Plant/Index"),302);
            assert.equal((await db.collection("users").doc(userAccount.uid).collection("user_plants").doc(plantId).get()).exists,false);
            assert.equal((await db.collection("plants").doc(plantId).collection("careLogs").get()).size,0,"Orphaned care logs after plant deletion");
            assert.equal((await db.collection("users").doc(userAccount.uid).collection("notifications").where("plantId","==",plantId).get()).size,0,"Orphaned reminders after plant deletion");
            if(plantImageUrl.startsWith("/Image/")){
                const imageId=plantImageUrl.slice("/Image/".length);
                assert.equal((await db.collection("uploaded_images").doc(imageId).get()).exists,false,"Orphaned image metadata after plant deletion");
                assert.equal((await db.collection("uploaded_images").doc(imageId).collection("chunks").get()).size,0,"Orphaned image chunks after plant deletion");
            }
        });
    }
    let articleId,articleTime;
    const article={Title:marker,Content:"Synthetic article for QA",CoverImage:""};
    await check("blank article rejected",async()=>status(await admin.post("/Article/Create",{Title:"",Content:""}),200));
    await check("admin creates article",async()=>{
        status(await admin.post("/Article/Create",article),302);
        const s=await db.collection("articles").where("title","==",marker).get();assert.equal(s.size,1);articleId=s.docs[0].id;articleTime=s.docs[0].data().createdAt;
    });
    if(articleId){
        await check("admin edits article without resetting creation date",async()=>{
            status(await admin.post("/Article/Edit/"+articleId,{...article,Title:marker+" edited"}),302);
            const data=(await db.collection("articles").doc(articleId).get()).data();assert.equal(data.title,marker+" edited");assert.ok(data.createdAt.isEqual(articleTime));
        });
        await check("public article detail",async()=>status(await anonymous.request("/Article/Details/"+articleId),200));
        await check("normal user cannot delete article",async()=>{status(await user.post("/Article/Delete/"+articleId,{},"/Profile/Edit"),302);assert.equal((await db.collection("articles").doc(articleId).get()).exists,true);});
        await check("GET cannot delete article",async()=>status(await admin.request("/Article/Delete/"+articleId),405));
        await check("admin deletes article",async()=>{status(await admin.post("/Article/Delete/"+articleId,{},"/Article/Index"),302);assert.equal((await db.collection("articles").doc(articleId).get()).exists,false);});
    }
    // Synthetic fixtures for admin-only lists which have no create screen.
    const now=Timestamp.now();
    const fixtures=[
        ["plant_templates","PlantTemplates","DeletePlantTemplate",{templateId:marker,name:marker,scientificName:"QA",description:"Synthetic",imageUrl:"",careInstructions:{light:"",water:"",soil:"",fertilizer:""},isFeatured:false,createdAt:now}],
        ["community_posts","CommunityPosts","DeleteCommunityPost",{postId:marker,authorId:userAccount.uid,authorName:"[TEST] QA",authorAvatar:"",content:marker,images:[],likeCount:0,commentCount:0,status:"active",createdAt:now}],
        ["qa_threads","QaThreads","DeleteQaThread",{threadId:marker,userId:userAccount.uid,userName:"[TEST] QA",userAvatar:"",expertId:adminAccount.uid,expertName:"[TEST] QA",title:marker,lastMessage:"Synthetic",lastMessageAt:now,status:"open",createdAt:now}],
        ["ai_diagnoses","AiDiagnoses","DeleteAiDiagnosis",{diagnosisId:marker,userId:userAccount.uid,uploadedImageUrl:"",result:{diseaseName:"QA only",confidence:0.5,cause:"Synthetic",treatment:"Synthetic"},createdAt:now}],
    ];
    for(const [collection,list,remove,data] of fixtures){
        const ref=db.collection(collection).doc("qa-"+Date.now());await ref.create(data);
        await check("admin reads populated "+collection,async()=>status(await admin.request("/Admin/"+list),200));
        await check("admin deletes QA "+collection,async()=>{status(await admin.post("/Admin/"+remove+"/"+ref.id,{},"/Admin/"+list),302);assert.equal((await ref.get()).exists,false);});
    }
    let sampleId, sampleImageUrl;
    const sample={Name:marker,ScientificName:"QA plant",Description:"Synthetic test record",ExistingImageUrl:"",Light:"Indirect",Water:"Weekly",Soil:"Test soil",Fertilizer:"None"};
    await check("admin creates sample plant with uploaded image",async()=>{
        status(await admin.upload("/Admin/CreateSamplePlant",sample,"Photo"),302);
        const s=await db.collection("sample_plants").where("name","==",marker).get();assert.equal(s.size,1);sampleId=s.docs[0].id;
        sampleImageUrl=s.docs[0].data().imageUrl;
        assert.match(sampleImageUrl,/^(https:\/\/|\/Image\/)/,"Upload did not persist the sample plant image URL");
        if(sampleImageUrl.startsWith("/"))status(await admin.request(sampleImageUrl),200);
    });
    if(sampleId){
        await check("admin edits sample",async()=>{
            status(await admin.post("/Admin/EditSamplePlant/"+sampleId,{...sample,Name:marker+" edited"}),302);
            const data=(await db.collection("sample_plants").doc(sampleId).get()).data();
            assert.equal(data.name,marker+" edited");assert.equal(data.imageUrl,sampleImageUrl);
        });
        await check("public sample detail",async()=>status(await anonymous.request("/Library/Details/"+sampleId),200));
        await check("GET cannot delete sample",async()=>status(await admin.request("/Admin/DeleteSamplePlant/"+sampleId),405));
        await check("admin deletes sample",async()=>{
            status(await admin.post("/Admin/DeleteSamplePlant/"+sampleId,{},"/Admin/SamplePlants"),302);
            assert.equal((await db.collection("sample_plants").doc(sampleId).get()).exists,false);
            if(sampleImageUrl.startsWith("/Image/")){
                const imageId=sampleImageUrl.slice("/Image/".length);
                assert.equal((await db.collection("uploaded_images").doc(imageId).get()).exists,false,"Orphaned sample image metadata after deletion");
                assert.equal((await db.collection("uploaded_images").doc(imageId).collection("chunks").get()).size,0,"Orphaned sample image chunks after deletion");
            }
        });
    }
    await check("upload QA profile image",async()=>{
        status(await user.upload("/Profile/Edit",{FullName:"[TEST] HomePlant user",Phone:""},"avatar"),302);
        const avatar=(await db.collection("users").doc(userAccount.uid).get()).data().avatarUrl;
        assert.ok(typeof avatar==="string"&&/^(https:\/\/|\/Image\/)/.test(avatar),"Upload did not persist image URL");
        if(avatar.startsWith("/"))status(await user.request(avatar),200);
    });
    const changedPassword=userAccount.password+"-QA";let changed=false;
    try{
        await check("change password",async()=>{status(await user.post("/Profile/ChangePassword",{CurrentPassword:userAccount.password,NewPassword:changedPassword,ConfirmPassword:changedPassword}),302);changed=true;await login(new Session(),{...userAccount,password:changedPassword});});
        if(changed)await check("old password rejected",async()=>status(await new Session().post("/Account/Login",{Email:userAccount.email,Password:userAccount.password}),200));
    }finally{
        if(changed){const r=await user.post("/Profile/ChangePassword",{CurrentPassword:changedPassword,NewPassword:userAccount.password,ConfirmPassword:userAccount.password});status(r,302);}
    }
    try {
        await check("GET cannot ban user",async()=>status(await admin.request("/Admin/Ban/"+userAccount.uid),405));
        await check("admin bans QA user",async()=>{status(await admin.post("/Admin/Ban/"+userAccount.uid,{},"/Admin/Users"),302);assert.equal((await db.collection("users").doc(userAccount.uid).get()).data().isLocked,true);});
        await check("ban invalidates existing session",async()=>status(await user.request("/Plant/Index"),302));
        await check("banned account cannot log in",async()=>{const s=new Session();status(await s.post("/Account/Login",{Email:userAccount.email,Password:userAccount.password}),200);status(await s.request("/Plant/Index"),302);});
    } finally { status(await admin.post("/Admin/UnBan/"+userAccount.uid,{},"/Admin/Users"),302); }
    try{
        await db.collection("users").doc(adminAccount.uid).update({role:"user"});
        await check("role downgrade invalidates admin access",async()=>status(await admin.request("/Admin/Dashboard"),302));
    }finally{await db.collection("users").doc(adminAccount.uid).update({role:"admin"});}
    await check("user login after unban",()=>login(user,userAccount));
    await check("logout removes session",async()=>{status(await user.post("/Account/Logout",{},"/Profile/Index"),302);status(await user.request("/Plant/Index"),302);});
    console.log(JSON.stringify({marker,passed,failures,plantId,sampleId},null,2));
}
run().catch(e=>{console.error("QA aborted: "+e.message);process.exitCode=1;}).finally(async()=>{await deleteApp(app);if(failures.length)process.exitCode=1;});
