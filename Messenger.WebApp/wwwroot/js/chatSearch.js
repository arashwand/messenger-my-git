// 🔥 نسخه جدید با استفاده از search-offcanvas

(function() {
    'use strict';

    const SEARCH_DELAY = 500;
    const MIN_SEARCH_LENGTH = 2;

    let searchTimeout = null;
    let allChats = [];
    let currentSearchType = 'name'; // 'name' or 'nationalCode'

    // عناصر DOM
    const searchInput = document.getElementById('searchInput');
    const searchResults = document.getElementById('searchResults');
    const searchOptionsContainer = document.getElementById('searchOptionsContainer');
    const chatList = document.getElementById('chatContactTab');
    const searchOffcanvas = document.getElementById('search-offcanvas');

    function getUserRole() {
        return document.getElementById('userRole')?.value || '';
    }

    function isManagerOrPersonel() {
        const role = getUserRole();
        return role === 'Manager' || role === 'Personel';
    }

    function init() {
        if (!searchInput || !searchResults) {
            console.error('❌ Search elements not found!');
            return;
        }

        // نمایش/عدم نمایش گزینههای جستجو بر اساس نقش
        if (isManagerOrPersonel() && searchOptionsContainer) {
            searchOptionsContainer.style.display = 'block';
            
            // رویداد تغییر نوع جستجو
            const radioButtons = document.querySelectorAll('input[name="searchType"]');
            radioButtons.forEach(radio => {
                radio.addEventListener('change', function() {
                    currentSearchType = this.value;
                    updatePlaceholder();
                });
            });
        }

        // رویداد جستجو
        searchInput.addEventListener('input', handleSearchInput);

        // رویداد باز شدن offcanvas
        if (searchOffcanvas) {
            searchOffcanvas.addEventListener('shown.bs.offcanvas', function() {
                searchInput.focus();
                saveChatsForLocalSearch();
            });

            // پاک کردن هنگام بستن offcanvas
            searchOffcanvas.addEventListener('hidden.bs.offcanvas', function() {
                clearSearchUI();
            });
        }

        updatePlaceholder();
        console.log('✅ Search initialized for role:', getUserRole());
    }

    function updatePlaceholder() {
        if (!searchInput) return;

        const role = getUserRole();
        
        if (role === 'Teacher') {
            searchInput.placeholder = 'جستجو در گروههای چت...';
        } else if (isManagerOrPersonel()) {
            if (currentSearchType === 'name') {
                searchInput.placeholder = 'جستجو بر اساس نام...';
            } else {
                searchInput.placeholder = 'جستجو بر اساس کد ملی...';
            }
        } else {
            searchInput.placeholder = 'جستجو...';
        }
    }

    function saveChatsForLocalSearch() {
        allChats = [];
        if (!chatList) return;

        const chatItems = chatList.querySelectorAll('li[id]');
        
        chatItems.forEach(item => {
            const nameEl = item.querySelector('.name');
            
            if (nameEl) {
                allChats.push({
                    element: item,
                    name: nameEl.textContent.trim().toLowerCase(),
                    id: item.id
                });
            }
        });

        console.log(`📦 Saved ${allChats.length} chats`);
    }

    function handleSearchInput(e) {
        const query = e.target.value.trim();

        if (searchTimeout) {
            clearTimeout(searchTimeout);
        }

        if (!query) {
            clearSearchResults();
            return;
        }

        if (query.length < MIN_SEARCH_LENGTH) {
            return;
        }

        searchTimeout = setTimeout(() => {
            const role = getUserRole();

            if (role === 'Teacher') {
                // Teacher: جستجوی محلی در چتها
                searchInChatsLocal(query);
            } else if (isManagerOrPersonel()) {
                // Manager/Personel: جستجوی کاربر از سرور
                searchUsersFromServer(query);
            } else {
                // سایر نقشها: جستجوی محلی
                searchInChatsLocal(query);
            }
        }, SEARCH_DELAY);
    }

    // Helper function to escape HTML
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // جستجوی محلی برای Teacher
    function searchInChatsLocal(query) {
        const lowerQuery = query.toLowerCase();
        const results = allChats.filter(chat => 
            chat.name.includes(lowerQuery)
        );

        console.log(`🔍 Local search: "${query}" -> ${results.length} results`);

        if (results.length === 0) {
            showMessage('گروهی یافت نشد');
        } else {
            displayChatResults(results);
        }
    }

    // نمایش نتایج چتها
    function displayChatResults(results) {
        let html = '<div style="padding-top: 10px;">';
        html += '<h6 style="padding: 0 0 10px 0; color: rgba(var(--dark-text), 1);">نتایج جستجو:</h6>';
        
        results.forEach(chat => {
            const escapedName = escapeHtml(chat.name);
            html += `
                <div class="search-result-item" data-chat-id="${escapeHtml(chat.id)}">
                    <div class="search-result-info">
                        <p class="search-result-name">${escapedName}</p>
                    </div>
                </div>
            `;
        });
        
        html += '</div>';
        searchResults.innerHTML = html;

        // رویداد کلیک
        const items = searchResults.querySelectorAll('.search-result-item');
        items.forEach(item => {
            item.addEventListener('click', function() {
                const chatId = this.dataset.chatId;
                openChat(chatId);
            });
        });
    }

    // جستجوی کاربر از سرور (Manager/Personel)
    function searchUsersFromServer(query) {
        console.log(`🌐 Server search: "${query}" (type: ${currentSearchType})`);
        showLoading();

        // ساخت query parameters
        const params = new URLSearchParams({
            query: query,
            searchType: currentSearchType
        });

        $.ajax({
            url: `/api/chat/searchUsers?${params.toString()}`,
            type: 'GET',
            success: function(response) {
                console.log('✅ Search response:', response);
                
                if (response.success && response.data && response.data.length > 0) {
                    displayUserResults(response.data);
                } else {
                    showMessage('کاربری یافت نشد');
                }
            },
            error: function(xhr) {
                console.error('❌ Search error:', xhr);
                
                if (xhr.status === 403) {
                    showMessage('شما مجاز به جستجوی کاربران نیستید');
                } else if (xhr.status === 400) {
                    const response = xhr.responseJSON;
                    showMessage(response?.message || 'متن جستجو نامعتبر است');
                } else {
                    showMessage('خطا در جستجو. لطفا دوباره تلاش کنید.');
                }
            }
        });
    }

    // نمایش نتایج کاربران
    function displayUserResults(users) {
        const baseUrl = document.getElementById('baseUrl')?.value || '';
        
        let html = '<div style="padding-top: 10px;">';
        html += '<h6 style="padding: 0 0 10px 0; color: rgba(var(--dark-text), 1);">نتایج جستجو:</h6>';

        users.forEach(user => {
            const escapedDisplayName = escapeHtml(user.nameFamily || 'بدون نام');
            const escapedRoleFa = escapeHtml(user.roleFaName || user.roleName || '');
            const escapedDept = user.deptName ? ` - ${escapeHtml(user.deptName)}` : '';
            
            // avatar URL is constructed server-side, but escape for safety
            const avatarUrl = user.profilePicName ? 
                `${baseUrl}/uploads/thumb/1/${encodeURIComponent(user.profilePicName)}` : 
                '/chatzy/assets/images/avatar/UserIcon.png';

            html += `
                <div class="search-result-item" data-user-id="${user.userId}" data-user-name="${escapedDisplayName}">
                    <img src="${escapeHtml(avatarUrl)}" 
                         alt="${escapedDisplayName}" 
                         class="search-result-avatar" 
                         onerror="this.src='/chatzy/assets/images/avatar/UserIcon.png'">
                    <div class="search-result-info">
                        <p class="search-result-name">${escapedDisplayName}</p>
                        <p class="search-result-role">${escapedRoleFa}${escapedDept}</p>
                    </div>
                </div>
            `;
        });

        html += '</div>';
        searchResults.innerHTML = html;

        // رویداد کلیک روی کاربر
        const items = searchResults.querySelectorAll('.search-result-item');
        items.forEach(item => {
            item.addEventListener('click', function() {
                const userId = this.dataset.userId;
                const userName = this.dataset.userName;
                startPrivateChat(userId, userName);
            });
        });
    }

    // باز کردن چت (برای Teacher)
    function openChat(chatId) {
        console.log('📂 Opening chat:', chatId);
        
        // بستن offcanvas
        if (searchOffcanvas) {
            const bsOffcanvas = bootstrap.Offcanvas.getInstance(searchOffcanvas);
            if (bsOffcanvas) {
                bsOffcanvas.hide();
            }
        }

        // کلیک روی المان چت
        const chatElement = document.getElementById(chatId);
        if (chatElement) {
            chatElement.click();
        }
    }

    // شروع چت خصوصی (برای Manager/Personel)
    function startPrivateChat(userId, userName) {
        console.log(`💬 Starting private chat with ${userName} (${userId})`);
        
        // بستن offcanvas
        if (searchOffcanvas) {
            const bsOffcanvas = bootstrap.Offcanvas.getInstance(searchOffcanvas);
            if (bsOffcanvas) {
                bsOffcanvas.hide();
            }
        }

        // فراخوانی تابع باز کردن چت
        if (typeof window.GetSelectedChatMessages === 'function') {
            try {
                window.GetSelectedChatMessages(userId, 'Private');
                
                // Toast موفقیت
                if (window.chatUIRenderer?.showToast) {
                    window.chatUIRenderer.showToast(`✅ چت با ${userName} باز شد`, 'success');
                }
            } catch (error) {
                console.error('❌ Error opening chat:', error);
                showErrorToast('خطا در باز کردن چت');
            }
        } else {
            showErrorToast('امکان باز کردن چت وجود ندارد');
        }
    }

    // نمایش loading
    function showLoading() {
        searchResults.innerHTML = `
            <div class="search-loading">
                <div class="spinner-border spinner-border-sm text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <p style="margin-top: 10px;">در حال جستجو...</p>
            </div>
        `;
    }

    // نمایش پیام
    function showMessage(message) {
        searchResults.innerHTML = `
            <div class="search-no-results">
                <p>${message}</p>
            </div>
        `;
    }

    // پاک کردن نتایج
    function clearSearchResults() {
        if (searchResults) {
            searchResults.innerHTML = '';
        }
    }

    // پاک کردن UI
    function clearSearchUI() {
        if (searchInput) {
            searchInput.value = '';
        }
        clearSearchResults();
    }

    // Toast خطا
    function showErrorToast(message) {
        if (window.chatUIRenderer?.showToast) {
            window.chatUIRenderer.showToast(message, 'error');
        } else {
            alert(message);
        }
    }

    // مقداردهی اولیه
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Export
    window.chatSearch = {
        refresh: saveChatsForLocalSearch,
        clear: clearSearchUI
    };

})();
