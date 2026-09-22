"use strict";

// Adds only fixed public catalog/community content to Production. The stable IDs
// and create-only writes make repeated runs safe and preserve later admin edits.
const fs = require("node:fs");
const path = require("node:path");
const { initializeApp, cert, deleteApp } = require("firebase-admin/app");
const { getFirestore, Timestamp } = require("firebase-admin/firestore");

const TARGET_PROJECT = "homeplant-production";
const confirm = `--confirm-project=${TARGET_PROJECT}`;
const at = value => Timestamp.fromDate(new Date(value));

const documents = [
  ["community_posts", "homeplant-tip-watering-2026", {postId:"homeplant-tip-watering-2026",authorId:"homeplant-editorial",authorName:"Đội ngũ HomePlant",authorAvatar:"",content:"Mẹo tưới cây: hãy kiểm tra 2–3 cm đất mặt trước khi tưới. Nếu đất vẫn còn ẩm, chờ thêm một ngày sẽ an toàn hơn là tưới theo lịch cứng.",images:[],likeCount:28,commentCount:6,status:"active",createdAt:at("2026-09-19T02:00:00Z")}],
  ["community_posts", "homeplant-tip-light-2026", {postId:"homeplant-tip-light-2026",authorId:"homeplant-editorial",authorName:"Đội ngũ HomePlant",authorAvatar:"",content:"Lá cây nghiêng hẳn về phía cửa sổ là dấu hiệu cây đang tìm sáng. Xoay chậu một phần tư vòng mỗi tuần để tán cây phát triển cân đối hơn.",images:[],likeCount:34,commentCount:4,status:"active",createdAt:at("2026-09-20T02:00:00Z")}],
  ["community_posts", "homeplant-tip-yellow-leaves-2026", {postId:"homeplant-tip-yellow-leaves-2026",authorId:"homeplant-editorial",authorName:"Đội ngũ HomePlant",authorAvatar:"",content:"Một lá già vàng tự nhiên không đáng lo. Nhưng nếu nhiều lá vàng cùng lúc, hãy kiểm tra úng nước, độ thoáng của giá thể và ánh sáng trước khi bón thêm phân.",images:[],likeCount:41,commentCount:9,status:"active",createdAt:at("2026-09-21T02:00:00Z")}],
  ["community_posts", "homeplant-tip-clean-leaves-2026", {postId:"homeplant-tip-clean-leaves-2026",authorId:"homeplant-editorial",authorName:"Đội ngũ HomePlant",authorAvatar:"",content:"Cuối tuần xanh: dùng khăn mềm ẩm lau hai mặt lá. Lá sạch quang hợp tốt hơn, đồng thời bạn có thể phát hiện sớm rệp sáp và nhện đỏ.",images:[],likeCount:52,commentCount:11,status:"active",createdAt:at("2026-09-22T02:00:00Z")}],
  ["sample_plants", "golden-pothos", {name:"Trầu Bà Vàng",scientificName:"Epipremnum aureum",description:"Cây dây leo dễ chăm, nổi bật với lá xanh pha vàng và phù hợp cho người mới bắt đầu.",imageUrl:"https://thumb.wikimedia.org/wikipedia/commons/thumb/9/9d/Epipremnum_aureum_31082012.jpg/960px-Epipremnum_aureum_31082012.jpg",createdAt:at("2026-09-22T03:00:00Z"),care:{light:"Ánh sáng gián tiếp từ trung bình đến sáng; tránh nắng gắt.",water:"Tưới khi 2–3 cm đất mặt đã khô.",soil:"Giá thể tơi xốp, thoát nước tốt.",fertilizer:"Bón phân loãng mỗi 4–6 tuần trong mùa sinh trưởng."},diseases:[{issue:"Lá vàng",cause:"Tưới quá nhiều hoặc thoát nước kém.",treatment:"Để đất khô hơn giữa hai lần tưới và kiểm tra lỗ thoát nước."}]}],
  ["sample_plants", "zz-plant", {name:"Kim Tiền",scientificName:"Zamioculcas zamiifolia",description:"Cây nội thất bền bỉ, chịu thiếu sáng và có thân lá xanh bóng, thích hợp cho văn phòng.",imageUrl:"https://thumb.wikimedia.org/wikipedia/commons/thumb/b/b9/Zamioculcas_zamiifolia_Chameleon_1.jpg/960px-Zamioculcas_zamiifolia_Chameleon_1.jpg",createdAt:at("2026-09-22T03:01:00Z"),care:{light:"Chịu sáng yếu nhưng phát triển tốt nhất ở ánh sáng gián tiếp.",water:"Chỉ tưới khi phần lớn giá thể đã khô.",soil:"Đất thoát nước nhanh, có perlite hoặc đá bọt.",fertilizer:"Bón nhẹ mỗi 6–8 tuần vào mùa ấm."},diseases:[{issue:"Thân mềm, lá vàng",cause:"Úng nước gây thối củ.",treatment:"Ngừng tưới, cắt phần thối và thay giá thể khô thoáng."}]}],
  ["sample_plants", "peace-lily", {name:"Lan Ý",scientificName:"Spathiphyllum wallisii",description:"Cây lá xanh đậm với mo hoa trắng thanh lịch, ưa ẩm và ánh sáng dịu trong nhà.",imageUrl:"https://thumb.wikimedia.org/wikipedia/commons/thumb/a/a1/Peace_lily_-_2.jpg/960px-Peace_lily_-_2.jpg",createdAt:at("2026-09-22T03:02:00Z"),care:{light:"Ánh sáng gián tiếp; ánh sáng tốt giúp cây ra hoa.",water:"Giữ đất ẩm vừa, tưới khi mặt đất bắt đầu se khô.",soil:"Giá thể giàu hữu cơ nhưng thoát nước tốt.",fertilizer:"Bón phân cân bằng loãng mỗi 6 tuần."},diseases:[{issue:"Đầu lá nâu",cause:"Không khí khô, nước nhiều khoáng hoặc bón phân đậm.",treatment:"Tăng ẩm, dùng nước ít khoáng và giảm nồng độ phân."}]}],
  ["sample_plants", "rubber-plant", {name:"Đa Búp Đỏ",scientificName:"Ficus elastica",description:"Cây thân gỗ trong nhà có lá lớn bóng, tạo điểm nhấn mạnh mẽ cho không gian sống.",imageUrl:"https://thumb.wikimedia.org/wikipedia/commons/thumb/3/33/Gummibaum_%28Ficus_elastica_Robusta%29.jpg/960px-Gummibaum_%28Ficus_elastica_Robusta%29.jpg",createdAt:at("2026-09-22T03:03:00Z"),care:{light:"Ánh sáng gián tiếp mạnh, có thể nhận nắng sớm nhẹ.",water:"Tưới khi 3–5 cm đất mặt khô.",soil:"Đất tơi xốp, giữ ẩm vừa và thoát nước tốt.",fertilizer:"Bón phân mỗi tháng trong mùa sinh trưởng."},diseases:[{issue:"Rụng lá",cause:"Thay đổi môi trường đột ngột, thiếu sáng hoặc úng nước.",treatment:"Ổn định vị trí, điều chỉnh ánh sáng và chu kỳ tưới."}]}],
];

async function main(args) {
  const allowed = new Set(["--dry-run", "--apply", confirm]);
  if (args.some(arg => !allowed.has(arg))) throw new Error("Unknown argument");
  const apply = args.includes("--apply");
  if (apply && !args.includes(confirm)) throw new Error(`Writes require --apply ${confirm}`);
  const keyPath = process.env.PRODUCTION_FIREBASE_KEY_PATH;
  if (!keyPath) throw new Error("Set PRODUCTION_FIREBASE_KEY_PATH");
  const key = JSON.parse(fs.readFileSync(path.resolve(keyPath), "utf8"));
  if (key.project_id !== TARGET_PROJECT) throw new Error("Credential project mismatch");
  const app = initializeApp({credential:cert(key),projectId:TARGET_PROJECT}, "curated-content-seed");
  try {
    const db = getFirestore(app); const result=[];
    for (const [collection,id,data] of documents) {
      const ref=db.collection(collection).doc(id); const existing=await ref.get();
      if (existing.exists) { result.push({collection,id,action:"skip"}); continue; }
      if (apply) await ref.create(data);
      result.push({collection,id,action:apply?"created":"pending"});
    }
    console.log(JSON.stringify({project:TARGET_PROJECT,mode:apply?"apply":"dry-run",documents:result},null,2));
  } finally { await deleteApp(app); }
}

module.exports={documents,main};
if(require.main===module)main(process.argv.slice(2)).catch(error=>{console.error(error.message);process.exitCode=1;});
