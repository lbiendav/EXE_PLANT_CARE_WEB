/**
 * HomePlant Feedback Module
 * Handles live preview, interactive star rating, AJAX submission and category filtering.
 */
(() => {
    'use strict';

    const starRatingLabels = {
        '5': '5 sao — Rất hài lòng, tuyệt vời!',
        '4': '4 sao — Hài lòng & hữu ích',
        '3': '3 sao — Bình thường / Ổn',
        '2': '2 sao — Cần cải thiện',
        '1': '1 sao — Chưa hài lòng'
    };

    const tagConfig = {
        'CỨU CÂY THÀNH CÔNG': { icon: '🌿', type: 'success' },
        'NGƯỜI MỚI BẮT ĐẦU': { icon: '🌱', type: 'info' },
        'HỘI VIÊN GOLD': { icon: '✨', type: 'gold' },
        'VƯỜN TRONG NHÀ': { icon: '🪴', type: 'copper' },
        'MẸO CHĂM SÓC': { icon: '💡', type: 'info' }
    };

    document.addEventListener('DOMContentLoaded', () => {
        initStarRating();
        initLivePreview();
        initFeedbackForm();
        initCategoryFilter();
    });

    // ── 1. Star Rating Picker ──
    function initStarRating() {
        const starInputs = document.querySelectorAll('input[name="modalRating"]');
        const starText = document.getElementById('starRatingDescription');

        starInputs.forEach(input => {
            input.addEventListener('change', () => {
                if (starText && starRatingLabels[input.value]) {
                    starText.textContent = starRatingLabels[input.value];
                }
                updateLivePreview();
            });
        });
    }

    // ── 2. Live Preview in Modal ──
    function initLivePreview() {
        const inputs = [
            'modalUserName',
            'modalUserSubtitle',
            'modalTitle',
            'modalContent'
        ];

        inputs.forEach(id => {
            const el = document.getElementById(id);
            if (el) {
                el.addEventListener('input', updateLivePreview);
            }
        });

        const tagInputs = document.querySelectorAll('input[name="modalTag"]');
        tagInputs.forEach(radio => {
            radio.addEventListener('change', updateLivePreview);
        });
    }

    function updateLivePreview() {
        const nameVal = document.getElementById('modalUserName')?.value.trim() || 'Người bạn yêu cây';
        const subVal = document.getElementById('modalUserSubtitle')?.value.trim() || 'Hôm nay · Thành viên HomePlant';
        const titleVal = document.getElementById('modalTitle')?.value.trim() || 'Tiêu đề chia sẻ của bạn';
        const contentVal = document.getElementById('modalContent')?.value.trim() || 'Cảm nhận thực tế của bạn khi đồng hành chăm cây cùng HomePlant sẽ xuất hiện tại đây...';

        const checkedRating = document.querySelector('input[name="modalRating"]:checked')?.value || '5';
        const checkedTagRadio = document.querySelector('input[name="modalTag"]:checked');
        const tagVal = checkedTagRadio ? checkedTagRadio.value : 'CỨU CÂY THÀNH CÔNG';
        const config = tagConfig[tagVal] || { icon: '🌿', type: 'success' };

        // Update preview card elements
        const previewName = document.getElementById('prevName');
        const previewSub = document.getElementById('prevSubtitle');
        const previewInitial = document.getElementById('prevInitial');
        const previewTitle = document.getElementById('prevTitle');
        const previewQuote = document.getElementById('prevQuote');
        const previewTag = document.getElementById('prevTag');
        const previewStars = document.getElementById('prevStars');

        if (previewName) previewName.textContent = nameVal;
        if (previewSub) previewSub.textContent = subVal;
        if (previewInitial) previewInitial.textContent = (nameVal[0] || 'N').toUpperCase();
        if (previewTitle) previewTitle.textContent = titleVal;
        if (previewQuote) previewQuote.textContent = `"${contentVal}"`;

        if (previewTag) {
            previewTag.className = `feedback-tag feedback-tag--${config.type}`;
            previewTag.innerHTML = `<span>${config.icon}</span> <span>${tagVal}</span>`;
        }

        if (previewStars) {
            const ratingNum = parseInt(checkedRating, 10) || 5;
            let starsHtml = '';
            for (let i = 1; i <= 5; i++) {
                if (i <= ratingNum) {
                    starsHtml += '<span class="star-gold">★</span>';
                } else {
                    starsHtml += '<span class="star-dim">★</span>';
                }
            }
            previewStars.innerHTML = starsHtml;
        }
    }

    // ── 3. Category Filtering ──
    function initCategoryFilter() {
        const filterBtns = document.querySelectorAll('.feedback-filter-btn');
        const cards = document.querySelectorAll('.feedback-card-wrapper');

        filterBtns.forEach(btn => {
            btn.addEventListener('click', () => {
                filterBtns.forEach(b => b.classList.remove('active'));
                btn.classList.add('active');

                const filter = btn.getAttribute('data-filter');

                cards.forEach(card => {
                    if (filter === 'all') {
                        card.style.display = '';
                    } else {
                        const cardTag = card.getAttribute('data-tag') || '';
                        if (cardTag.toLowerCase().includes(filter.toLowerCase())) {
                            card.style.display = '';
                        } else {
                            card.style.display = 'none';
                        }
                    }
                });
            });
        });
    }

    // ── 4. Form Submission via AJAX ──
    function initFeedbackForm() {
        const form = document.getElementById('feedbackSubmitForm');
        if (!form) return;

        form.addEventListener('submit', async (e) => {
            e.preventDefault();

            const submitBtn = document.getElementById('feedbackSubmitBtn');
            const submitError = document.getElementById('feedbackSubmitError');
            if (submitError) submitError.classList.add('d-none');

            const userName = document.getElementById('modalUserName')?.value.trim();
            const userSubtitle = document.getElementById('modalUserSubtitle')?.value.trim();
            const checkedTagRadio = document.querySelector('input[name="modalTag"]:checked');
            const tag = checkedTagRadio ? checkedTagRadio.value : 'CỨU CÂY THÀNH CÔNG';
            const rating = parseInt(document.querySelector('input[name="modalRating"]:checked')?.value || '5', 10);
            const title = document.getElementById('modalTitle')?.value.trim();
            const content = document.getElementById('modalContent')?.value.trim();

            if (!userName || userName.length < 2) {
                showError('Vui lòng nhập tên của bạn (tối thiểu 2 ký tự).');
                return;
            }

            if (!title || title.length < 4) {
                showError('Vui lòng nhập tiêu đề cảm nhận (tối thiểu 4 ký tự).');
                return;
            }

            if (!content || content.length < 15) {
                showError('Vui lòng chia sẻ cảm nhận chi tiết hơn (tối thiểu 15 ký tự).');
                return;
            }

            // Button loading state
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Đang gửi...';
            }

            const payload = {
                userName,
                userSubtitle,
                tag,
                rating,
                title,
                content
            };

            try {
                const response = await fetch('/api/feedback', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json'
                    },
                    body: JSON.stringify(payload)
                });

                const result = await response.json();

                if (!response.ok || !result.success) {
                    throw new Error(result.message || 'Không thể gửi cảm nhận lúc này. Vui lòng thử lại sau.');
                }

                // Close modal
                const modalEl = document.getElementById('feedbackModal');
                if (modalEl && window.bootstrap) {
                    const modalInstance = bootstrap.Modal.getInstance(modalEl);
                    if (modalInstance) {
                        modalInstance.hide();
                    }
                }

                // Reset form
                form.reset();
                document.getElementById('modalRating5').checked = true;
                document.getElementById('starRatingDescription').textContent = starRatingLabels['5'];
                updateLivePreview();

                // Show success toast indicating pending admin approval
                showToast(result.message || 'Cảm ơn bạn đã gửi cảm nhận! Đánh giá của bạn sẽ được hiển thị trên HomePlant sau khi ban quản trị duyệt 🌿');

            } catch (err) {
                showError(err.message || 'Có lỗi xảy ra khi gửi. Vui lòng thử lại.');
            } finally {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = 'Gửi chia sẻ ngay 🌿';
                }
            }
        });
    }

    function showError(msg) {
        const errorEl = document.getElementById('feedbackSubmitError');
        if (errorEl) {
            errorEl.textContent = msg;
            errorEl.classList.remove('d-none');
        } else {
            alert(msg);
        }
    }

    function showToast(message) {
        let toast = document.getElementById('feedbackToast');
        if (!toast) {
            toast = document.createElement('div');
            toast.id = 'feedbackToast';
            toast.className = 'feedback-toast';
            document.body.appendChild(toast);
        }

        toast.innerHTML = `
            <span style="font-size: 20px;">✨</span>
            <div>
                <strong style="color: #d0ea90; display: block; font-size: 14px;">Thành công!</strong>
                <span style="font-size: 13px;">${escapeHtml(message)}</span>
            </div>
        `;

        toast.classList.add('show');
        setTimeout(() => {
            toast.classList.remove('show');
        }, 4500);
    }

    function prependFeedbackCard(data) {
        const grid = document.getElementById('feedbackCardsGrid');
        if (!grid) return;

        const wrapper = document.createElement('div');
        wrapper.className = 'col-lg-4 col-md-6 col-12 feedback-card-wrapper';
        wrapper.setAttribute('data-tag', data.tag || '');

        const initial = (data.userName && data.userName.length > 0) ? data.userName[0].toUpperCase() : 'N';
        const rating = data.rating || 5;
        let starsHtml = '';
        for (let i = 1; i <= 5; i++) {
            if (i <= rating) {
                starsHtml += '<span>★</span>';
            } else {
                starsHtml += '<span class="star-dim">★</span>';
            }
        }

        const tagType = data.tagType || 'success';
        const tagIcon = data.tagIcon || '🌿';

        wrapper.innerHTML = `
            <article class="feedback-card card-just-added">
                <span class="feedback-quote-watermark" aria-hidden="true">”</span>
                <div class="feedback-card-top">
                    <span class="feedback-tag feedback-tag--${tagType}">
                        <span>${tagIcon}</span>
                        <span>${escapeHtml(data.tag)}</span>
                    </span>
                    <div class="feedback-stars" aria-label="${rating} trên 5 sao">
                        ${starsHtml}
                    </div>
                </div>
                <h3 class="feedback-title">${escapeHtml(data.title)}</h3>
                <blockquote class="feedback-quote">
                    "${escapeHtml(data.content)}"
                </blockquote>
                <div class="feedback-author">
                    <div class="feedback-avatar" aria-hidden="true">${initial}</div>
                    <div class="feedback-author-meta">
                        <span class="feedback-author-name">${escapeHtml(data.userName)}</span>
                        <span class="feedback-author-sub">${escapeHtml(data.userSubtitle || 'Hôm nay · Thành viên')}</span>
                    </div>
                </div>
            </article>
        `;

        grid.insertBefore(wrapper, grid.firstChild);
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }
})();
