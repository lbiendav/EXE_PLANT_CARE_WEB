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
  ["articles", "GUIDE_WATERING_2026", {title:"Tưới cây đúng cách: đọc đất thay vì nhìn lịch",content:"Tưới nước là việc đơn giản nhưng cũng là nguyên nhân phổ biến nhất khiến cây trong nhà suy yếu. Một lịch cố định chỉ nên dùng để nhắc bạn kiểm tra cây, không phải mệnh lệnh phải tưới.\n\nHãy chạm vào 2–3 cm đất mặt. Nếu đất còn mát và ẩm, cây chưa cần thêm nước. Khi đất đã se khô, tưới chậm quanh bầu cho đến khi nước bắt đầu thoát ra đáy chậu, sau đó đổ bỏ nước đọng trong đĩa.\n\nChậu nhỏ, phòng nóng và nhiều ánh sáng sẽ khô nhanh hơn. Ngược lại, chậu lớn, mùa mưa hoặc phòng điều hòa ít nắng cần khoảng cách tưới dài hơn. Quan sát trọng lượng chậu và độ căng của lá sẽ giúp bạn hiểu nhịp uống nước riêng của từng cây.\n\nDấu hiệu tưới quá nhiều thường là lá vàng mềm, đất có mùi và thân sát gốc nhũn. Thiếu nước thường làm lá rũ nhưng đất khô nhẹ và chậu rất nhẹ. Khi chưa chắc chắn, chờ thêm một ngày thường an toàn hơn là tưới thêm.",coverImage:"https://thumb.wikimedia.org/wikipedia/commons/thumb/9/9d/Epipremnum_aureum_31082012.jpg/960px-Epipremnum_aureum_31082012.jpg",tags:["Tưới nước","Cơ bản","Cây trong nhà"],views:0,createdAt:at("2026-09-22T04:00:00Z")}],
  ["articles", "GUIDE_LIGHT_2026", {title:"Chọn vị trí ánh sáng phù hợp cho cây trong nhà",content:"Ánh sáng gián tiếp mạnh là vùng sáng gần cửa sổ nhưng tia nắng không chiếu gắt trực tiếp lên lá trong nhiều giờ. Đây là vị trí phù hợp với phần lớn cây lá nhiệt đới.\n\nCửa sổ hướng đông thường có nắng sáng dịu. Hướng nam và tây có thể rất mạnh, nên dùng rèm mỏng hoặc đặt cây lùi xa kính. Ở vị trí thiếu sáng, cây mọc chậm, khoảng cách giữa các lá dài và tán nghiêng về phía cửa sổ.\n\nBạn nên xoay chậu một phần tư vòng mỗi tuần để tán phát triển cân đối. Không chuyển cây đột ngột từ góc tối ra nắng mạnh; hãy tăng ánh sáng từ từ trong một đến hai tuần để tránh cháy lá.\n\nMàu lá nhạt, mảng cháy khô và nhiệt độ lá cao là dấu hiệu ánh sáng quá mạnh. Khi đó hãy đưa cây lùi khỏi cửa sổ hoặc che nắng vào khung giờ trưa.",coverImage:"https://thumb.wikimedia.org/wikipedia/commons/thumb/3/33/Gummibaum_%28Ficus_elastica_Robusta%29.jpg/960px-Gummibaum_%28Ficus_elastica_Robusta%29.jpg",tags:["Ánh sáng","Vị trí đặt cây","Cơ bản"],views:0,createdAt:at("2026-09-22T04:01:00Z")}],
  ["articles", "GUIDE_REPOTTING_2026", {title:"Khi nào cần thay chậu và cách giảm sốc cho cây",content:"Cây không cần thay chậu chỉ vì đã ở trong chậu một năm. Hãy thay khi rễ mọc kín bầu, chui nhiều qua lỗ thoát nước, đất khô quá nhanh hoặc cây mất cân đối so với chậu.\n\nChọn chậu mới lớn hơn đường kính chậu cũ khoảng 2–5 cm. Chậu quá lớn giữ nhiều nước quanh vùng rễ chưa sử dụng và làm tăng nguy cơ úng. Giá thể mới cần phù hợp với loài cây và luôn có khả năng thoát nước.\n\nTrước khi thay, tưới nhẹ từ hôm trước để bầu không quá khô. Gỡ cây cẩn thận, cắt bỏ rễ đen mềm hoặc có mùi, giữ lại rễ sáng màu và chắc. Đặt cây ở độ cao cũ, lấp giá thể mà không nén quá chặt.\n\nSau khi thay chậu, đặt cây ở nơi sáng dịu, tránh bón phân trong khoảng ba đến bốn tuần. Một vài lá rũ nhẹ là phản ứng bình thường; hãy giữ môi trường ổn định và tránh tưới liên tục vì lo cây bị sốc.",coverImage:"https://thumb.wikimedia.org/wikipedia/commons/thumb/b/b5/Aloe_vera_Lanzarote.jpg/960px-Aloe_vera_Lanzarote.jpg",tags:["Thay chậu","Rễ cây","Giá thể"],views:0,createdAt:at("2026-09-22T04:02:00Z")}],
  ["articles", "GUIDE_PESTS_2026", {title:"Phát hiện sớm rệp sáp và nhện đỏ trên cây",content:"Kiểm tra cây mỗi tuần giúp xử lý sâu hại trước khi chúng lan rộng. Hãy quan sát mặt dưới lá, nách lá và các chồi non—đây là nơi rệp sáp, rệp vảy và nhện đỏ thường ẩn náu.\n\nRệp sáp trông như cụm bông trắng nhỏ. Nhện đỏ rất khó thấy nhưng thường để lại chấm vàng li ti và tơ mảnh. Khi phát hiện, cách ly cây khỏi những chậu khác, cắt bỏ phần bị nặng và rửa kỹ cả hai mặt lá.\n\nVới ổ dịch nhẹ, lau côn trùng bằng khăn ẩm và lặp lại kiểm tra sau ba đến năm ngày. Có thể dùng xà phòng diệt côn trùng hoặc sản phẩm chuyên dụng đúng hướng dẫn, thử trước trên một vùng lá nhỏ.\n\nGiữ tán cây thông thoáng, lau bụi định kỳ và tránh bón thừa đạm. Tiếp tục theo dõi ít nhất ba tuần vì trứng có thể nở sau lần xử lý đầu tiên.",coverImage:"https://thumb.wikimedia.org/wikipedia/commons/thumb/e/e2/%D8%A8%D8%B1%DA%AF_%D9%88_%D8%B3%D8%A7%D9%82%D9%87_%DA%AF%DB%8C%D8%A7%D9%87_%D8%B9%D9%86%DA%A9%D8%A8%D9%88%D8%AA%DB%8C_04-Chlorophytum_comosum%2C.jpg/960px-%D8%A8%D8%B1%DA%AF_%D9%88_%D8%B3%D8%A7%D9%82%D9%87_%DA%AF%DB%8C%D8%A7%D9%87_%D8%B9%D9%86%DA%A9%D8%A8%D9%88%D8%AA%DB%8C_04-Chlorophytum_comosum%2C.jpg",tags:["Sâu bệnh","Rệp sáp","Nhện đỏ"],views:0,createdAt:at("2026-09-22T04:03:00Z")}],
];

const defaultImage = "https://i.ibb.co/MkMj38WM/plant.png";
const imageUpdates = [
  ["3tH705XSq7pl7YjSL5bX", "https://thumb.wikimedia.org/wikipedia/commons/thumb/a/ad/Epipremnum_aureum_kz01.jpg/960px-Epipremnum_aureum_kz01.jpg"],
  ["8xUYQAeubZiLInNu4seC", "https://thumb.wikimedia.org/wikipedia/commons/thumb/b/b5/Aloe_vera_Lanzarote.jpg/960px-Aloe_vera_Lanzarote.jpg"],
  ["DYkOb3q54mjsJX1rS2ie", "https://thumb.wikimedia.org/wikipedia/commons/thumb/a/a9/Asparagales_-_Sansevieria_trifasciata_4.jpg/960px-Asparagales_-_Sansevieria_trifasciata_4.jpg"],
  ["LSbmzQf9e93wa4gjJvD0", "https://thumb.wikimedia.org/wikipedia/commons/thumb/b/b9/Zamioculcas_zamiifolia_Chameleon_1.jpg/960px-Zamioculcas_zamiifolia_Chameleon_1.jpg"],
  ["YRuFlTdtxTec2CQdwvO8", "https://thumb.wikimedia.org/wikipedia/commons/thumb/d/d7/Chamaedorea_elegans07ra.jpg/960px-Chamaedorea_elegans07ra.jpg"],
  ["a4G13c9Z8RIZU47nTUz6", "https://thumb.wikimedia.org/wikipedia/commons/thumb/e/e2/%D8%A8%D8%B1%DA%AF_%D9%88_%D8%B3%D8%A7%D9%82%D9%87_%DA%AF%DB%8C%D8%A7%D9%87_%D8%B9%D9%86%DA%A9%D8%A8%D9%88%D8%AA%DB%8C_04-Chlorophytum_comosum%2C.jpg/960px-%D8%A8%D8%B1%DA%AF_%D9%88_%D8%B3%D8%A7%D9%82%D9%87_%DA%AF%DB%8C%D8%A7%D9%87_%D8%B9%D9%86%DA%A9%D8%A8%D9%88%D8%AA%DB%8C_04-Chlorophytum_comosum%2C.jpg"],
  ["jtBjBcZAA2Z3wDtxRvOW", "https://thumb.wikimedia.org/wikipedia/commons/thumb/a/a1/Peace_lily_-_2.jpg/960px-Peace_lily_-_2.jpg"],
  ["tMGgJEBUczYMd3foJ2sk", "https://thumb.wikimedia.org/wikipedia/commons/thumb/d/d1/Ficus_lyrata_393081802.jpg/960px-Ficus_lyrata_393081802.jpg"],
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
    for (const [id,imageUrl] of imageUpdates) {
      const ref=db.collection("sample_plants").doc(id); const snapshot=await ref.get();
      if (!snapshot.exists) { result.push({collection:"sample_plants",id,action:"missing"}); continue; }
      if (snapshot.data().imageUrl !== defaultImage) { result.push({collection:"sample_plants",id,action:"preserved"}); continue; }
      if (apply) await ref.update({imageUrl});
      result.push({collection:"sample_plants",id,action:apply?"image-updated":"image-pending"});
    }
    console.log(JSON.stringify({project:TARGET_PROJECT,mode:apply?"apply":"dry-run",documents:result},null,2));
  } finally { await deleteApp(app); }
}

module.exports={documents,imageUpdates,main};
if(require.main===module)main(process.argv.slice(2)).catch(error=>{console.error(error.message);process.exitCode=1;});
