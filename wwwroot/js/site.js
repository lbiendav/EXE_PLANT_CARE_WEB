(() => {
    const providerLimit = 32 * 1024 * 1024;
    const compressionThreshold = 1024 * 1024;
    const targetSize = 8 * 1024 * 1024;

    async function compressLargeImage(input) {
        const file = input.files && input.files[0];
        if (!file || file.size <= compressionThreshold || file.type === "image/gif") return;

        const feedback = document.getElementById("photoPlaceholderText");
        if (feedback)
            feedback.textContent = "Đang tối ưu ảnh trước khi tải lên...";

        const submitButtons = input.form
            ? Array.from(input.form.querySelectorAll('button[type="submit"], input[type="submit"]'))
            : [];
        submitButtons.forEach(button => button.disabled = true);
        input.setCustomValidity("");

        try {
            const bitmap = await createImageBitmap(file);
            const scale = Math.min(1, 2048 / Math.max(bitmap.width, bitmap.height));
            const canvas = document.createElement("canvas");
            canvas.width = Math.max(1, Math.round(bitmap.width * scale));
            canvas.height = Math.max(1, Math.round(bitmap.height * scale));

            const context = canvas.getContext("2d");
            context.fillStyle = "#fff";
            context.fillRect(0, 0, canvas.width, canvas.height);
            context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
            bitmap.close();

            const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", 0.82));
            if (!blob || blob.size > targetSize)
                throw new Error("Image is still too large after compression.");

            const baseName = file.name.replace(/\.[^.]+$/, "") || "plant";
            const compressed = new File([blob], `${baseName}.jpg`, {
                type: "image/jpeg",
                lastModified: Date.now()
            });

            // Ignore an older compression job if the user selected another file.
            if (!input.files || input.files[0] !== file) return;

            const transfer = new DataTransfer();
            transfer.items.add(compressed);
            input.files = transfer.files;

            if (feedback) {
                const originalSize = (file.size / 1024 / 1024).toFixed(1);
                const compressedSize = (compressed.size / 1024 / 1024).toFixed(1);
                feedback.textContent = `Đã tối ưu ảnh: ${originalSize} MB → ${compressedSize} MB`;
            }
        } catch {
            if (file.size > providerLimit) {
                input.setCustomValidity("Không thể thu nhỏ ảnh trên 32 MB này. Hãy đổi ảnh sang JPG, PNG hoặc WebP rồi thử lại.");
                input.reportValidity();
            } else if (feedback && input.files && input.files[0] === file) {
                feedback.textContent = `Sẽ tải ảnh gốc: ${file.name}`;
            }
        } finally {
            submitButtons.forEach(button => button.disabled = false);
        }
    }

    document.querySelectorAll('input[type="file"][accept*="image"]')
        .forEach(input => {
            input.addEventListener("change", () => {
                const preparation = compressLargeImage(input);
                input.imagePreparation = preparation;
                preparation.finally(() => {
                    if (input.imagePreparation === preparation)
                        input.imagePreparation = null;
                });
            });

            if (!input.form) return;
            input.form.addEventListener("submit", async event => {
                const preparation = input.imagePreparation;
                if (!preparation) return;

                event.preventDefault();
                event.stopImmediatePropagation();
                await preparation;

                if (input.form.checkValidity())
                    input.form.requestSubmit();
            }, true);
        });
})();

(() => {
    const link = document.querySelector("[data-notification-endpoint]");
    if (!link) return;

    const badge = link.querySelector(".notification-count");
    const endpoint = link.dataset.notificationEndpoint;

    async function refreshNotifications() {
        try {
            const response = await fetch(endpoint, {
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });
            if (!response.ok) return;

            const result = await response.json();
            const count = Number(result.count) || 0;
            badge.textContent = count > 99 ? "99+" : String(count);
            badge.classList.toggle("d-none", count === 0);
            badge.setAttribute("aria-label", `${count} thông báo chưa đọc`);

            if (result.latest && "Notification" in window && Notification.permission === "granted") {
                const storageKey = "homeplant-last-browser-notification";
                if (sessionStorage.getItem(storageKey) !== result.latest.id) {
                    new Notification(result.latest.title, {
                        body: result.latest.message,
                        icon: "/favicon.ico",
                        tag: result.latest.id
                    });
                    sessionStorage.setItem(storageKey, result.latest.id);
                }
            }
        } catch {
            // A reminder polling failure must not affect the rest of the page.
        }
    }

    refreshNotifications();
    window.setInterval(refreshNotifications, 60_000);
})();
