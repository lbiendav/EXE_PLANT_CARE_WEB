const { initializeApp, cert } =
    require("firebase-admin/app");

const { getFirestore } =
    require("firebase-admin/firestore");

const path = require("path");
const fs = require("fs");

const keyPath = path.join(
    __dirname,
    "firebase-key.json"
);

const serviceAccount = JSON.parse(
    fs.readFileSync(
        keyPath,
        "utf8"
    )
);

console.log(
    "Firebase Project:",
    serviceAccount.project_id
);

initializeApp({
    credential: cert(serviceAccount)
});

const db = getFirestore();



// BỘ DỮ LIỆU 8 CÂY MẪU CHUẨN CHỈ (FULL TRƯỜNG)
const samplePlants = [
    {
        name: "Kim Tiền",
        scientificName: "Zamioculcas zamiifolia",
        image: "https://images.unsplash.com/photo-1597055181300-e3623ddfac99?auto=format&fit=crop&q=80&w=500",
        description: "Loài cây tượng trưng cho sự mạnh mẽ, kiên cường và mang lại tài lộc phong thủy. Cực kỳ dễ chăm sóc, phù hợp cho người bận rộn.",
        care: {
            light: "Ánh sáng tán xạ, chịu được bóng râm râm mát. Tránh nắng gắt trực tiếp.",
            water: "Tưới 1-2 tuần/lần. Để đất khô hoàn toàn trước khi tưới đợt tiếp theo.",
            soil: "Đất tơi xốp, thoát nước tốt (trộn nhiều mùn dừa, đá perlite).",
            fertilizer: "Bón phân NPK loãng 1 tháng/lần vào mùa xuân và mùa hè."
        },
        diseases: [
            {
                issue: "Vàng lá, rụng lá liên tục",
                cause: "Tưới quá nhiều nước gây úng rễ.",
                treatment: "Ngừng tưới ngay lập tức. Đem chậu ra nơi thoáng gió. Nếu nặng, phải nhổ cây lên cắt bỏ rễ thối và thay đất mới."
            },
            {
                issue: "Thân héo rũ, nhăn nheo",
                cause: "Thiếu nước trầm trọng trong thời gian dài.",
                treatment: "Tưới đẫm nước một lần cho nước chảy ra đáy chậu, sau đó quay lại nhịp tưới bình thường."
            }
        ]
    },
    {
        name: "Lưỡi Hổ",
        scientificName: "Sansevieria trifasciata",
        image: "https://images.unsplash.com/photo-1599009587841-f2cbdf298c9d?auto=format&fit=crop&q=80&w=500",
        description: "Chuyên gia lọc không khí ban đêm, hấp thụ formaldehyde và nhả oxy. Rất thích hợp để trong phòng ngủ hoặc bàn làm việc.",
        care: {
            light: "Sống tốt ở mọi điều kiện từ bóng râm đến nắng chói.",
            water: "Tưới 2-3 tuần/lần. Cây chịu hạn cực tốt, sợ úng hơn sợ khát.",
            soil: "Đất trồng xương rồng, sen đá hoặc đất pha cát thoát nước nhanh.",
            fertilizer: "Hầu như không cần bón phân, hoặc 6 tháng bón 1 lần."
        },
        diseases: [
            {
                issue: "Lá nhũn mềm ở gốc và đổ gục",
                cause: "Thối nhũn do tưới nước quá nhiều hoặc nấm đất.",
                treatment: "Cắt bỏ ngang phần lá bị nhũn. Bôi thuốc liền sẹo vào vết cắt. Thay đất khô hoàn toàn."
            }
        ]
    },
    {
        name: "Bàng Singapore",
        scientificName: "Ficus lyrata",
        image: "https://images.unsplash.com/photo-1610842857417-66a9d702316e?auto=format&fit=crop&q=80&w=500",
        description: "Cây nội thất mang phong cách hiện đại, thanh lịch với những chiếc lá to bản, xanh bóng mang vẻ đẹp nhiệt đới.",
        care: {
            light: "Cần nhiều ánh sáng gián tiếp (cạnh cửa sổ). Thiếu sáng lá sẽ rụng.",
            water: "Tưới 1-2 lần/tuần khi thấy bề mặt đất (khoảng 3cm) đã khô.",
            soil: "Đất giàu dinh dưỡng, giữ ẩm vừa phải nhưng không đọng nước.",
            fertilizer: "Bón phân hữu cơ hoặc phân tan chậm 2 tháng/lần."
        },
        diseases: [
            {
                issue: "Xuất hiện rệp sáp trắng ở nách lá",
                cause: "Môi trường thiếu ẩm, rệp sáp tấn công hút nhựa.",
                treatment: "Dùng cồn 70 độ thấm vào bông gòn lau sạch rệp. Xịt nước rửa chén pha loãng đẫm 2 mặt lá liên tục 3 ngày."
            }
        ]
    },
    {
        name: "Trầu Bà Xanh",
        scientificName: "Epipremnum aureum",
        image: "https://images.unsplash.com/photo-1615456247385-e36f9a0c78a0?auto=format&fit=crop&q=80&w=500",
        description: "Dòng cây dây leo quốc dân, sức sống mãnh liệt. Có thể trồng chậu đất hoặc trồng thủy sinh đều đẹp.",
        care: {
            light: "Ánh sáng bóng râm hoặc đèn huỳnh quang văn phòng.",
            water: "Trồng đất: 2-3 ngày tưới 1 lần. Thủy sinh: Châm nước 1 tuần/lần.",
            soil: "Không kén đất, dễ sống mọi môi trường.",
            fertilizer: "Nhỏ vài giọt dung dịch thủy sinh hoặc bón phân bón lá 1 tháng/lần."
        },
        diseases: [
            {
                issue: "Lá nhạt màu, mất vân xanh/vàng",
                cause: "Thiếu ánh sáng trầm trọng.",
                treatment: "Mang cây ra vị trí có ánh sáng hắt nhẹ (ban công, cửa sổ) vài tiếng mỗi ngày để lấy lại màu."
            }
        ]
    },
    {
        name: "Lan Ý",
        scientificName: "Spathiphyllum",
        image: "https://images.unsplash.com/photo-1601004890684-d8cbf643f5f2?auto=format&fit=crop&q=80&w=500",
        description: "Hoa màu trắng thanh khiết, tượng trưng cho sự bình yên. Có khả năng lọc các độc tố như benzen và trichloroethylene.",
        care: {
            light: "Tránh nắng trực tiếp. Thích hợp ánh sáng gián tiếp hoặc đèn văn phòng.",
            water: "Thích ẩm. Tưới khi bề mặt đất vừa khô (khoảng 2-3 lần/tuần). Lá sẽ rủ xuống báo hiệu cần nước.",
            soil: "Đất tơi xốp, giữ ẩm tốt nhưng phải thoát nước.",
            fertilizer: "Bón phân dạng lỏng pha loãng mỗi 6 tuần vào mùa phát triển."
        },
        diseases: [
            {
                issue: "Đầu lá bị cháy nâu",
                cause: "Nước tưới có nhiều clo hoặc không khí quá khô.",
                treatment: "Sử dụng nước lọc hoặc để nước máy qua đêm trước khi tưới. Phun sương lên lá để tăng độ ẩm."
            },
            {
                issue: "Hoa chuyển sang màu xanh",
                cause: "Cây già đi hoặc nhận quá nhiều ánh sáng.",
                treatment: "Đây là hiện tượng sinh lý bình thường. Nếu do ánh sáng, dời cây vào nơi râm mát hơn."
            }
        ]
    },
    {
        name: "Nha Đam",
        scientificName: "Aloe vera",
        image: "https://images.unsplash.com/photo-1596547609652-9cb5d8d737b8?auto=format&fit=crop&q=80&w=500",
        description: "Cây thảo dược đa năng, vừa làm cảnh vừa có thể dùng để làm dịu vết bỏng hoặc chăm sóc da.",
        care: {
            light: "Thích nắng ấm. Để cạnh cửa sổ có ánh nắng hắt vào là tốt nhất.",
            water: "Tưới thưa, khoảng 2-3 tuần/lần. Để đất khô cong rồi mới tưới.",
            soil: "Đất trộn nhiều cát, sỏi đá để thoát nước tuyệt đối.",
            fertilizer: "Mỗi năm chỉ cần bón 1 lần vào mùa xuân."
        },
        diseases: [
            {
                issue: "Lá mềm nhũn, đổi màu xỉn",
                cause: "Chậu đọng nước gây thối rễ.",
                treatment: "Cắt bỏ các lá thối, lấy cây ra phơi gốc 1 ngày rồi trồng lại vào đất cát khô mới."
            }
        ]
    },
    {
        name: "Cây Nhện (Mẫu Tử)",
        scientificName: "Chlorophytum comosum",
        image: "https://images.unsplash.com/photo-1620127682229-33388a1fa236?auto=format&fit=crop&q=80&w=500",
        description: "Cây thả rủ tuyệt đẹp, thường mọc ra các chồi non lủng lẳng như mạng nhện. Lọc sạch CO2 rất tốt.",
        care: {
            light: "Ánh sáng gián tiếp cường độ vừa phải. Chịu bóng râm khá tốt.",
            water: "Tưới 1 lần/tuần. Giữ đất hơi ẩm nhưng không nhão.",
            soil: "Đất trồng thông thường trộn thêm xơ dừa.",
            fertilizer: "Bón NPK định kỳ 2 tháng/lần."
        },
        diseases: [
            {
                issue: "Đầu lá bị khô đen",
                cause: "Thiếu ẩm hoặc tích tụ muối từ phân bón/nước máy.",
                treatment: "Tưới đẫm nước để rửa mặn cho đất. Cắt tỉa phần đầu lá đen bằng kéo sạch để đảm bảo thẩm mỹ."
            }
        ]
    },
    {
        name: "Cọ Cảnh",
        scientificName: "Chamaedorea elegans",
        image: "https://images.unsplash.com/photo-1606813296229-47e113dc2558?auto=format&fit=crop&q=80&w=500",
        description: "Mang không gian nhiệt đới thu nhỏ vào phòng khách. Cây phát triển chậm, tán xòe đẹp, thân thiện với thú cưng.",
        care: {
            light: "Ánh sáng yếu đến trung bình. Cấm kỵ nắng trực tiếp.",
            water: "Tưới khi đất khô khoảng 50%. Tầm 1 tuần/lần.",
            soil: "Đất than bùn pha cát.",
            fertilizer: "Phân bón lá sinh học phun 2 tháng/lần."
        },
        diseases: [
            {
                issue: "Lá lốm đốm vàng và có màng nhện mỏng",
                cause: "Bị nhện đỏ tấn công do không khí quá khô nóng.",
                treatment: "Cách ly cây. Dùng vòi sen xịt mạnh vào mặt dưới lá để trôi nhện đỏ. Xịt dung dịch dầu neem (Neem oil) để diệt trứng."
            }
        ]
    }
];

// HÀM CHẠY ĐỂ BƠM DỮ LIỆU
async function seedDatabase() {
    console.log("Starting Firestore seed...");

try {

    const batch = db.batch();

    for (const plant of samplePlants) {

        const docRef = db
            .collection("plantSamples")
            .doc();

        batch.set(docRef, {
            ...plant,
            ccreatedAt: new Date()
        });

        console.log(
            "Preparing: " + plant.name
        );
    }

    await batch.commit();

    console.log(
        "DONE! Added " +
        samplePlants.length +
        " plants to Firestore."
    );

}
catch (error) {

    console.error(
        "ERROR WHILE SEEDING:"
    );

    console.error(error);

}
finally {

    process.exit(0);

}

}

seedDatabase();

