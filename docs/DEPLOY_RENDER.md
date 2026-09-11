# HomePlant — nhật ký và hướng dẫn triển khai Render Free

Cập nhật: 11/09/2026. Mục tiêu: bản thử nghiệm online trong quá trình phát triển,
giữ ASP.NET Core MVC + Firestore + Firebase Authentication + ImgBB.

## 1. Trạng thái thực tế

| Công việc | Trạng thái | Chi tiết |
| --- | --- | --- |
| Kiểm tra cấu trúc và khả năng hosting | Hoàn thành | .NET 9; Render chạy bằng Docker, Firebase kết nối bên ngoài |
| Dockerfile và .dockerignore | Hoàn thành | Build nhiều giai đoạn; runtime chạy bằng user không phải root; loại khóa và cấu hình local khỏi Docker context |
| Cấu hình Render | Hoàn thành trên Render | Free, Singapore, nhánh `staging`, On Commit, `/healthz`; bỏ qua thay đổi chỉ trong `docs/**` |
| Nạp credential và cấu hình cổng | Hoàn thành ở local | Nhận `PORT`; hỗ trợ secret file/ADC; giữ khóa local khi Development |
| URL trong email Firebase | Hoàn thành ở local | Ưu tiên `App__PublicBaseUrl`, tiếp theo `RENDER_EXTERNAL_URL`; local dùng request |
| Đóng gói giao diện 3D | Hoàn thành ở local | Publish HTML, CSS, JS, font và các frame; bỏ file `.bak` |
| Publish và kiểm tra HTTP | Hoàn thành | Publish thành công; health, landing, login, assets trả 200; HTTP chuyển HTTPS 307 |
| Kiểm tra Docker image | Hoàn thành | Linux amd64; sửa quyền đọc index.html; chạy bằng UID 1654, không nhúng secret |
| Đẩy code lên GitHub | Hoàn thành | Lần deploy đầu dùng `1d5b715` trên `origin/staging`; main không bị push thay đổi |
| Tạo dịch vụ Render | Hoàn thành | `homeplant-staging`, Docker, Singapore, Free $0/tháng; lần build đầu thành công trong 51,3 giây |
| Kết nối GitHub tự động | Hoàn thành | Kết nối repository `EXE_PLANT_CARE_WEB`, Auto-Deploy On Commit |
| Nạp khóa vào Render | Hoàn thành | Secret file và Firebase API key đã đổi sang project staging riêng; ImgBB giữ API key hiện có |
| Firebase riêng | Hoàn thành | `homeplant-staging-dav`; Firestore Standard tại Singapore, Free tier, billing chưa bật; Email/Password đã bật |
| Domain và kiểm thử online | Đang kiểm tra sau chuyển Firebase | `https://homeplant-staging.onrender.com`; đã thêm hostname vào Authorized domains của project mới |
| Kiểm tra Firebase thật từ máy | Hoàn thành, chỉ đọc | Đọc tối đa 1 article và cấu hình Firebase Auth thành công; chưa ghi/xóa hoặc gửi email |

Không có khóa bí mật, mật khẩu, nội dung service account hoặc dữ liệu người dùng
trong tài liệu này. Các mục “hoàn thành ở local” chưa đồng nghĩa đã chạy trên Render.

Website: https://homeplant-staging.onrender.com

Render Dashboard: https://dashboard.render.com/web/srv-dahmb71594qs73fkkd50

Firebase staging: https://console.firebase.google.com/project/homeplant-staging-dav/overview

Lần deploy đầu đã dùng credential Firebase hiện tại được người dùng cho phép.
Sau khi nhận lựa chọn tạo Firebase riêng, đã tạm suspend dịch vụ, tạo project mới,
thay Project ID/API key/secret file, xác minh secret đúng project rồi resume và
deploy lại. Chưa sao chép dữ liệu, tài khoản hoặc gửi email thử. Firebase cũ
`home-plant-app-dav` và cấu hình local được giữ nguyên.

## 2. Các thay đổi trong code và mục đích

- `Dockerfile`: dùng .NET SDK 9 để restore/publish, .NET ASP.NET 9 để chạy.
  Build container không cần kết nối Firebase hay khóa Firebase. Chuẩn hóa quyền
  đọc output publish để user runtime đọc được cả file local có mode `600`.
- `.dockerignore`: không gửi `Firebase/`, `Seed/`, `appsettings*.json`, `.env`,
  khóa riêng, output build và cấu hình công cụ lên quá trình Docker build.
  `global.json` chỉ dùng cho SDK trên máy, không đưa vào container; Docker dùng
  SDK của image `sdk:9.0`.
- `HomePlant.csproj`: đưa `3D_UI/index.html`, `assets/`, `render_output/` vào
  publish; không publish khóa Firebase, Seed hay appsettings local.
- `Program.cs`: lắng nghe `0.0.0.0:$PORT` khi hosting cung cấp `PORT`; local giữ
  launch profile. Thêm `/healthz`, session cookie HttpOnly và Secure ngoài Development.
- `Services/FirebaseAuthService.cs`: tạo URL email từ domain cấu hình thay vì phụ
  thuộc hoàn toàn vào request do proxy chuyển tiếp; encode UID trong URL.
- `appsettings.Example.json`: bổ sung tên cấu hình tùy chọn, không chứa khóa thật.
- `render.yaml`: mô tả một web service Free, không tạo database trả phí hoặc disk.
- `firebase.staging.json` và `firestore.staging.rules`: cấu hình riêng để bật
  Email/Password và triển khai rules chặn client trực tiếp trên staging; không sửa
  `firebase.json`/`firestore.rules` đang dùng cho project cũ.

Thứ tự nạp credential:

1. `Firebase__CredentialPath` nếu có: đường dẫn tới file JSON service account.
2. `FIREBASE_KEY` nếu có: JSON service account, tương thích cấu hình vừa có trên GitHub.
3. Khi Development và chưa đặt `GOOGLE_APPLICATION_CREDENTIALS`: dùng
   `Firebase/firebase-key.json` nếu file tồn tại.
4. Các trường hợp còn lại: Application Default Credentials (ADC). Trên Render,
   ADC đọc file được chỉ định bởi `GOOGLE_APPLICATION_CREDENTIALS`.

## 3. Chuẩn bị Firebase

Khuyến nghị dùng Firebase project dành riêng cho staging để thử tính năng và seed
không ảnh hưởng dữ liệu quan trọng. Nếu dùng project hiện tại, bản online và local
sẽ cùng đọc/ghi dữ liệu khi có cùng Project ID và credential.

Đã chọn project riêng `homeplant-staging-dav`, không liên kết tài khoản thanh toán.
Firestore `(default)` là Standard/Native, vị trí `asia-southeast1` (Singapore).
Backend dùng service account `homeplant-render@homeplant-staging-dav.iam.gserviceaccount.com`
với hai role `roles/datastore.user` và `roles/firebaseauth.admin`, không cấp Owner/Editor.
Khóa local riêng là `Firebase/firebase-staging-key.json` (Git ignore, quyền file 600).
Trên Render, tên file vẫn là `firebase-key.json` để giữ đường dẫn runtime ổn định.

Để tự làm lại cho một project mới, tạo project trên Firebase Console, bỏ qua Google
Analytics nếu không cần và không nâng lên Blaze. Bật Cloud Firestore API nếu báo
API disabled; có thể cần chờ vài phút sau khi bật. Tạo database ở Singapore với
rules Production. Tạo web app để lấy Web API key. Trong Google Cloud Console →
IAM & Admin → Service Accounts, tạo service account dành cho backend, cấp hai
role trên, tạo khóa JSON và lưu ngoài Git. Không ghi đè khóa của project local.

Các lệnh đã dùng (chỉ chạy lệnh tạo khi tài nguyên chưa tồn tại):

```sh
firebase projects:create homeplant-staging-dav --display-name 'HomePlant Staging'
firebase apps:create WEB 'HomePlant Staging Web' --project homeplant-staging-dav
firebase firestore:databases:create '(default)' --location asia-southeast1 --project homeplant-staging-dav
firebase deploy --only auth --config firebase.staging.json --project homeplant-staging-dav --non-interactive
firebase deploy --only firestore:rules,firestore:indexes --config firebase.staging.json --project homeplant-staging-dav --non-interactive
```

Không dùng `firebase deploy` thiếu `--project` khi có nhiều project. File rules
staging từ chối toàn bộ truy cập SDK client; backend Admin SDK vẫn truy cập bằng
IAM. Chỉ deploy file này vào project web staging, không vào project mobile cũ.

1. Vào Firebase Console, chọn hoặc tạo project thử nghiệm.
2. Trong Authentication → Sign-in method, bật Email/Password.
3. Tạo Firestore database nếu project chưa có. Chọn vị trí trước khi tạo; vị trí
   database hiện có không thay đổi chỉ bằng cách sửa `firebase.json`.
4. Lấy Project ID và Web API Key của đúng project.
5. Vào Project settings → Service accounts → Generate new private key nếu cần
   credential cho project mới. Lưu file an toàn trên máy, không commit.
6. Khi đã có domain Render, thêm hostname đó tại Authentication → Settings →
   Authorized domains (chỉ hostname, không có `https://` hoặc đường dẫn).
7. Kiểm tra indexes khi chạy các trang có truy vấn sắp xếp/lọc. Nếu cần dùng
   Firebase CLI, luôn chỉ định project rõ ràng:

```sh
firebase deploy --only firestore:indexes --project YOUR_STAGING_PROJECT_ID
```

Lệnh trên chỉ dùng sau khi đăng nhập Firebase CLI và xác nhận đúng project.
Không chạy Seed tự động trong lúc deploy. Admin SDK có quyền theo IAM, nên không
cần mở Firestore Rules cho mọi người để backend hoạt động.

## 4. Kiểm tra trên máy

### Publish bằng .NET

Chạy trong thư mục chứa `HomePlant.csproj`:

```sh
dotnet restore HomePlant.csproj
dotnet publish HomePlant.csproj --configuration Release --no-restore --output ./bin/render-publish /p:UseAppHost=false
```

Output phải có `HomePlant.dll`, `wwwroot/`, `3D_UI/index.html`, `3D_UI/assets/`
và `3D_UI/render_output/`. Không được có `firebase-key.json`, cấu hình local hoặc Seed.

### Kiểm tra Docker sau khi mở Docker Desktop

```sh
docker info
docker build --pull -t homeplant:staging .
```

Tạo file `.env.render.local` trên máy bằng editor với nội dung mẫu bên dưới;
điền giá trị thật trên máy, không đưa vào Git:

```dotenv
Firebase__ProjectId=YOUR_STAGING_PROJECT_ID
Firebase__ApiKey=YOUR_FIREBASE_WEB_API_KEY
ImgBB__ApiKey=YOUR_IMGBB_API_KEY
GOOGLE_APPLICATION_CREDENTIALS=/etc/secrets/firebase-key.json
App__PublicBaseUrl=http://localhost:8080
```

Đổi đường dẫn nguồn trong lệnh sau thành đường dẫn tuyệt đối tới service account
của bạn. `--mount` chỉ gắn file để đọc lúc chạy, không nhúng vào image:

```sh
docker run --rm --name homeplant-staging -p 127.0.0.1:8080:8080 --env-file .env.render.local --mount type=bind,source=/ABSOLUTE/PATH/firebase-key.json,target=/etc/secrets/firebase-key.json,readonly homeplant:staging
```

Mở `http://localhost:8080/healthz`, `/`, `/Account/Login`. Lệnh này kiểm tra
container chạy trong Production; cookie Secure yêu cầu HTTPS để kiểm thử session
đầy đủ. Nếu cần test đăng nhập qua HTTP local, thêm
`-e ASPNETCORE_ENVIRONMENT=Development` vào `docker run`; không dùng Development
trên Render. Dừng container bằng Ctrl+C.

## 5. Đưa code lên nhánh staging

Trước hết kiểm tra `git status` để giữ nguyên thay đổi đang làm của bạn. File
`global.json` đã tồn tại dưới dạng untracked trước công việc triển khai; chưa tự
động thêm file này vào commit triển khai.

Nếu chưa có nhánh staging:

```sh
git switch -c staging
```

Nếu đã có nhánh staging, dùng `git switch staging` sau khi xử lý thay đổi dở dang.
Không dùng lệnh xóa hoặc reset để ép đổi nhánh.

Chỉ thêm các file triển khai đã xem lại:

```sh
git add Dockerfile .dockerignore .gitignore render.yaml Program.cs HomePlant.csproj Services/FirebaseAuthService.cs appsettings.Example.json docs/DEPLOY_RENDER.md
git diff --cached --stat
git diff --cached
git commit -m "Configure Render staging deployment"
git push -u origin staging
```

Repository hiện cấu hình remote:
`https://github.com/lbiendav/EXE_PLANT_CARE_WEB.git`.
GitHub có thể yêu cầu đăng nhập/quyền push. Không đưa service account hoặc mật khẩu
vào commit để xử lý lỗi authentication.

## 6. Tạo Web Service trên Render — cách thủ công khuyến nghị

1. Đăng nhập [Render Dashboard](https://dashboard.render.com).
2. Chọn New → Web Service, kết nối GitHub và chọn repository
   `lbiendav/EXE_PLANT_CARE_WEB`. Nếu cần cấp quyền GitHub, chỉ chọn repository này.
3. Điền cấu hình:

| Trường | Giá trị |
| --- | --- |
| Name | `homeplant-staging` hoặc tên chưa bị dùng |
| Branch | `staging` |
| Language/Runtime | Docker |
| Region | Singapore |
| Root Directory | Để trống |
| Dockerfile Path | `./Dockerfile` |
| Docker Build Context | `.` |
| Docker Command | Để trống; dùng ENTRYPOINT của Dockerfile |
| Instance Type | **Free** |
| Health Check Path | `/healthz` |
| Auto Deploy | On Commit |
| Build Filters → Ignored Paths | `docs/**` |

4. Thêm Environment Variables:

| Key | Value | Ghi chú |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Không dùng Development online |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` | Nhận scheme HTTPS từ proxy của Render |
| `ASPNETCORE_HTTPS_PORT` | `443` | Cổng HTTPS công khai |
| `GOOGLE_APPLICATION_CREDENTIALS` | `/etc/secrets/firebase-key.json` | Đường dẫn secret file ở bước tiếp theo |
| `Firebase__ProjectId` | Project ID Firebase thử nghiệm | Cùng project với service account và API key |
| `Firebase__ApiKey` | Firebase Web API Key | Không phải JSON private key |
| `ImgBB__ApiKey` | API key ImgBB | Cần để upload ảnh |

`PORT` do Render cung cấp; không cần tự điền. Với domain mặc định, không cần đặt
`App__PublicBaseUrl`: ứng dụng tự dùng `RENDER_EXTERNAL_URL` do Render cung cấp.
Nếu dùng custom domain, đặt `App__PublicBaseUrl=https://your-domain.example` và
thêm domain đó trong Firebase Authorized domains.

5. Trong Secret Files, thêm file có tên chính xác `firebase-key.json`. Dán nội
   dung JSON service account vào ô secret của Render, không vào repository,
   Dockerfile hay tài liệu. File lúc chạy nằm ở `/etc/secrets/firebase-key.json`.
   Nếu dịch vụ cũ đã dùng `FIREBASE_KEY` thì vẫn được hỗ trợ; khi chuyển sang secret
   file, bỏ `FIREBASE_KEY` sau khi đã kiểm tra file đúng project để tránh cấu hình trùng.
6. Kiểm tra instance vẫn là **Free**, không thêm disk/database trả phí. Bấm
   Deploy Web Service/Create Web Service.
7. Xem Events/Logs: restore → publish → start → health check thành công → Live.
8. Lấy URL Render thực tế và cập nhật Firebase Authorized domains như mục 3.
9. Làm các kiểm thử ở mục 8. Ghi URL và kết quả thực tế vào mục 1/mục 10.

Lưu ý proxy: chỉ bật `ASPNETCORE_FORWARDEDHEADERS_ENABLED` khi app nằm sau proxy
tin cậy như Render, nơi cổng container không được truy cập công khai trực tiếp.
Nếu tự host máy chủ trực tiếp, cần cấu hình KnownProxies/KnownNetworks riêng.

## 7. Cách dùng Blueprint render.yaml

Đây là cách thay thế mục 6, không thực hiện cả hai để tránh tạo hai service.

1. Push file `render.yaml` lên nhánh `staging`.
2. Render → New → Blueprint → chọn repository và nhánh `staging`.
3. Xem lại danh sách tài nguyên: một web service `homeplant-staging`, plan Free.
4. Điền các giá trị có `sync: false`: Firebase Project ID, Firebase API Key, ImgBB API Key.
5. Sau khi service được tạo, thêm Secret File `firebase-key.json` trong Environment.
   Blueprint không chứa private key. Lần start đầu có thể thất bại cho đến khi thêm file.
6. Save và deploy lại, sau đó hoàn tất Authorized domains và kiểm thử.

### Phương án dự phòng nếu không kết nối được GitHub

Repository hiện là public. Có thể chọn **Public Git Repository**, nhập
`https://github.com/lbiendav/EXE_PLANT_CARE_WEB`, chọn nhánh `staging` và các giá trị
ở mục 6; Auto-Deploy đặt **Off**. Đây chỉ là phương án dự phòng, không phải cấu
hình dịch vụ hiện tại: dịch vụ hiện tại đã kết nối GitHub và dùng **On Commit**.

Mỗi lần cập nhật: push lên staging → Render → Manual Deploy → Deploy latest commit.
Sau khi hoàn tất kết nối GitHub, kiểm tra kết nối repository trong Settings rồi
bật On Commit. `render.yaml` vẫn là mẫu cho phương án có tự động deploy; import
Blueprint không phải thao tác bắt buộc cho Web Service tạo thủ công này.

## 8. Kiểm thử online sau deploy

- [ ] `/healthz` trả HTTP 200 và `{"status":"ok"}`. Đây chỉ là liveness, chưa chứng
  minh Firestore/Auth kết nối thành công.
- [ ] `/` tải được giao diện, CSS/JS/font và các frame 3D, không bị 404.
- [ ] `/Account/Login` và `/Account/Register` mở được; không lặp redirect HTTPS.
- [ ] `/Home/Index` hoặc `/Library/Index` đọc dữ liệu Firestore được.
- [ ] Dùng tài khoản thử nghiệm để đăng ký, nhận email, xác minh và đăng nhập.
- [ ] Link trong email trở về đúng domain Render, không phải localhost/HTTP.
- [ ] Tạo cây/chỉnh sửa dữ liệu bằng tài khoản test, tải lại trang để kiểm tra dữ liệu còn.
- [ ] Upload một ảnh mẫu qua ImgBB.
- [ ] Người chưa đăng nhập/không phải admin không truy cập được trang quản trị.
- [ ] Sau redeploy, dữ liệu Firestore vẫn còn; chấp nhận phải đăng nhập lại vì session RAM.

Không dùng tài khoản/dữ liệu thật cho thao tác thử ghi/xóa. Việc gửi email test cần
người thực hiện chọn địa chỉ email của mình; không gửi tự động tới người khác.

## 9. Cập nhật ứng dụng và xử lý lỗi

Sau mỗi phần tính năng chạy ổn: đưa thay đổi lên `staging`, chạy publish kiểm tra,
push GitHub. Khi đã kết nối GitHub và bật On Commit, Render tự build/deploy commit
mới. Nếu dùng Public Git Repository với Auto-Deploy Off, chọn Manual Deploy →
Deploy latest commit. Không cần tạo lại dịch vụ hay database.
Thay đổi chỉ trong `docs/**` không tự build lại vì đã cấu hình Ignored Paths.
Khi bản mới lỗi, dùng Events/Deploys để rollback bản chạy tốt trước đó. Rollback
code không khôi phục dữ liệu Firestore đã sửa.

| Hiện tượng | Kiểm tra/cách xử lý |
| --- | --- |
| Không có default credentials/file not found | Tên Secret File, `GOOGLE_APPLICATION_CREDENTIALS`, đã Save và redeploy chưa |
| Firebase project ID thiếu | Kiểm tra `Firebase__ProjectId`, phải có hai dấu gạch dưới |
| Invalid API key/permission denied | API key và service account phải thuộc project dự định dùng; kiểm tra IAM/API đã bật |
| Unauthorized continue URI | Thêm hostname Render vào Firebase Authorized domains |
| Redirect HTTPS liên tục | Kiểm tra biến forwarded headers và cổng HTTPS 443 |
| Không tìm thấy cổng | Xem log `Now listening`; app phải bind `0.0.0.0` với PORT Render |
| Thiếu `3D_UI` hoặc asset 404 | Kiểm tra output publish và tên file phân biệt hoa/thường trên Linux |
| Firestore yêu cầu index | Tạo index theo query/lỗi, đúng project staging; chờ build index xong |
| Upload ảnh thất bại | Kiểm tra `ImgBB__ApiKey`, dung lượng ảnh và log HTTP |
| Lần mở đầu chậm khoảng một phút | Render Free ngủ khi không có truy cập trong 15 phút |
| Đăng xuất sau restart/redeploy | Session đang lưu RAM, không phải mất dữ liệu Firebase |

Giới hạn hiện tại: đây là staging; chưa thay cơ chế session, chưa audit toàn bộ bảo
mật/nghiệp vụ. Không dùng dữ liệu nhạy cảm cho bản thử nghiệm. .NET 9 hiện còn được
hỗ trợ đến 10/11/2026; nên lên kế hoạch nâng .NET 10 LTS trước thời điểm đó, không
gộp việc nâng major version vào lần deploy đầu này.

Ghi nhận để xử lý trước production: file `firestore.rules` trong repository đang
có `allow read, write: if true`. Chưa xác nhận đây là rules đang được áp dụng online.
Firebase project còn đăng ký app Android/iOS, nên chưa tự thay rules chung trong
lần deploy web này; cần rà soát quyền cho các client trước khi áp dụng rules mới.

## 10. Nhật ký thao tác thực tế

- 10/09/2026: xác nhận mã nguồn ở `main`, commit gốc `e55c992`; có `global.json`
  untracked từ trước, giữ nguyên.
- 10/09/2026: xác nhận .NET SDK 9.0.315 dùng được. Docker CLI có sẵn nhưng không
  kết nối được daemon; chưa build image Docker.
- 10/09/2026: mở Render Dashboard và gặp màn hình login; chưa tạo service/tài nguyên.
- 10/09/2026: thêm cấu hình container, Render, nạp credential, URL email, publish
  assets và health endpoint; bắt đầu kiểm tra publish.
- 10/09/2026: publish Release thành công; kiểm tra output đủ tài nguyên 3D, không
  có private key hoặc appsettings local. Kiểm tra HTTP Production: `/healthz`, `/`,
  `/Account/Login`, `/assets/js/main.js` trả 200; request HTTP thường redirect 307.
  Dùng project/API key giả cho smoke test; chưa gọi Firestore/Auth thật.
- 10/09/2026: đã khởi động Docker Desktop sau khi được cấp quyền; build image
  `homeplant:staging` cho Linux amd64 thành công. Build còn các warning nullable
  trong code nghiệp vụ hiện có.
- 10/09/2026: fetch GitHub phát hiện `origin/main` mới ở `222da75` với Dockerfile
  và biến `FIREBASE_KEY`; bổ sung tương thích biến này và chuẩn bị đồng bộ lịch sử
  vào nhánh staging trước khi push.
- 10/09/2026: tạo nhánh staging, commit cấu hình `bfb835c`, merge main mới bằng
  commit `4308395`. Giữ nguyên `global.json` ngoài commit.
- 10/09/2026: smoke test Linux phát hiện `3D_UI/index.html` có mode `600` trên máy,
  khiến user thường trong container nhận HTTP 500. Đã bổ sung chuẩn hóa quyền đọc
  trong Docker build. Health/login/frame đều trả 200, URL khóa trả 404 và admin
  chưa đăng nhập redirect về login; đang kiểm tra lại trang chủ sau sửa quyền.
- 10/09/2026: kiểm tra lại image sau sửa quyền: `/healthz`, `/`, `/Account/Login`,
  frame 3D đều HTTP 200; HTTP thường redirect 307. Container chạy UID 1654,
  thư mục ứng dụng không chứa service account, appsettings local hoặc Seed.
- 10/09/2026: push `staging` thành công, xác nhận remote ở `613f7f2`; chỉ có
  `global.json` có sẵn vẫn untracked.
- 10/09/2026: Firebase CLI nhận diện đúng project `home-plant-app-dav`; kiểm tra
  bằng Admin SDK đọc Firestore và cấu hình Auth thành công. Không sửa dữ liệu.
- 10/09/2026: Render đã đăng nhập. Authorize GitHub xong, chọn Only select
  repositories → `EXE_PLANT_CARE_WEB`; bước Install đang chờ mã xác thực GitHub.
- 10/09/2026: vì repository public, chuẩn bị phương án Public Git Repository;
  điền tên `homeplant-staging`, staging, Docker, Singapore, Free, health `/healthz`,
  Dockerfile `./Dockerfile`, build context `.`, Auto-Deploy Off và các biến không
  chứa secret. Chưa upload khóa, chưa bấm Deploy; chờ xác nhận chuyển các khóa
  vào đúng dịch vụ Render này hoặc người dùng tự nhập.
- 11/09/2026: người dùng hoàn tất đăng nhập Render, GitHub xác thực, đồng ý nạp
  khóa và bật quyền Chrome extension “Allow access to file URLs”. Chuyển form về
  Git Provider đã kết nối, nhánh staging, Auto-Deploy On Commit.
- 11/09/2026: nạp biến môi trường và Secret File, tạo dịch vụ
  `srv-dahmb71594qs73fkkd50`. Lần deploy `1d5b715` báo Live sau 51,3 giây;
  URL là `https://homeplant-staging.onrender.com`.
- 11/09/2026: nhận lựa chọn Firebase riêng; suspend dịch vụ trong lúc chuyển đổi.
  Tạo project `homeplant-staging-dav`, web app `HomePlant Staging Web`, bật API
  Firestore/Auth/IAM/Rules, bật Email/Password, thêm domain Render vào Authorized domains.
- 11/09/2026: tạo Firestore Standard Singapore, xác nhận `freeTier: true` và
  `billingEnabled: false`. Deploy rules staging chặn client trực tiếp thành công;
  project cũ không bị thay rules. Chưa seed dữ liệu mới hoặc sao chép dữ liệu cũ.
- 11/09/2026: tạo service account riêng với quyền Firestore User/Firebase Auth Admin,
  lưu khóa vào file Git ignore quyền 600; đổi Project ID/API key/secret file Render.
  Đọc lại secret đã lưu xác nhận project ID mới. Đọc Firestore (0 article) và cấu
  hình Auth bằng khóa mới thành công. Resume và yêu cầu deploy lại.
- 11/09/2026: các file `.env` tạm dùng để nhập khóa lên Render đã được xóa;
  các file khóa gốc/local vẫn giữ nguyên. Không đưa khóa vào tài liệu/Git.

## 11. Tài liệu chính thức đã đối chiếu

- [Render Docker](https://render.com/docs/docker)
- [Render Web Services: port và HTTPS proxy](https://render.com/docs/web-services)
- [Render Free: sleep, quota và filesystem](https://render.com/docs/free)
- [Render Blueprint specification](https://render.com/docs/blueprint-spec)
- [Render Environment Variables và Secret Files](https://render.com/docs/configure-environment-variables)
- [Firebase Admin SDK và credentials](https://firebase.google.com/docs/admin/setup)
- [Firebase Authorized domains cho email actions](https://firebase.google.com/docs/auth/web/passing-state-in-email-actions)
- [ASP.NET Core forwarded headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-9.0)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
