(() => {
    const providerLimit = 32 * 1024 * 1024;
    const compressionThreshold = 1024 * 1024;
    const targetSize = 8 * 1024 * 1024;

    async function compressLargeImage(input) {
        const file = input.files && input.files[0];
        if (!file || file.size <= compressionThreshold || file.type === "image/gif") return;

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
            const transfer = new DataTransfer();
            transfer.items.add(compressed);
            input.files = transfer.files;
        } catch {
            if (file.size > providerLimit) {
                input.setCustomValidity("Không thể thu nhỏ ảnh trên 32 MB này. Hãy đổi ảnh sang JPG, PNG hoặc WebP rồi thử lại.");
                input.reportValidity();
            }
        } finally {
            submitButtons.forEach(button => button.disabled = false);
        }
    }

    document.querySelectorAll('input[type="file"][accept*="image"]')
        .forEach(input => input.addEventListener("change", () => compressLargeImage(input)));
})();
